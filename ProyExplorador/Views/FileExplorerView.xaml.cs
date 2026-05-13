using ProyExplorador.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ProyExplorador.Views
{
    /// <summary>
    /// Code-behind mínimo del explorador.
    /// La virtualización se configura aquí en lugar de XAML porque
    /// VirtualizationMode.Recycling + IsVirtualizingWhenGrouping deben
    /// establecerse en el ItemsPanel, no en el ListView.
    /// </summary>
    public partial class FileExplorerView : UserControl
    {
        public FileExplorerView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Inicialización diferida — sólo cuando la vista sea visible
            if (DataContext is FileExplorerViewModel vm)
                _ = vm.InitializeAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Cancelar cargas en curso al salir de la vista
            if (DataContext is FileExplorerViewModel vm)
                vm.OnNavigatedFrom();
        }
    }
}
