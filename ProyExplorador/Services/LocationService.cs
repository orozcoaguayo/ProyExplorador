using System;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Resultado simple de ubicación.
    /// </summary>
    public class LocationResult
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Accuracy { get; set; }
        public string Status { get; set; } = "Desconocido";
    }

    /// <summary>
    /// Servicio para obtener y gestionar la ubicación del dispositivo.
    /// Implementación por defecto usa geolocalización por IP como fallback.
    /// </summary>
    public class LocationService
    {
        private readonly HttpClient _http = new HttpClient();

        /// <summary>
        /// Obtiene la ubicación actual de forma asíncrona.
        /// Usa IP geolocation (ipapi.co) como alternativa cross-platform.
        /// </summary>
        public async Task<LocationResult?> GetCurrentLocationAsync()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                var resp = await _http.GetAsync("https://ipapi.co/json/", cts.Token);
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                double lat = root.TryGetProperty("latitude", out var pLat) && pLat.TryGetDouble(out var dlat) ? dlat : 0;
                double lon = root.TryGetProperty("longitude", out var pLon) && pLon.TryGetDouble(out var dlon) ? dlon : 0;

                return new LocationResult
                {
                    Latitude = lat,
                    Longitude = lon,
                    Accuracy = 0,
                    Status = "Activo (IP)"
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fuerza la actualización de la ubicación.
        /// </summary>
        public Task<LocationResult?> RefreshLocationAsync() => GetCurrentLocationAsync();

        /// <summary>
        /// Abre la ubicación en Google Maps con el navegador predeterminado.
        /// </summary>
        public void OpenInGoogleMaps(double lat, double lon)
        {
            var url = $"https://www.google.com/maps/@{lat},{lon},15z";
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
    }
}
