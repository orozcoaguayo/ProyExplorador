using System.Windows.Controls;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Contrato del servicio de navegación entre vistas.
    /// </summary>
    public interface INavigationService
    {
        string CurrentView { get; }
        event Action<string>? NavigationChanged;
        void NavigateTo(string viewKey);
        bool CanGoBack { get; }
        void GoBack();
    }
}
