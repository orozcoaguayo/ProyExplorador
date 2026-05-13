using CommunityToolkit.Mvvm.ComponentModel;

namespace ProyExplorador.Models
{
    /// <summary>
    /// Representa un archivo abierto recientemente.
    /// </summary>
    public partial class RecentFile : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _fullPath = string.Empty;
        [ObservableProperty] private string _extension = string.Empty;
        [ObservableProperty] private string _icon = "📄";

        public DateTime LastAccessed { get; set; }

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - LastAccessed;
                if (diff.TotalMinutes < 1)  return "Justo ahora";
                if (diff.TotalHours < 1)    return $"Hace {(int)diff.TotalMinutes} min";
                if (diff.TotalDays < 1)     return $"Hace {(int)diff.TotalHours} h";
                if (diff.TotalDays < 7)     return $"Hace {(int)diff.TotalDays} días";
                return LastAccessed.ToString("dd/MM/yyyy");
            }
        }
    }
}
