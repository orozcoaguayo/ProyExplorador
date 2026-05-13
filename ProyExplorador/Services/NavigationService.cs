namespace ProyExplorador.Services
{
    /// <summary>
    /// Servicio de navegación MVVM: gestiona el historial y notifica cambios de vista.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly Stack<string> _history = new();
        private string _currentView = "Dashboard";

        public string CurrentView => _currentView;
        public bool CanGoBack => _history.Count > 0;

        public event Action<string>? NavigationChanged;

        public void NavigateTo(string viewKey)
        {
            if (_currentView == viewKey) return;
            _history.Push(_currentView);
            _currentView = viewKey;
            NavigationChanged?.Invoke(viewKey);
        }

        public void GoBack()
        {
            if (!CanGoBack) return;
            _currentView = _history.Pop();
            NavigationChanged?.Invoke(_currentView);
        }
    }
}
