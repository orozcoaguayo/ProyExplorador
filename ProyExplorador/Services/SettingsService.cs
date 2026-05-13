using ProyExplorador.Models;
using System.IO;
using System.Text.Json;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Persiste la configuración en JSON dentro de AppData.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private static readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProyExplorador", "settings.json");

        public AppSettings Settings { get; private set; } = new();

        public void Load()
        {
            try
            {
                if (!File.Exists(_configPath)) return;
                var json = File.ReadAllText(_configPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch { Settings = new AppSettings(); }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { /* ignorar errores de escritura */ }
        }
    }
}
