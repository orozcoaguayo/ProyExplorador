using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProyExplorador.Models;
using ProyExplorador.Services;
using System.Collections.ObjectModel;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// Buscador avanzado de archivos.
    /// Optimizaciones:
    ///   • Debounce de 400 ms — no lanza búsqueda en cada pulsación
    ///   • CancellationToken — cancela la búsqueda anterior antes de iniciar la nueva
    ///   • Búsqueda recursiva paralela con SemaphoreSlim (en FileService)
    ///   • Resultados se agregan en batches desde el hilo de background al UI
    /// </summary>
    public partial class SearchViewModel : ViewModelBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<SearchViewModel> _logger;

        [ObservableProperty] private string _query = string.Empty;
        [ObservableProperty] private string _rootPath;
        [ObservableProperty] private string _filterExtension = string.Empty;
        [ObservableProperty] private string _resultCount = "Sin resultados";
        [ObservableProperty] private bool   _hasResults;

        public ObservableCollection<FileItem> Results { get; } = [];

        // Debounce + cancellation
        private CancellationTokenSource _searchCts = new();
        private Timer? _debounceTimer;
        private const int DebounceMs = 400;

        public SearchViewModel(IFileService fileService, ILogger<SearchViewModel> logger)
        {
            _fileService = fileService;
            _logger      = logger;
            _rootPath    = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        // ── Partial para detectar cambios en Query ─────────────────────────
        partial void OnQueryChanged(string value)
        {
            // Debounce: reiniciar timer en cada pulsación
            _debounceTimer?.Dispose();
            if (string.IsNullOrWhiteSpace(value))
            {
                ClearResults();
                return;
            }
            _debounceTimer = new Timer(_ =>
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => _ = ExecuteSearchAsync());
            }, null, DebounceMs, Timeout.Infinite);
        }

        [RelayCommand]
        public async Task ExecuteSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(Query)) return;

            // Cancelar búsqueda previa
            await _searchCts.CancelAsync();
            _searchCts.Dispose();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            SetBusy(true, $"Buscando \"{Query}\"...");
            Results.Clear();
            HasResults   = false;
            ResultCount  = "Buscando...";
            ClearError();

            try
            {
                var ext = string.IsNullOrWhiteSpace(FilterExtension) ? null : FilterExtension.Trim();

                // SearchAsync delega en FileService que usa recursión paralela con SemaphoreSlim
                var hits = await ((FileService)_fileService)
                    .SearchAsync(RootPath, Query.Trim(), ext, ct)
                    .ConfigureAwait(false);

                if (ct.IsCancellationRequested) return;

                // Agregar resultados en el hilo UI
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var item in hits.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase))
                        Results.Add(item);

                    HasResults  = Results.Count > 0;
                    ResultCount = Results.Count == 0
                        ? "Sin resultados"
                        : $"{Results.Count:N0} resultado{(Results.Count == 1 ? "" : "s")}";
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Search cancelled: {Query}", Query);
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                _logger.LogError(ex, "Search failed: {Query}", Query);
            }
            finally { SetBusy(false); }
        }

        [RelayCommand]
        private async Task OpenResultAsync(FileItem item)
            => await _fileService.OpenFileAsync(item.FullPath);

        [RelayCommand]
        private void ClearResults()
        {
            Results.Clear();
            Query           = string.Empty;
            FilterExtension = string.Empty;
            ResultCount     = "Sin resultados";
            HasResults      = false;
            ClearError();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debounceTimer?.Dispose();
                _searchCts.Cancel();
                _searchCts.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
