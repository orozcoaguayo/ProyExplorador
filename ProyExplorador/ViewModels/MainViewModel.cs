using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyExplorador.Models;
using ProyExplorador.Services;
using ProyExplorador.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// ViewModel principal: gestiona la navegación, sidebar y estado global.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private readonly INavigationService _navigation;

        // ── Vistas inyectadas ──────────────────────────────────────────────
        public DashboardViewModel    DashboardVm    { get; }
        public FileExplorerViewModel FileExplorerVm { get; }
        public SearchViewModel       SearchVm       { get; }
        public MultimediaViewModel   MultimediaVm   { get; }
        public CleanupViewModel      CleanupVm      { get; }
        public StatsViewModel        StatsVm        { get; }
        public SettingsViewModel     SettingsVm     { get; }
        public FileReaderViewModel   FileReaderVm   { get; }
        // DataViewer is now a separate window, not a navigable ViewModel

        // ── Estado de navegación ──────────────────────────────────────────
        [ObservableProperty] private ViewModelBase _currentViewModel;
        [ObservableProperty] private NavigationItem? _selectedNavItem;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isMaximized;
        [ObservableProperty] private string _windowTitle = "ProyExplorador";

        // Datos del usuario para el footer del sidebar
        public string UserName    { get; } = Environment.UserName;
        public string UserInitial { get; } = Environment.UserName.Length > 0
            ? Environment.UserName[0].ToString().ToUpper() : "U";

        public ObservableCollection<NavigationItem> NavItems { get; } = new();

        public MainViewModel(
            INavigationService    navigation,
            DashboardViewModel    dashboardVm,
            FileExplorerViewModel fileExplorerVm,
            SearchViewModel       searchVm,
            MultimediaViewModel   multimediaVm,
            CleanupViewModel      cleanupVm,
            StatsViewModel        statsVm,
            SettingsViewModel     settingsVm,
            FileReaderViewModel   fileReaderVm)
        {
            _navigation    = navigation;
            DashboardVm    = dashboardVm;
            FileExplorerVm = fileExplorerVm;
            SearchVm       = searchVm;
            MultimediaVm   = multimediaVm;
            CleanupVm      = cleanupVm;
            StatsVm        = statsVm;
            SettingsVm     = settingsVm;
            FileReaderVm   = fileReaderVm;

            // Vista inicial
            _currentViewModel = DashboardVm;

            BuildNavItems();

            _navigation.NavigationChanged += OnNavigationChanged;

            // añadir DataViewer en el switch de navegación
        }

        // ── Construcción del menú lateral ────────────────────────────────
        private void BuildNavItems()
        {
            NavItems.Clear();
            NavItems.Add(new NavigationItem { Title = "Inicio",       Icon = "🏠", ViewKey = "Dashboard",    IsSelected = true  });
            NavItems.Add(new NavigationItem { Title = "Archivos",     Icon = "📁", ViewKey = "FileExplorer", IsSelected = false });
            NavItems.Add(new NavigationItem { Title = "Multimedia",   Icon = "🎵", ViewKey = "Multimedia",   IsSelected = false });
            NavItems.Add(new NavigationItem { Title = "Lector",       Icon = "📖", ViewKey = "FileReader",   IsSelected = false });
            NavItems.Add(new NavigationItem { Title = "Búsqueda",     Icon = "🔍", ViewKey = "Search",       IsSelected = false });
            NavItems.Add(new NavigationItem { Title = "Limpieza",     Icon = "🧹", ViewKey = "Cleanup",      IsSelected = false });
            NavItems.Add(new NavigationItem { Title = "Estadísticas", Icon = "📊", ViewKey = "Stats",        IsSelected = false });
            NavItems.Add(new NavigationItem { Title = "Configuración",Icon = "⚙️", ViewKey = "Settings",     IsSelected = false });
            // Visualizador de Datos ahora es una ventana independiente (botón en footer)
        }

        // ── Comando de navegación desde sidebar ──────────────────────────
        [RelayCommand]
        private async Task NavigateAsync(NavigationItem item)
        {
            foreach (var nav in NavItems) nav.IsSelected = false;
            item.IsSelected  = true;
            SelectedNavItem  = item;
            WindowTitle      = $"ProyExplorador — {item.Title}";
            _navigation.NavigateTo(item.ViewKey);
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            if (_navigation.CanGoBack)
            {
                _navigation.GoBack();
                await Task.CompletedTask;
            }
        }

        // ── Cámara ────────────────────────────────────────────────────────
        [RelayCommand]
        private void OpenCamera()
        {
            try
            {
                var win = new CameraWindow
                {
                    Owner = Application.Current.MainWindow
                };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo abrir la ventana de cámara:\n{ex.Message}",
                    "Error de cámara", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Editor de Fotos ───────────────────────────────────────────────
        [RelayCommand]
        private void OpenPhotoEditor()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title  = "Seleccionar imagen",
                    Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.webp|Todos los archivos|*.*"
                };

                if (dlg.ShowDialog() == true)
                {
                    var win = new PhotoEditorWindow(dlg.FileName)
                    {
                        Owner = Application.Current.MainWindow
                    };
                    win.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo abrir el editor de fotos:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Búsqueda global rápida ────────────────────────────────────────
        [RelayCommand]
        private async Task GlobalSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;
            SearchVm.Query = SearchText;
            _navigation.NavigateTo("Search");
            await SearchVm.ExecuteSearchCommand.ExecuteAsync(null);
        }

        // ── Respuesta a cambio de navegación ─────────────────────────────
        private void OnNavigationChanged(string viewKey)
        {
            // Cancelar operaciones pendientes de la vista que se abandona
            CurrentViewModel?.OnNavigatedFrom();

            CurrentViewModel = viewKey switch
            {
                "Dashboard"    => DashboardVm,
                "FileExplorer" => FileExplorerVm,
                "Multimedia"   => MultimediaVm,
                "Search"       => SearchVm,
                "Cleanup"      => CleanupVm,
                "Stats"        => StatsVm,
                "Settings"     => SettingsVm,
                "FileReader"   => FileReaderVm,
                // DataViewer handled as independent window
                _              => DashboardVm
            };

            // Sincronizar ítem seleccionado
            foreach (var nav in NavItems)
                nav.IsSelected = nav.ViewKey == viewKey;

            // debug diagnostics removed
        }

        public override async Task InitializeAsync()
        {
            await DashboardVm.InitializeAsync();
        }
    }
}
