using ProyExplorador.Helpers;
using ProyExplorador.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProyExplorador
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private object? _lastViewModel;
        private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = _vm = viewModel;

            Loaded += OnLoaded;
            _vm.PropertyChanged += OnVmPropertyChanged;
            _toastTimer.Tick += (_, _) => HideToast();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Activar dark-mode nativo DWM
            AcrylicHelper.EnableDarkMode(this);

            // Fade-in de apertura
            Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(320)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);

            await _vm.InitializeAsync();
        }

        // ── Transición de vista ───────────────────────────────────────────
        private void OnVmPropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.CurrentViewModel)) return;

            var newVm = _vm.CurrentViewModel;
            if (newVm == _lastViewModel) return;
            _lastViewModel = newVm;

            // Animar la entrada de la nueva vista
            var presenter = MainContent;
            if (presenter is null) return;

            presenter.Opacity = 0;
            var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            presenter.BeginAnimation(OpacityProperty, fade);
        }

        // ── Toast público ─────────────────────────────────────────────────
        public void ShowToast(string title, string message, string icon = "✅")
        {
            ToastTitle.Text   = title;
            ToastMessage.Text = message;
            ToastIcon.Text    = icon;
            ToastPanel.Visibility = Visibility.Visible;

            var fadeIn    = new DoubleAnimation(0, 1,    new Duration(TimeSpan.FromMilliseconds(250)));
            var slideUp   = new DoubleAnimation(30, 0,   new Duration(TimeSpan.FromMilliseconds(250)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            ToastPanel.BeginAnimation(OpacityProperty, fadeIn);
            ToastTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);

            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            _toastTimer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)));
            fadeOut.Completed += (_, _) => ToastPanel.Visibility = Visibility.Collapsed;
            ToastPanel.BeginAnimation(OpacityProperty, fadeOut);
        }

        // ── Window controls ───────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => ToggleMaximize();

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(180)));
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
        }

        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                WindowBorder.CornerRadius = new CornerRadius(14);
            }
            else
            {
                WindowState = WindowState.Maximized;
                WindowBorder.CornerRadius = new CornerRadius(0);
            }
        }
    }
}
