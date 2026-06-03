using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyExplorador.Models;
using ProyExplorador.Services;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// ViewModel del Dashboard principal: drives, accesos rápidos y recientes.
    /// Ubicación removida por completo.
    /// </summary>
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly IFileService _fileService;
        private readonly INavigationService _navigation;

        [ObservableProperty] private string _greeting = "Buenos días";
        [ObservableProperty] private string _currentDate = DateTime.Now.ToString("dddd, dd MMMM yyyy");
        [ObservableProperty] private long _totalFilesCount;
        [ObservableProperty] private string _systemInfo = string.Empty;

        public ObservableCollection<DriveItem> Drives { get; } = new();
        public ObservableCollection<RecentFile> RecentFiles { get; } = new();
        public ObservableCollection<QuickAccess> QuickAccesses { get; } = new();

        public DashboardViewModel(IFileService fileService, INavigationService navigation)
        {
            _fileService = fileService;
            _navigation = navigation;
        }

        public override async Task InitializeAsync()
        {
            SetBusy(true, "Cargando dashboard...");
            try
            {
                UpdateGreeting();
                await LoadDrivesAsync();
                await LoadRecentFilesAsync();
                LoadQuickAccesses();
                LoadSystemInfo();
            }
            finally { SetBusy(false); }
        }

        [RelayCommand]
        private async Task RefreshAsync() => await InitializeAsync();

        [RelayCommand]
        private void OpenFolder(string path)
        {
            _navigation.NavigateTo("FileExplorer");
        }

        private async Task LoadDrivesAsync()
        {
            var drives = await _fileService.GetDrivesAsync();
            Drives.Clear();
            foreach (var d in drives) Drives.Add(d);
        }

        private async Task LoadRecentFilesAsync()
        {
            var recent = await _fileService.GetRecentFilesAsync(10);
            RecentFiles.Clear();
            foreach (var r in recent) RecentFiles.Add(r);
        }

        private void LoadQuickAccesses()
        {
            QuickAccesses.Clear();
            var specials = new[]
            {
                (Environment.SpecialFolder.Desktop, "Escritorio", "🖥️"),
                (Environment.SpecialFolder.MyDocuments, "Documentos", "📄"),
                (Environment.SpecialFolder.MyPictures, "Imágenes", "🖼️"),
                (Environment.SpecialFolder.MyMusic, "Música", "🎵"),
                (Environment.SpecialFolder.MyVideos, "Videos", "🎬"),
                (Environment.SpecialFolder.UserProfile, "Usuario", "👤"),
            };

            foreach (var (folder, name, icon) in specials)
            {
                var path = Environment.GetFolderPath(folder);
                if (System.IO.Directory.Exists(path))
                    QuickAccesses.Add(new QuickAccess { Name = name, Path = path, Icon = icon });
            }
        }

        private void LoadSystemInfo()
        {
            SystemInfo = $"Windows {Environment.OSVersion.Version} · {Environment.MachineName} · {Environment.ProcessorCount} núcleos";
        }

        private void UpdateGreeting()
        {
            var hour = DateTime.Now.Hour;
            Greeting = hour switch
            {
                < 12 => "☀️  Buenos días",
                < 18 => "🌤️  Buenas tardes",
                _ => "🌙  Buenas noches"
            };
        }
    }

    /// <summary>Acceso rápido a carpeta especial.</summary>
    public class QuickAccess
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Icon { get; set; } = "📁";
    }
}
