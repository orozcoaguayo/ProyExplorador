namespace ProyExplorador.Models
{
    /// <summary>
    /// Configuración persistente de la aplicación.
    /// </summary>
    public class AppSettings
    {
        public string Theme { get; set; } = "Dark";
        public string Language { get; set; } = "es";
        public string DefaultView { get; set; } = "Dashboard";
        public string LastOpenPath { get; set; } = string.Empty;
        public bool ShowHiddenFiles { get; set; } = false;
        public bool ShowExtensions { get; set; } = true;
        public double SidebarWidth { get; set; } = 220;
        public string FileViewMode { get; set; } = "Details";
    }
}
