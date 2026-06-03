using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using ProyExplorador.Services;
using System.Globalization;

namespace ProyExplorador.Views
{
    /// <summary>
    /// Interaction logic for LocationWindow.xaml
    /// </summary>
    public partial class LocationWindow : Window
    {
        private readonly LocationService _servicio;
        private LocationResult? _ultima;

        public LocationWindow()
        {
            InitializeComponent();
            _servicio = new LocationService();
            Loaded += LocationWindow_Loaded;
            Closed += (_, _) => { /* no dispose necesario */ };
        }

        private async void LocationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InicializarWebViewAsync();
            await ActualizarUbicacionAsync();
        }

        private async Task InicializarWebViewAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync();
                var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
                await webView.EnsureCoreWebView2Async(env);
                webView.NavigationCompleted += (_, _) => { /* opcional */ };
                MapHost.Children.Clear();
                MapHost.Children.Add(webView);
            }
            catch
            {
                // Mostrar fallback
                MapHost.Children.Clear();
                MapHost.Children.Add(new TextBlock { Text = "WebView2 no disponible", Foreground = System.Windows.Media.Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            }
        }

        private async Task ActualizarUbicacionAsync()
        {
            try
            {
                StatusText.Text = "Obteniendo ubicación...";
                var loc = await _servicio.GetCurrentLocationAsync();
                if (loc != null)
                {
                    _ultima = loc;
                    TxtLatitud.Text = loc.Latitude.ToString(CultureInfo.InvariantCulture);
                    TxtLongitud.Text = loc.Longitude.ToString(CultureInfo.InvariantCulture);
                    TxtPrecision.Text = loc.Accuracy.ToString(CultureInfo.InvariantCulture);
                    TxtEstado.Text = loc.Status ?? "Activo";

                    DetalleLat.Text = TxtLatitud.Text;
                    DetalleLong.Text = TxtLongitud.Text;
                    DetallePrec.Text = TxtPrecision.Text;

                    StatusText.Text = "Ubicación actualizada";

                    await MostrarEnMapaAsync(loc.Latitude, loc.Longitude);
                }
                else
                {
                    StatusText.Text = "Ubicación no disponible";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private async Task MostrarEnMapaAsync(double lat, double lon)
        {
            try
            {
                if (MapHost.Children.Count == 0) return;
                if (MapHost.Children[0] is Microsoft.Web.WebView2.Wpf.WebView2 webView && webView.CoreWebView2 != null)
                {
                    var html = "<!doctype html>\n<html>\n<head>\n<meta charset=\"utf-8\" />\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n<title>Mapa</title>\n<style>html,body,#map{{height:100%;margin:0;padding:0}}#map{{height:100%}}</style>\n</head>\n<body>\n<div id=\"map\" style=\"height:100%\"></div>\n<script>\nfunction init(){\n  var map = L.map('map').setView([" + lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + "], 13);\n  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap contributors' }).addTo(map);\n  L.marker([" + lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]).addTo(map);\n}\nvar script = document.createElement('script');\nscript.onload = init;\nscript.src = 'https://unpkg.com/leaflet@1.9.3/dist/leaflet.js';\ndocument.head.appendChild(script);\nvar link = document.createElement('link');\nlink.rel = 'stylesheet';\nlink.href = 'https://unpkg.com/leaflet@1.9.3/dist/leaflet.css';\ndocument.head.appendChild(link);\n</script>\n</body>\n</html>";
                    webView.CoreWebView2.NavigateToString(html);
                }
            }
            catch { }
        }

        private async void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            await ActualizarUbicacionAsync();
        }

        private void BtnAbrirMapa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_ultima == null) { StatusText.Text = "Sin ubicación disponible"; return; }
                _servicio.OpenInGoogleMaps(_ultima.Latitude, _ultima.Longitude);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error al abrir mapa: {ex.Message}";
            }
        }
    }
}
