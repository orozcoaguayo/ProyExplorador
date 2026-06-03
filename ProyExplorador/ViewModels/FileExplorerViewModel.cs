using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProyExplorador.Models;
using ProyExplorador.Services;
using ProyExplorador.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// ViewModel del explorador de archivos.
    /// Optimizaciones clave:
    ///   • IAsyncEnumerable → UI se puebla incrementalmente, sin freeze
    ///   • CancellationToken propio de navegación: si el usuario cambia de carpeta
    ///     antes de que termine la carga anterior, la carga vieja se cancela
    ///   • SortItems con Span-free LINQ optimizado
    ///   • FolderTree lazy: sólo expande al hacer clic
    ///   • WeakEventManager para FileSystemWatcher (no fugas de memoria)
    /// </summary>
    public partial class FileExplorerViewModel : ViewModelBase
    {
        private readonly IFileService        _fileService;
        private readonly INavigationService  _navigation;
        private readonly FileReaderViewModel _fileReader;
        private readonly MultimediaViewModel _multimediaVm;
        private readonly ILogger<FileExplorerViewModel> _logger;

        // CancellationToken específico para la carga del directorio actual
        private CancellationTokenSource _loadCts = new();

        [ObservableProperty] private string   _currentPath;
        [ObservableProperty] private FileItem? _selectedItem;
        [ObservableProperty] private string   _viewMode = "Details";
        [ObservableProperty] private string   _sortBy   = "Name";
        [ObservableProperty] private bool     _showHidden;
        [ObservableProperty] private string   _newFolderName = string.Empty;
        [ObservableProperty] private bool     _isCreatingFolder;
        [ObservableProperty] private int      _itemCount;
        [ObservableProperty] private string   _loadTimeText = string.Empty;

        public ObservableCollection<FileItem>   Items       { get; } = [];
        public ObservableCollection<string>     Breadcrumbs { get; } = [];
        public ObservableCollection<FolderNode> TreeNodes   { get; } = [];
        public ObservableCollection<string>     Favorites   { get; } = [];

        private readonly Stack<string> _backStack = new();
        private readonly Stack<string> _fwdStack  = new();

        public bool CanGoBack    => _backStack.Count > 0;
        public bool CanGoForward => _fwdStack.Count  > 0;

        private static readonly string[] DefaultFavorites =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        ];

        private static readonly HashSet<string> TextExtensions =
            new([".txt", ".log", ".md", ".json", ".xml", ".ini", ".cfg", ".yaml", ".yml", ".cs", ".html", ".htm", ".css", ".js", ".config"],
                StringComparer.OrdinalIgnoreCase);

        public FileExplorerViewModel(
            IFileService fileService,
            INavigationService navigation,
            FileReaderViewModel fileReader,
            MultimediaViewModel multimediaVm,
            ILogger<FileExplorerViewModel> logger)
        {
            _fileService = fileService;
            _navigation  = navigation;
            _fileReader  = fileReader;
            _multimediaVm = multimediaVm;
            _logger      = logger;
            _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public override async Task InitializeAsync()
        {
            foreach (var fav in DefaultFavorites)
                if (!string.IsNullOrEmpty(fav) && !Favorites.Contains(fav))
                    Favorites.Add(fav);

            await LoadFolderTreeAsync();
            await NavigateToAsync(CurrentPath);
        }

        // ── Navegación ─────────────────────────────────────────────────────
        [RelayCommand]
        public async Task NavigateToAsync(string path)
        {
            if (!System.IO.Directory.Exists(path) && !System.IO.File.Exists(path)) return;

            if (!string.IsNullOrEmpty(CurrentPath) && CurrentPath != path)
            {
                _backStack.Push(CurrentPath);
                _fwdStack.Clear();
            }

            CurrentPath = path;
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));

            UpdateBreadcrumbs(path);
            await LoadItemsAsync(path);
        }

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private async Task GoBackAsync()
        {
            if (!CanGoBack) return;
            _fwdStack.Push(CurrentPath);
            var prev = _backStack.Pop();
            CurrentPath = prev;
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            UpdateBreadcrumbs(prev);
            await LoadItemsAsync(prev);
        }

        [RelayCommand(CanExecute = nameof(CanGoForward))]
        private async Task GoForwardAsync()
        {
            if (!CanGoForward) return;
            _backStack.Push(CurrentPath);
            var next = _fwdStack.Pop();
            CurrentPath = next;
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            UpdateBreadcrumbs(next);
            await LoadItemsAsync(next);
        }

        [RelayCommand]
        private async Task GoUpAsync()
        {
            var parent = System.IO.Directory.GetParent(CurrentPath)?.FullName;
            if (parent is not null) await NavigateToAsync(parent);
        }

        [RelayCommand]
        private async Task RefreshAsync() => await LoadItemsAsync(CurrentPath);

        [RelayCommand]
        private async Task OpenItemAsync(FileItem item)
        {
            if (item.IsDirectory)
            {
                await NavigateToAsync(item.FullPath);
                return;
            }

            // Archivos con visor interno de texto
            if (TextExtensions.Contains(item.Extension))
            {
                await _fileReader.LoadFileAsync(item.FullPath);
                _navigation.NavigateTo("FileReader");
                return;
            }

            // Imágenes → Editor de fotos interno
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
            if (imageExtensions.Contains(item.Extension, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var editor = new PhotoEditorWindow(item.FullPath);
                    editor.Show();
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al abrir editor de fotos");
                    // Fallback a app nativa si falla el editor
                }
            }

            // Multimedia → Vista integrada de reproductor
            var mediaExts = new[] { ".mp4", ".avi", ".mkv", ".mov", ".mp3", ".wav", ".wma" };
            if (mediaExts.Contains(item.Extension, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    // Reproducir mediante el MultimediaViewModel inyectado y navegar a la vista
                    await _multimediaVm.PlayItemAsync(item);
                    _navigation.NavigateTo("Multimedia");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al reproducir internamente, se abrirá con la app predeterminada.");
                    // Fallback al shell de Windows
                }
            }

            // Office, PDF, vídeo → app nativa de Windows
            if (FileOpenerService.ShouldOpenNatively(item.Extension))
            {
                await Task.Run(() => FileOpenerService.OpenWithDefaultApp(item.FullPath));
                return;
            }

            // Resto → delegar al servicio centralizado (FileOpenerService) para manejo de errores
            await Task.Run(() => FileOpenerService.OpenWithDefaultApp(item.FullPath));
        }

        [RelayCommand]
        private async Task DeleteItemAsync(FileItem item)
        {
            var ok = await _fileService.DeleteItemAsync(item.FullPath);
            if (ok) Items.Remove(item);
            else SetError($"No se pudo eliminar: {item.Name}");
        }

        [RelayCommand]
        private void ChangeViewMode(string mode) => ViewMode = mode;

        [RelayCommand]
        private void ChangeSortBy(string sort)
        {
            SortBy = sort;
            var sorted = SortItems(Items.ToList());
            Items.Clear();
            foreach (var i in sorted) Items.Add(i);
        }

        // ── Creación de carpeta ────────────────────────────────────────────
        [RelayCommand]
        private void StartCreateFolder() => IsCreatingFolder = true;

        [RelayCommand]
        private async Task ConfirmCreateFolderAsync()
        {
            if (string.IsNullOrWhiteSpace(NewFolderName)) { IsCreatingFolder = false; return; }
            var newPath = System.IO.Path.Combine(CurrentPath, NewFolderName.Trim());
            try
            {
                System.IO.Directory.CreateDirectory(newPath);
                NewFolderName    = string.Empty;
                IsCreatingFolder = false;
                await LoadItemsAsync(CurrentPath);
            }
            catch (Exception ex) { SetError(ex.Message); }
        }

        [RelayCommand]
        private void CancelCreateFolder() { IsCreatingFolder = false; NewFolderName = string.Empty; }

        // ── Favoritos ─────────────────────────────────────────────────────
        [RelayCommand]
        private void AddCurrentToFavorites()
        {
            if (!string.IsNullOrEmpty(CurrentPath) && !Favorites.Contains(CurrentPath))
                Favorites.Add(CurrentPath);
        }

        [RelayCommand]
        private void RemoveFavorite(string path) => Favorites.Remove(path);

        // ── Carga incremental ──────────────────────────────────────────────
        /// <summary>
        /// Carga el directorio de forma incremental.
        /// Cancela cualquier carga anterior en curso antes de empezar.
        /// Los ítems aparecen en la UI tan pronto como se enumeran (sin freeze).
        /// </summary>
        private async Task LoadItemsAsync(string path)
        {
            if (!System.IO.Directory.Exists(path)) return;

            // Cancelar carga previa si aún estaba en curso
            await _loadCts.CancelAsync();
            _loadCts.Dispose();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;

            SetBusy(true, $"Cargando {System.IO.Path.GetFileName(path)}...");
            Items.Clear();
            ItemCount = 0;
            ClearError();

            var sw = Stopwatch.StartNew();
            try
            {
                // Buffer temporal para reducir número de UI updates
                var buffer = new List<FileItem>(64);
                const int BatchSize = 50; // insertar en lotes de 50

                await foreach (var item in _fileService.EnumerateItemsAsync(path, ShowHidden, ct)
                                                       .ConfigureAwait(false))
                {
                    if (ShowHidden || !item.IsHidden)
                        buffer.Add(item);

                    if (buffer.Count >= BatchSize)
                    {
                        var batch = SortItems(buffer).ToList();
                        buffer.Clear();

                        // Volver al hilo UI para actualizar la colección
                        await System.Windows.Application.Current.Dispatcher
                            .InvokeAsync(() =>
                            {
                                foreach (var i in batch) Items.Add(i);
                                ItemCount = Items.Count;
                            });
                    }
                }

                // Último lote
                if (buffer.Count > 0)
                {
                    var last = SortItems(buffer).ToList();
                    await System.Windows.Application.Current.Dispatcher
                        .InvokeAsync(() =>
                        {
                            foreach (var i in last) Items.Add(i);
                            ItemCount = Items.Count;
                        });
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("LoadItems cancelled for {Path}", path);
                return;  // no mostrar error al usuario — fue cancelación intencional
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                _logger.LogError(ex, "LoadItems failed for {Path}", path);
            }
            finally
            {
                sw.Stop();
                LoadTimeText = $"{ItemCount} elementos · {sw.ElapsedMilliseconds} ms";
                SetBusy(false);
            }
        }

        // ── Sorting in-memory ──────────────────────────────────────────────
        private IEnumerable<FileItem> SortItems(IEnumerable<FileItem> source) => SortBy switch
        {
            "Name"  => source.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            "Size"  => source.OrderByDescending(i => i.IsDirectory).ThenByDescending(i => i.Size),
            "Date"  => source.OrderByDescending(i => i.DateModified),
            "Type"  => source.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Extension, StringComparer.OrdinalIgnoreCase),
            _       => source.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
        };

        // ── Breadcrumbs ────────────────────────────────────────────────────
        private void UpdateBreadcrumbs(string path)
        {
            Breadcrumbs.Clear();
            var parts = path.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
            var accumulated = string.Empty;
            foreach (var part in parts)
            {
                accumulated = string.IsNullOrEmpty(accumulated)
                    ? (part.EndsWith(':') ? part + "\\" : part)
                    : System.IO.Path.Combine(accumulated, part);
                Breadcrumbs.Add(accumulated);
            }
        }

        // ── Folder tree (lazy) ─────────────────────────────────────────────
        private async Task LoadFolderTreeAsync()
        {
            var drives = await _fileService.GetDrivesAsync().ConfigureAwait(false);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TreeNodes.Clear();
                foreach (var d in drives)
                    TreeNodes.Add(new FolderNode { Name = d.Label, FullPath = d.Name, Icon = d.Icon });
            });
        }

        [RelayCommand]
        private async Task ExpandFolderNodeAsync(FolderNode node)
        {
            if (node.Children.Count > 0) return; // ya expandido

            try
            {
                var dirs = await Task.Run(() =>
                    System.IO.Directory.EnumerateDirectories(node.FullPath)
                                       .Take(200) // limitar nodos del árbol
                                       .Select(d => new DirectoryInfo(d))
                                       .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden))
                                       .Select(d => new FolderNode { Name = d.Name, FullPath = d.FullName })
                                       .ToList());

                foreach (var child in dirs)
                    node.Children.Add(child);
            }
            catch { /* sin acceso */ }
        }

        // ── Dispose ────────────────────────────────────────────────────────
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadCts.Cancel();
                _loadCts.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Nodo del árbol de carpetas — lazy expandable.</summary>
    public partial class FolderNode : ObservableObject
    {
        [ObservableProperty] private string _name     = string.Empty;
        [ObservableProperty] private string _fullPath = string.Empty;
        [ObservableProperty] private string _icon     = "📁";
        [ObservableProperty] private bool   _isExpanded;
        public ObservableCollection<FolderNode> Children { get; } = [];
    }
}
