using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Servicio de cámara: enumera dispositivos, inicia/detiene la captura
    /// y entrega frames al suscriptor como BitmapSource para WPF.
    /// Implementa IDisposable para liberar correctamente los recursos COM.
    /// </summary>
    public sealed class CameraService : IDisposable
    {
        private VideoCaptureDevice? _device;
        private bool _disposed;

        /// <summary>Se invoca en el hilo de UI con cada nuevo frame.</summary>
        public event Action<BitmapSource>? FrameReady;

        // ── Enumeración de dispositivos ────────────────────────────────────
        /// <summary>Devuelve la lista de cámaras disponibles en el sistema.</summary>
        public static IReadOnlyList<(string MonikerString, string Name)> GetDevices()
        {
            var collection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            var result = new List<(string, string)>();
            foreach (FilterInfo fi in collection)
                result.Add((fi.MonikerString, fi.Name));
            return result;
        }

        // ── Inicio ────────────────────────────────────────────────────────
        /// <summary>
        /// Abre la primera cámara disponible (o la indicada por índice).
        /// Lanza <see cref="InvalidOperationException"/> si no hay cámaras.
        /// </summary>
        public void Start(int deviceIndex = 0)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var devices = GetDevices();
            if (devices.Count == 0)
                throw new InvalidOperationException("No se encontró ninguna cámara en el sistema.");

            if (deviceIndex >= devices.Count)
                deviceIndex = 0;

            _device = new VideoCaptureDevice(devices[deviceIndex].MonikerString);
            _device.NewFrame += OnNewFrame;
            _device.Start();
        }

        // ── Parada ────────────────────────────────────────────────────────
        public void Stop()
        {
            if (_device is null) return;
            _device.NewFrame -= OnNewFrame;
            if (_device.IsRunning)
            {
                _device.SignalToStop();
                _device.WaitForStop();
            }
            _device = null;
        }

        public bool IsRunning => _device?.IsRunning ?? false;

        // ── Captura de frame ──────────────────────────────────────────────
        /// <summary>
        /// Captura el frame actual como <see cref="BitmapSource"/> (para mostrar en WPF).
        /// Devuelve null si la cámara no está activa.
        /// </summary>
        public BitmapSource? CaptureFrame()
        {
            return _lastFrame;
        }

        private BitmapSource? _lastFrame;

        private void OnNewFrame(object sender, NewFrameEventArgs e)
        {
            try
            {
                // Clonar el bitmap ANTES de que AForge lo libere
                var bmp = (Bitmap)e.Frame.Clone();
                var bs  = ConvertToBitmapSource(bmp);
                bs.Freeze(); // obligatorio para cruzar hilos en WPF
                _lastFrame = bs;

                Application.Current?.Dispatcher.BeginInvoke(() => FrameReady?.Invoke(bs));
            }
            catch { /* frame descartado */ }
        }

        // ── Guardado ──────────────────────────────────────────────────────
        /// <summary>Guarda el frame actual como JPG en <paramref name="filePath"/>.</summary>
        public bool SavePhoto(string filePath)
        {
            if (_lastFrame is null) return false;
            try
            {
                var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
                encoder.Frames.Add(BitmapFrame.Create(_lastFrame));
                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                encoder.Save(stream);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Conversión helper ─────────────────────────────────────────────
        private static BitmapSource ConvertToBitmapSource(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption  = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            return bi;
        }

        // ── Dispose ───────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
        }
    }
}
