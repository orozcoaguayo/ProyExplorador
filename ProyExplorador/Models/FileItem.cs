using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace ProyExplorador.Models
{
    /// <summary>
    /// Representa un archivo o carpeta dentro del explorador.
    /// </summary>
    public partial class FileItem : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _fullPath = string.Empty;
        [ObservableProperty] private string _extension = string.Empty;
        [ObservableProperty] private DateTime _dateModified;
        [ObservableProperty] private DateTime _dateCreated;
        [ObservableProperty] private bool _isHidden;
        [ObservableProperty] private string _icon = "📄";

        // Propiedades no observables (sin setter público)
        public long Size        { get; set; }
        public bool IsDirectory { get; set; }

        public string FormattedSize => IsDirectory ? "—" : FormatSize(Size);
        public string ItemType => IsDirectory ? "Carpeta" : (string.IsNullOrEmpty(Extension) ? "Archivo" : $"Archivo {Extension.ToUpper()}");

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
