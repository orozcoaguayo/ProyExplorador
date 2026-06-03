using System;
using System.Threading.Tasks;
using System.Windows;
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
            await ActualizarUbicacionAsync();
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
                TxtEstado.Text = loc.Status ?? "Activo";
                TxtCiudad.Text = loc.City ?? "-";

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
                StatusText.Text = $"Error al obtener ubicación: {ex.Message}";
                MessageBox.Show(
                    "No se pudo obtener la ubicación actual.",
                    "Ubicación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task MostrarEnMapaAsync(double lat, double lon)
        {
            try
            {
                // Ahora abrimos Google Maps en el navegador predeterminado con las coordenadas
                var url = $"https://www.google.com/maps?q={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Ignorar errores de apertura
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                MessageBox.Show(ex.ToString(), "ERROR COMPLETO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
