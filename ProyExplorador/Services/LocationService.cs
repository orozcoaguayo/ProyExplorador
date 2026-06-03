using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Globalization;

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
        public string? City { get; set; }
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
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var url = "https://ipapi.co/json/";
                var resp = await _http.GetAsync(url, cts.Token);

                MessageBox.Show($"HTTP {(int)resp.StatusCode} - {resp.ReasonPhrase}", "Error de ubicación");
                var json = await resp.Content.ReadAsStringAsync(cts.Token);
                MessageBox.Show(json, "Respuesta ipapi.co");

                if (!resp.IsSuccessStatusCode)
                {
                    // Intentar fallback
                    try
                    {
                        var fallbackUrl = "https://ipinfo.io/json";
                        var resp2 = await _http.GetAsync(fallbackUrl, cts.Token);
                        MessageBox.Show($"Fallback HTTP {(int)resp2.StatusCode} - {resp2.ReasonPhrase}", "Error de ubicación");
                        var json2 = await resp2.Content.ReadAsStringAsync(cts.Token);
                        MessageBox.Show(json2, "Respuesta ipinfo.io");

                        if (!resp2.IsSuccessStatusCode) return null;
                        using var doc2 = JsonDocument.Parse(json2);
                        var root2 = doc2.RootElement;
                        double lat2 = 0;
                        double lon2 = 0;
                        if (root2.TryGetProperty("loc", out var ploc) && ploc.ValueKind == JsonValueKind.String)
                        {
                            var parts = ploc.GetString()?.Split(',');
                            if (parts != null && parts.Length == 2)
                            {
                                double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out lat2);
                                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out lon2);
                            }
                        }
                        if (lat2 != 0 && lon2 != 0)
                        {
                            return new LocationResult { Latitude = lat2, Longitude = lon2, Accuracy = 0, Status = "Activo (IP-fallback)", City = root2.TryGetProperty("city", out var pCity2) ? pCity2.GetString() : null };
                        }
                        return null;
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show(ex2.ToString(), "Error de ubicación");
                        return null;
                    }
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                // Mostrar resultado de la deserialización
                MessageBox.Show(root.ToString(), "Deserialización ipapi.co");

                double lat = 0;
                double lon = 0;
                string? city = null;

                // ipapi.co returns 'latitude'/'longitude' or 'lat'/'lon' sometimes; inspeccionamos
                if (root.TryGetProperty("latitude", out var pLat) && pLat.TryGetDouble(out var dlat)) lat = dlat;
                else if (root.TryGetProperty("lat", out var pLat2) && pLat2.TryGetDouble(out var dlat2)) lat = dlat2;

                if (root.TryGetProperty("longitude", out var pLon) && pLon.TryGetDouble(out var dlon)) lon = dlon;
                else if (root.TryGetProperty("lon", out var pLon2) && pLon2.TryGetDouble(out var dlon2)) lon = dlon2;

                if (root.TryGetProperty("city", out var pCity) && pCity.ValueKind == JsonValueKind.String) city = pCity.GetString();

                // Mostrar valores encontrados
                MessageBox.Show($"Parsed lat={lat}, lon={lon}, city={city}", "Error de ubicación");

                if (lat == 0 || lon == 0) return null;

                return new LocationResult
                {
                    Latitude = lat,
                    Longitude = lon,
                    Accuracy = 0,
                    Status = "Activo (IP)",
                    City = city
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error de ubicación");
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
