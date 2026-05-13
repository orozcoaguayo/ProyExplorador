using CommunityToolkit.Mvvm.ComponentModel;

namespace ProyExplorador.Models
{
    /// <summary>
    /// Información de un disco/unidad de almacenamiento.
    /// </summary>
    public partial class DriveItem : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _label = string.Empty;
        [ObservableProperty] private string _driveFormat = string.Empty;
        [ObservableProperty] private double _usagePercent;
        [ObservableProperty] private string _icon = "💾";

        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSpace { get; set; }

        public string FormattedTotal => FormatSize(TotalSize);
        public string FormattedFree  => FormatSize(FreeSpace);
        public string FormattedUsed  => FormatSize(UsedSpace);

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
