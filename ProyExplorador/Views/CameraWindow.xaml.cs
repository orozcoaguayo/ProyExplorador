using Microsoft.Win32;
using ProyExplorador.Services;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;

namespace ProyExplorador.Views
{
    /// <summary>
    /// Ventana modal de cámara web.  Code-behind mínimo: toda la lógica de
    /// captura está encapsulada en <see cref="CameraService"/>.
    /// </summary>
    public partial class CameraWindow : Window
    {
        private readonly CameraService _camera = new();

        // ── Constructor ───────────────────────────────────────────────────
        public CameraWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        // ── Inicio ────────────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadDeviceList();
            _camera.FrameReady += OnFrameReady;
        }

        private void LoadDeviceList()
        {
            var devices = CameraService.GetDevices();
            CmbDevices.Items.Clear();
            if (devices.Count == 0)
            {
                CmbDevices.Items.Add("Sin dispositivos");
                CmbDevices.SelectedIndex = 0;
                TxtStatus.Text = "No se encontró ninguna cámara";
                BtnStart.IsEnabled = false;
                return;
            }
            foreach (var (_, name) in devices)
                CmbDevices.Items.Add(name);
            CmbDevices.SelectedIndex = 0;
        }

        // ── Eventos de botones ────────────────────────────────────────────
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var idx = CmbDevices.SelectedIndex >= 0 ? CmbDevices.SelectedIndex : 0;
                _camera.Start(idx);

                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = true;
                BtnCapture.IsEnabled = true;
                BtnStartRecording.IsEnabled = true;
                NoSignalOverlay.Visibility = Visibility.Collapsed;
                TxtStatus.Text = "Cámara activa";
            }
            catch (InvalidOperationException ex)
            {
                ShowError("Sin cámara", ex.Message);
            }
            catch (Exception ex)
            {
                ShowError("Error al iniciar cámara", ex.Message);
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopCamera();
        }

        private void BtnCapture_Click(object sender, RoutedEventArgs e)
        {
            if (!_camera.IsRunning)
            {
                ShowError("Cámara inactiva", "Inicia la cámara antes de tomar una foto.");
                return;
            }

            // Seleccionar carpeta destino
            var dialog = new SaveFileDialog
            {
                Title = "Guardar fotografía",
                Filter = "JPEG (*.jpg)|*.jpg|PNG (*.png)|*.png",
                DefaultExt = "jpg",
                FileName = $"Foto_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog(this) != true) return;

            var path = dialog.FileName;

            // Si el usuario eligió PNG, cambiamos el encoder en CameraService
            bool saved;
            if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
                saved = SaveAsPng(path);
            else
                saved = _camera.SavePhoto(path);

            if (saved)
            {
                PlayFlash();
                MessageBox.Show(this,
                    $"Foto guardada correctamente en:\n{path}",
                    "Fotografía guardada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ShowError("Error de captura", "No se pudo guardar la fotografía. Asegúrate de que la cámara esté activa.");
            }
        }

        private bool SaveAsPng(string path)
        {
            var frame = _camera.CaptureFrame();
            if (frame is null) return false;
            try
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
                using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                encoder.Save(stream);
                return true;
            }
            catch { return false; }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Selector de cámara ────────────────────────────────────────────
        private void CmbDevices_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Si la cámara estaba corriendo, reiniciarla con el nuevo dispositivo
            if (_camera.IsRunning)
            {
                StopCamera();
                BtnStart_Click(sender, new RoutedEventArgs());
            }
        }

        // ── Frame handler ─────────────────────────────────────────────────
        private void OnFrameReady(System.Windows.Media.Imaging.BitmapSource bs)
        {
            ImgPreview.Source = bs;
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private void StopCamera()
        {
            _camera.Stop();
            ImgPreview.Source = null;
            NoSignalOverlay.Visibility = Visibility.Visible;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            BtnCapture.IsEnabled = false;
            BtnStartRecording.IsEnabled = false;
            BtnStopRecording.IsEnabled = false;
            TxtStatus.Text = "Cámara desactivada";
        }

        private void PlayFlash()
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            FlashOverlay.BeginAnimation(OpacityProperty, anim);
        }

        private void ShowError(string title, string message) =>
            MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        // ── TitleBar drag ─────────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        // ── Cierre / Dispose ──────────────────────────────────────────────
        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _camera.FrameReady -= OnFrameReady;
            _camera.Dispose();
        }

        // ── Grabación de video ────────────────────────────────────────────
        private void BtnStartRecording_Click(object sender, RoutedEventArgs e)
        {
            if (!_camera.IsRunning)
            {
                ShowError("Cámara inactiva", "Inicia la cámara antes de grabar.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Guardar video (se guardará como frames JPG)",
                Filter = "Video (*.mp4)|*.mp4|Imágenes (*.jpg)|*.jpg",
                DefaultExt = "mp4",
                FileName = $"Video_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog(this) != true) return;

            if (_camera.StartRecording(dialog.FileName, 640, 480, 30))
            {
                BtnStartRecording.IsEnabled = false;
                BtnStopRecording.IsEnabled = true;
                BtnCapture.IsEnabled = false;
                TxtStatus.Text = "Grabando video...";
            }
            else
            {
                ShowError("Error", "No se pudo iniciar la grabación.");
            }
        }

        private void BtnStopRecording_Click(object sender, RoutedEventArgs e)
        {
            bool success = _camera.StopRecording();
            BtnStartRecording.IsEnabled = true;
            BtnStopRecording.IsEnabled = false;
            BtnCapture.IsEnabled = true;
            TxtStatus.Text = "Cámara activa";

            if (success)
            {
                MessageBox.Show(this, "✅ Video MP4 guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(this, "❌ Error al guardar el video. Verifica que FFmpeg esté instalado:\nchoco install ffmpeg", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}