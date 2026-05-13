using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProyExplorador.Services;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// ViewModel de configuración de la aplicación.
    /// </summary>
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty] private string _selectedTheme;
        [ObservableProperty] private string _selectedLanguage;
        [ObservableProperty] private bool   _showHiddenFiles;
        [ObservableProperty] private bool   _showExtensions;
        [ObservableProperty] private string _savedMessage = string.Empty;

        public string[] Themes    => ["Dark", "Light", "System"];
        public string[] Languages => ["Español", "English"];

        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService  = settingsService;
            var s             = settingsService.Settings;
            _selectedTheme    = s.Theme;
            _selectedLanguage = s.Language == "es" ? "Español" : "English";
            _showHiddenFiles  = s.ShowHiddenFiles;
            _showExtensions   = s.ShowExtensions;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            var s          = _settingsService.Settings;
            s.Theme        = SelectedTheme;
            s.Language     = SelectedLanguage == "Español" ? "es" : "en";
            s.ShowHiddenFiles = ShowHiddenFiles;
            s.ShowExtensions  = ShowExtensions;
            _settingsService.Save();
            SavedMessage   = "✅ Configuración guardada correctamente";
            await Task.Delay(3000);
            SavedMessage   = string.Empty;
        }

        [RelayCommand]
        private void RestoreDefaults()
        {
            SelectedTheme    = "Dark";
            SelectedLanguage = "Español";
            ShowHiddenFiles  = false;
            ShowExtensions   = true;
        }
    }
}
