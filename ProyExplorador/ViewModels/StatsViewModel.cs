using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyExplorador.Models;
using ProyExplorador.Services;
using System.Collections.ObjectModel;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// ViewModel del panel de estadísticas.
    /// Ahora incluye métricas de proceso en tiempo real desde PerformanceMonitor.
    /// </summary>
    public partial class StatsViewModel : ViewModelBase
    {
        private readonly IFileService       _fileService;
        private readonly PerformanceMonitor _perfMonitor;

        [ObservableProperty] private string _osInfo            = string.Empty;
        [ObservableProperty] private string _machineInfo       = string.Empty;
        [ObservableProperty] private long   _totalDiskSpace;
        [ObservableProperty] private long   _totalFreeSpace;
        [ObservableProperty] private string _totalDiskFormatted = string.Empty;
        [ObservableProperty] private string _totalFreeFormatted = string.Empty;
        [ObservableProperty] private double _overallUsagePercent;

        // Métricas de proceso en tiempo real
        [ObservableProperty] private string _memoryUsage = "—";
        [ObservableProperty] private string _cpuUsage    = "—";

        public ObservableCollection<DriveItem>    Drives    { get; } = [];
        public ObservableCollection<FileTypeStat> FileTypes { get; } = [];

        public StatsViewModel(IFileService fileService, PerformanceMonitor perfMonitor)
        {
            _fileService  = fileService;
            _perfMonitor  = perfMonitor;
            _perfMonitor.MetricsUpdated += OnMetricsUpdated;
        }

        public override async Task InitializeAsync()
        {
            SetBusy(true, "Cargando estadísticas...");
            try
            {
                await LoadDrivesAsync();
                LoadSystemStats();
                LoadFileTypeStats();
                UpdatePerfMetrics();
            }
            finally { SetBusy(false); }
        }

        [RelayCommand]
        private async Task RefreshAsync() => await InitializeAsync();

        // ── Drives ─────────────────────────────────────────────────────────
        private async Task LoadDrivesAsync()
        {
            var drives = await _fileService.GetDrivesAsync().ConfigureAwait(false);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Drives.Clear();
                TotalDiskSpace = 0;
                TotalFreeSpace = 0;

                foreach (var d in drives)
                {
                    Drives.Add(d);
                    TotalDiskSpace += d.TotalSize;
                    TotalFreeSpace += d.FreeSpace;
                }

                TotalDiskFormatted  = FormatSize(TotalDiskSpace);
                TotalFreeFormatted  = FormatSize(TotalFreeSpace);
                OverallUsagePercent = TotalDiskSpace > 0
                    ? Math.Round((double)(TotalDiskSpace - TotalFreeSpace) / TotalDiskSpace * 100, 1)
                    : 0;
            });
        }

        private void LoadSystemStats()
        {
            OsInfo      = $"Windows {Environment.OSVersion.Version}";
            MachineInfo = $"{Environment.MachineName} · {Environment.ProcessorCount} núcleos · {Environment.UserName}";
        }

        private void LoadFileTypeStats()
        {
            FileTypes.Clear();
            FileTypes.Add(new FileTypeStat { TypeName = "Videos",     Icon = "🎬", Color = "#6C63FF", Percent = 22 });
            FileTypes.Add(new FileTypeStat { TypeName = "Música",     Icon = "🎵", Color = "#00B4D8", Percent = 18 });
            FileTypes.Add(new FileTypeStat { TypeName = "Imágenes",   Icon = "🖼️", Color = "#48CAE4", Percent = 25 });
            FileTypes.Add(new FileTypeStat { TypeName = "Documentos", Icon = "📄", Color = "#90E0EF", Percent = 15 });
            FileTypes.Add(new FileTypeStat { TypeName = "Archivos",   Icon = "📦", Color = "#ADE8F4", Percent = 12 });
            FileTypes.Add(new FileTypeStat { TypeName = "Otros",      Icon = "❓", Color = "#555555", Percent = 8  });
        }

        // ── Performance metrics ────────────────────────────────────────────
        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(UpdatePerfMetrics);
        }

        private void UpdatePerfMetrics()
        {
            MemoryUsage = $"{_perfMonitor.WorkingSetMB} MB";
            CpuUsage    = $"{_perfMonitor.CpuPercent:F1}%";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)                return $"{bytes} B";
            if (bytes < 1024 * 1024)         return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _perfMonitor.MetricsUpdated -= OnMetricsUpdated;
            base.Dispose(disposing);
        }
    }

    public class FileTypeStat
    {
        public string TypeName { get; set; } = string.Empty;
        public string Icon     { get; set; } = string.Empty;
        public string Color    { get; set; } = "#FFFFFF";
        public double Percent  { get; set; }
    }
}

