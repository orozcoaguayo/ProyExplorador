using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace ProyExplorador.Views
{
    /// <summary>
    /// Ventana de navegador web integrada usando WebView2.
    /// </summary>
    public partial class BrowserWindow : Window
    {
        private Microsoft.Web.WebView2.Wpf.WebView2? _webView;
        private const string HomeUrl = "https://www.google.com";

        public BrowserWindow()
        {
            InitializeComponent();
            Loaded += BrowserWindow_Loaded;
        }

        private async void BrowserWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InicializarWebViewAsync();
            IrAInicio();
        }

        private async Task InicializarWebViewAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync();
                _webView = new Microsoft.Web.WebView2.Wpf.WebView2();
                await _webView.EnsureCoreWebView2Async(env);
                _webView.NavigationCompleted += WebView_NavigationCompleted;
                _webView.NavigationStarting += WebView_NavigationStarting;
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                WebHost.Children.Clear();
                WebHost.Children.Add(_webView);
            }
            catch (Exception ex)
            {
                WebHost.Children.Clear();
                WebHost.Children.Add(new System.Windows.Controls.TextBlock { Text = $"WebView2 no disponible: {ex.Message}", Foreground = System.Windows.Media.Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            }
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            TxtDireccion.Text = e.Uri;
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                UpdateAddressBar();
            }
            else
            {
                MessageBox.Show($"Error al navegar: Código {e.WebErrorStatus}", "Navegador", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // placeholder para mensajes si se requiere
        }

        private void UpdateAddressBar()
        {
            if (_webView?.CoreWebView2 != null)
            {
                TxtDireccion.Text = _webView.CoreWebView2.Source ?? string.Empty;
            }
        }

        private void Navegar(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return;
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }
                _webView?.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al navegar: {ex.Message}", "Navegador", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IrAInicio()
        {
            Navegar(HomeUrl);
        }

        private void Recargar()
        {
            try
            {
                _webView?.CoreWebView2.Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al recargar: {ex.Message}", "Navegador", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Atras()
        {
            try
            {
                if (_webView?.CoreWebView2 != null && _webView.CoreWebView2.CanGoBack)
                    _webView.CoreWebView2.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al ir atrás: {ex.Message}", "Navegador", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Adelante()
        {
            try
            {
                if (_webView?.CoreWebView2 != null && _webView.CoreWebView2.CanGoForward)
                    _webView.CoreWebView2.GoForward();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al ir adelante: {ex.Message}", "Navegador", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAtras_Click(object sender, RoutedEventArgs e) => Atras();
        private void BtnAdelante_Click(object sender, RoutedEventArgs e) => Adelante();
        private void BtnRecargar_Click(object sender, RoutedEventArgs e) => Recargar();
        private void BtnInicio_Click(object sender, RoutedEventArgs e) => IrAInicio();
        private void BtnIr_Click(object sender, RoutedEventArgs e) => Navegar(TxtDireccion.Text.Trim());

        private void TxtDireccion_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Navegar(TxtDireccion.Text.Trim());
        }
    }
}
