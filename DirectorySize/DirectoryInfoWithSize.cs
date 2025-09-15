using System;
using System.IO;
using System.Linq;

namespace DirectorySize
{
  public class DirectoryInfoWithSize
  {
    public string Name { get; set; }
    public long Size { get; set; }
    public int FileCount { get; set; }
    public string FullPath { get; set; }

    public DirectoryInfoWithSize(DirectoryInfo directoryInfo)
    {
      Name = directoryInfo.Name;
      FullPath = directoryInfo.FullName;

      try
      {
        // Calculer la taille des fichiers dans le dossier courant
        FileInfo[] files = directoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly);
        FileCount = files.Length;
        Size = files.Sum(f => f.Length);

        // Parcourir les sous-dossiers
        foreach (var dir in directoryInfo.GetDirectories("*", SearchOption.TopDirectoryOnly))
        {
          try
          {
            var dirFiles = dir.GetFiles("*", SearchOption.AllDirectories);
            FileCount += dirFiles.Length;
            Size += dirFiles.Sum(f => f.Length);
          }
          catch (UnauthorizedAccessException)
          {
            // Ignorer les dossiers sans accès
          }
        }
      }
      catch (UnauthorizedAccessException)
      {
        // Ignorer les dossiers sans accès
        Size = 0;
        FileCount = 0;
      }
    }
  }
}
