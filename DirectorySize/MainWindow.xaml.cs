using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms;
using DirectorySize.Properties;

namespace DirectorySize
{
  public partial class MainWindow: Window
  {
    private BackgroundWorker worker;
    private Settings settings = Settings.Default;
    private Border progressBar;

    public MainWindow()
    {
      InitializeComponent();
      InitializeBackgroundWorker();
      
      // Restaurer la position et la taille de la fenêtre
      if (settings.WindowLeft >= 0 && settings.WindowTop >= 0 && 
          settings.WindowWidth > 0 && settings.WindowHeight > 0)
      {
        Left = settings.WindowLeft;
        Top = settings.WindowTop;
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        
        if (settings.WindowState == WindowState.Maximized)
        {
          // Pour éviter le clignotement, on commence en mode normal puis on maximise
          WindowState = WindowState.Normal;
          Loaded += (s, e) => WindowState = WindowState.Maximized;
        }
        else
        {
          WindowState = settings.WindowState;
        }
      }
      
      LoadLastDirectory();
      
      // Initialiser la référence à la barre de progression
      progressBar = (Border)FindName("ProgressBar");
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
      base.OnClosing(e);
      
      // Sauvegarder la position et la taille de la fenêtre
      if (WindowState == WindowState.Normal)
      {
        settings.WindowLeft = Left;
        settings.WindowTop = Top;
        settings.WindowWidth = Width;
        settings.WindowHeight = Height;
      }
      else
      {
        // Si la fenêtre est maximisée ou minimisée, on sauvegarde la position et taille restaurées
        settings.WindowLeft = RestoreBounds.Left;
        settings.WindowTop = RestoreBounds.Top;
        settings.WindowWidth = RestoreBounds.Width;
        settings.WindowHeight = RestoreBounds.Height;
      }
      
      settings.WindowState = WindowState;
      settings.Save();
    }

    private void LoadLastDirectory()
    {
      if (!string.IsNullOrEmpty(settings.LastDirectory) && Directory.Exists(settings.LastDirectory))
      {
        FolderPathTextBox.Text = settings.LastDirectory;
      }
    }

    private void SaveLastDirectory(string path)
    {
      if (Directory.Exists(path))
      {
        settings.LastDirectory = path;
        settings.Save();
      }
    }

    private void InitializeBackgroundWorker()
    {
      worker = new BackgroundWorker
      {
        WorkerReportsProgress = true,
        WorkerSupportsCancellation = true
      };

      worker.DoWork += Worker_DoWork;
      worker.ProgressChanged += Worker_ProgressChanged;
      worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
      using (var dialog = new FolderBrowserDialog())
      {
        dialog.Description = "Sélectionnez le dossier à analyser";
        
        // Définir le répertoire initial si disponible
        if (!string.IsNullOrEmpty(FolderPathTextBox.Text) && Directory.Exists(FolderPathTextBox.Text))
        {
          dialog.SelectedPath = FolderPathTextBox.Text;
        }
        
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
          FolderPathTextBox.Text = dialog.SelectedPath;
          SaveLastDirectory(dialog.SelectedPath);
        }
      }
    }

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
      string path = FolderPathTextBox.Text.Trim();

      if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
      {
        System.Windows.MessageBox.Show("Veuillez sélectionner un dossier valide.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

      // Sauvegarder le répertoire avant de lancer l'analyse
      SaveLastDirectory(path);

      if (worker.IsBusy)
      {
        worker.CancelAsync();
        AnalyzeButton.Content = "Annulation...";
        AnalyzeButton.IsEnabled = false;
        return;
      }

      // Préparer l'interface pour l'analyse
      ResultsListView.ItemsSource = null;
      StatusTextBlock.Text = "Analyse en cours...";
      AnalyzeButton.Content = "Annuler";
      AnalyzeButton.Visibility = Visibility.Visible;
      
      worker.RunWorkerAsync(argument: path);
    }

    private void Worker_DoWork(object sender, DoWorkEventArgs e)
    {
      string path = (string)e.Argument;
      var directories = new List<DirectoryInfo>();
      var rootDir = new DirectoryInfo(path);

      // Récupérer tous les sous-dossiers
      try
      {
        directories = rootDir.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
      }
      catch (UnauthorizedAccessException)
      {
        // Ignorer les dossiers sans accès
      }

      var results = new List<DirectoryInfoWithSize>();
      int total = directories.Count;
      int processed = 0;

      foreach (var dir in directories)
      {
        if (worker.CancellationPending)
        {
          e.Cancel = true;
          return;
        }

        try
        {
          var dirSize = new DirectoryInfoWithSize(dir);
          results.Add(dirSize);
          processed++;
          worker.ReportProgress((processed * 100) / total, dirSize);
        }
        catch (UnauthorizedAccessException)
        {
          // Ignorer les dossiers sans accès
          processed++;
        }
      }

      e.Result = results.OrderByDescending(d => d.Size).ToList();
    }

    private void UpdateProgress(int percentage, string currentDirectory = null)
    {
        if (progressBar == null) return;

        // Mettre à jour la largeur de la barre de progression
        Dispatcher.Invoke(() =>
        {
            // Calculer la largeur en fonction du pourcentage
            double maxWidth = StatusBarGrid.ActualWidth;
            double newWidth = (maxWidth * percentage) / 100.0;
            progressBar.Width = newWidth;

            // Mettre à jour l'opacité (plus clair au début, plus foncé à la fin)
            double opacity = 0.2 + (0.8 * (percentage / 100.0));
            progressBar.Opacity = opacity;

            // Mettre à jour le texte
            string statusText = $"Analyse en cours... {percentage}%";
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                statusText += $" - {System.IO.Path.GetFileName(currentDirectory)}";
            }
            StatusTextBlock.Text = statusText;
        });
    }

    private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        var dirInfo = e.UserState as DirectoryInfoWithSize;
        UpdateProgress(e.ProgressPercentage, dirInfo?.FullPath);
    }

    private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Réinitialiser la barre de progression
            if (progressBar != null)
            {
                progressBar.Width = 0;
            }

            if (e.Cancelled)
            {
                StatusTextBlock.Text = "Analyse annulée par l'utilisateur.";
            }
            else if (e.Error != null)
            {
                StatusTextBlock.Text = $"Erreur : {e.Error.Message}";
                System.Windows.MessageBox.Show($"Une erreur est survenue : {e.Error.Message}", "Erreur", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                var results = e.Result as List<DirectoryInfoWithSize>;
                if (results != null && results.Any())
                {
                    ResultsListView.ItemsSource = results;
                    long totalSize = results.Sum(d => d.Size);
                    StatusTextBlock.Text = $"Analyse terminée - {results.Count} dossiers analysés - Taille totale : {FormatSize(totalSize)}";
                }
                else
                {
                    StatusTextBlock.Text = "Aucun dossier trouvé ou accessible.";
                }
            }

            // Toujours réactiver le bouton et le rendre visible
            AnalyzeButton.Content = "Analyser";
            AnalyzeButton.IsEnabled = true;
            AnalyzeButton.Visibility = Visibility.Visible;
        });
    }

    private static string FormatSize(long bytes)
    {
      string[] sizes = { "octets", "Ko", "Mo", "Go", "To" };
      int order = 0;
      double len = bytes;

      while (len >= 1024 && order < sizes.Length - 1)
      {
        order++;
        len /= 1024;
      }

      return $"{len:0.##} {sizes[order]}";
    }
  }
}
