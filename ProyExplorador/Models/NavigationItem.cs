using CommunityToolkit.Mvvm.ComponentModel;

namespace ProyExplorador.Models
{
    /// <summary>
    /// Elemento de navegación en la sidebar.
    /// </summary>
    public partial class NavigationItem : ObservableObject
    {
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private string _icon = string.Empty;
        [ObservableProperty] private string _viewKey = string.Empty;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _hasNotification;
        [ObservableProperty] private int _notificationCount;
    }
}
