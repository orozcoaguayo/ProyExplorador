using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyExplorador.Services;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// ViewModel del módulo de limpieza del sistema.
    /// </summary>
    public partial class CleanupViewModel : ViewModelBase
    {
        private readonly CleanupService _cleanupService;

        [ObservableProperty] private long   _tempSize;
        [ObservableProperty] private string _tempSizeFormatted = "Calculando...";
        [ObservableProperty] private int    _cleanProgress;
        [ObservableProperty] private string _currentFile = string.Empty;
        [ObservableProperty] private string _freedSpaceText = string.Empty;
        [ObservableProperty] private bool   _cleanDone;
        [ObservableProperty] private bool   _isCalculating;

        public CleanupViewModel(CleanupService cleanupService)
        {
            _cleanupService = cleanupService;
        }

        public override async Task InitializeAsync()
        {
            await CalculateSizeAsync();
        }

        [RelayCommand]
        private async Task CalculateSizeAsync()
        {
            IsCalculating    = true;
            TempSizeFormatted = "Calculando...";
            TempSize = await _cleanupService.CalculateTempSizeAsync();
            TempSizeFormatted = FormatSize(TempSize);
            IsCalculating = false;
        }

        [RelayCommand]
        private async Task CleanTempAsync()
        {
            SetBusy(true, "Limpiando archivos temporales...");
            CleanDone   = false;
            CleanProgress = 0;

            var progress = new Progress<(int percent, string file)>(report =>
            {
                CleanProgress = report.percent;
                CurrentFile   = report.file;
            });

            var freed = await _cleanupService.CleanTempFilesAsync(progress);
            FreedSpaceText = $"✅ Espacio liberado: {FormatSize(freed)}";
            CleanProgress  = 100;
            CleanDone      = true;

            await CalculateSizeAsync();
            SetBusy(false);
        }

        [RelayCommand]
        private async Task EmptyRecycleBinAsync()
        {
            SetBusy(true, "Vaciando papelera...");
            await _cleanupService.EmptyRecycleBinAsync();
            FreedSpaceText = "✅ Papelera vaciada correctamente";
            SetBusy(false);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)                return $"{bytes} B";
            if (bytes < 1024 * 1024)         return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
