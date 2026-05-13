using ProyExplorador.Models;

namespace ProyExplorador.Services
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        void Save();
        void Load();
    }
}
