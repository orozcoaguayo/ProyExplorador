using AForge.Video;
using AForge.Video.DirectShow;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ProyExplorador.Services
{
    public sealed class CameraService : IDisposable
    {
        private VideoCaptureDevice? _device;
        private List<Bitmap>? _recordedFrames;
        private bool _isRecording;
        private bool _disposed;
        private string? _recordingPath;

        public event Action<BitmapSource>? FrameReady;

        public bool IsRunning => _device?.IsRunning ?? false;
        public bool IsRecording => _isRecording;

        public static IReadOnlyList<(string MonikerString, string Name)> GetDevices()
        {
            var collection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            var result = new List<(string, string)>();
            foreach (FilterInfo fi in collection)
                result.Add((fi.MonikerString, fi.Name));
            return result;
        }

        public void Start(int deviceIndex = 0)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var devices = GetDevices();
            if (devices.Count == 0)
                throw new InvalidOperationException("No se encontró ninguna cámara.");

            if (deviceIndex >= devices.Count)
                deviceIndex = 0;

            _device = new VideoCaptureDevice(devices[deviceIndex].MonikerString);
            _device.NewFrame += OnNewFrame;
            _device.Start();
        }

        public void Stop()
        {
            if (_device is null) return;
            if (_isRecording)
                StopRecording();

            _device.NewFrame -= OnNewFrame;
            if (_device.IsRunning)
            {
                _device.SignalToStop();
                _device.WaitForStop();
            }
            _device = null;
        }

        public BitmapSource? CaptureFrame() => _lastFrame;

        private BitmapSource? _lastFrame;

        private void OnNewFrame(object sender, NewFrameEventArgs e)
        {
            try
            {
                var bmp = (Bitmap)e.Frame.Clone();
                var bs = ConvertToBitmapSource(bmp);
                bs.Freeze();
                _lastFrame = bs;

                if (_isRecording && _recordedFrames != null)
                {
                    _recordedFrames.Add(bmp);
                }
                else
                {
                    bmp.Dispose();
                }

                Application.Current?.Dispatcher.BeginInvoke(() => FrameReady?.Invoke(bs));
            }
            catch { }
        }

        public bool StartRecording(string filePath, int width = 640, int height = 480, int frameRate = 30)
        {
            if (_isRecording) return false;

            try
            {
                if (!filePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                    filePath = Path.ChangeExtension(filePath, ".mp4");

                _recordingPath = filePath;
                _recordedFrames = new List<Bitmap>();
                _isRecording = true;
                return true;
            }
            catch
            {
                _recordedFrames = null;
                _recordingPath = null;
                _isRecording = false;
                return false;
            }
        }

        public bool StopRecording()
        {
            if (!_isRecording || _recordedFrames == null) return false;

            try
            {
                string? dir = Path.GetDirectoryName(_recordingPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string tempDir = Path.Combine(Path.GetTempPath(), "CameraTemp_" + Guid.NewGuid());
                Directory.CreateDirectory(tempDir);

                System.Diagnostics.Debug.WriteLine($"Guardando {_recordedFrames.Count} frames en {tempDir}");

                for (int i = 0; i < _recordedFrames.Count; i++)
                {
                    string framePath = Path.Combine(tempDir, $"frame_{i:D6}.jpg");
                    _recordedFrames[i].Save(framePath, ImageFormat.Jpeg);
                }

                // Usar ruta completa de FFmpeg
                bool success = ConvertToMP4(tempDir, _recordingPath ?? "video.mp4");

                try { Directory.Delete(tempDir, true); } catch { }

                foreach (var frame in _recordedFrames)
                    frame?.Dispose();

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                return false;
            }
            finally
            {
                _recordedFrames?.Clear();
                _recordedFrames = null;
                _recordingPath = null;
                _isRecording = false;
            }
        }

        private static bool ConvertToMP4(string frameDir, string outputPath)
        {
            try
            {
                // Ruta directa a FFmpeg
                string ffmpegPath = @"C:\ffmpeg\bin\ffmpeg.exe";

                if (!File.Exists(ffmpegPath))
                {
                    System.Diagnostics.Debug.WriteLine($"FFmpeg no encontrado en: {ffmpegPath}");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"Usando FFmpeg de: {ffmpegPath}");

                string inputPattern = Path.Combine(frameDir, "frame_%06d.jpg");

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-y -framerate 30 -i \"{inputPattern}\" -c:v libx264 -preset fast -crf 23 \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        System.Diagnostics.Debug.WriteLine("No se pudo iniciar FFmpeg");
                        return false;
                    }

                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"FFmpeg error: {error}");
                        return false;
                    }

                    if (!File.Exists(outputPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Archivo no creado: {outputPath}");
                        return false;
                    }

                    long fileSize = new FileInfo(outputPath).Length;
                    System.Diagnostics.Debug.WriteLine($"Video creado: {outputPath} ({fileSize} bytes)");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en conversión: {ex.Message}");
                return false;
            }
        }

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
            catch { return false; }
        }

        public bool SavePhotoPng(string filePath)
        {
            if (_lastFrame is null) return false;
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_lastFrame));
                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                encoder.Save(stream);
                return true;
            }
            catch { return false; }
        }

        private static BitmapSource ConvertToBitmapSource(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            return bi;
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_isRecording)
                StopRecording();
            Stop();
            _disposed = true;
        }
    }
}