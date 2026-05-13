using ProyExplorador.ViewModels;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ProyExplorador.Views
{
    public partial class MultimediaView : UserControl
    {
        private MultimediaViewModel? _vm;
        private bool _isDraggingSlider = false;

        public MultimediaView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            _vm = DataContext as MultimediaViewModel;
            if (_vm is null) return;

            // Conectar acciones del ViewModel con el MediaElement
            _vm.PlayAction      = () => MediaPlayer.Play();
            _vm.PauseAction     = () => MediaPlayer.Pause();
            _vm.StopAction      = () => { MediaPlayer.Stop(); MediaPlayer.Source = null; };
            _vm.OpenFileAction  = path =>
            {
                MediaPlayer.Source = new Uri(path, UriKind.Absolute);
                MediaPlayer.Play();
            };
            _vm.GetPositionFunc = () => MediaPlayer.Position;
            _vm.GetDurationFunc = () => MediaPlayer.NaturalDuration.HasTimeSpan
                ? MediaPlayer.NaturalDuration.TimeSpan : TimeSpan.Zero;
            _vm.SeekAction      = t => MediaPlayer.Position = t;
        }

        private void MediaPlayer_MediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_vm is not null && MediaPlayer.NaturalDuration.HasTimeSpan)
                _vm.Duration = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private async void MediaPlayer_MediaEnded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_vm?.NextCommand.CanExecute(null) == true)
                await _vm.NextCommand.ExecuteAsync(null);
        }

        private void ProgressSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            // Sólo buscar cuando el usuario arrastra (no en actualizaciones del timer)
            if (_isDraggingSlider && _vm is not null)
                _vm.SeekAction?.Invoke(TimeSpan.FromSeconds(e.NewValue));
        }
    }
}
