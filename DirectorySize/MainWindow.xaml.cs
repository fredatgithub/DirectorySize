using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using DirectorySize.Properties;

namespace DirectorySize
{
  public partial class MainWindow: Window
  {
    private BackgroundWorker worker;
    private Settings settings = Settings.Default;

    public MainWindow()
    {
      InitializeComponent();
      InitializeBackgroundWorker();
      LoadLastDirectory();
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

      ResultsListView.ItemsSource = null;
      StatusTextBlock.Text = "Analyse en cours...";
      AnalyzeButton.Content = "Annuler";
      
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

    private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
      var dirInfo = e.UserState as DirectoryInfoWithSize;
      if (dirInfo != null)
      {
        StatusTextBlock.Text = $"Analyse en cours... {e.ProgressPercentage}% - {dirInfo.Name}";
      }
      else
      {
        StatusTextBlock.Text = $"Analyse en cours... {e.ProgressPercentage}%";
      }
    }

    private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
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

      // Toujours réactiver le bouton et le mettre à jour
      AnalyzeButton.Content = "Analyser";
      AnalyzeButton.IsEnabled = true;
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
