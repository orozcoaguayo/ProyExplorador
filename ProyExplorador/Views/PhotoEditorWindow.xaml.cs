using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProyExplorador.Views
{
    /// <summary>
    /// Editor de fotos profesional con herramientas de edición, zoom,
    /// rotación, filtros y panel de propiedades.
    /// Diseño moderno estilo Windows 11 integrado con el explorador de archivos.
    /// </summary>
    public partial class PhotoEditorWindow : Window
    {
        // ── Estado de la imagen ───────────────────────────────────────────
        private string? _currentFilePath;
        private BitmapSource? _originalBitmap;
        private WriteableBitmap? _editableBitmap;
        private double _currentZoom = 1.0;
        private double _currentBrightness = 0;
        private int _rotationAngle = 0;
        private bool _isFlippedH = false;
        private bool _isFlippedV = false;
        private bool _hasUnsavedChanges = false;

        // ── Constantes ────────────────────────────────────────────────────
        private const double ZoomStep = 0.1;
        private const double ZoomMin = 0.1;
        private const double ZoomMax = 5.0;

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTORES
        // ══════════════════════════════════════════════════════════════════

        public PhotoEditorWindow()
        {
            InitializeComponent();
            InitializeWindow();
        }

        /// <summary>Constructor con ruta de archivo inicial (integración con explorador).</summary>
        public PhotoEditorWindow(string filePath) : this()
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                LoadImage(filePath);
            }
        }

        // ── Inicialización ────────────────────────────────────────────────
        private void InitializeWindow()
        {
            // Drag & drop
            MainImage.AllowDrop = true;
            MainImage.Drop += MainImage_Drop;
            MainImage.DragOver += MainImage_DragOver;

            // Eventos de ventana
            Closing += PhotoEditorWindow_Closing;
        }

        // ══════════════════════════════════════════════════════════════════
        //  CARGA DE IMAGEN
        // ══════════════════════════════════════════════════════════════════

        private void LoadImage(string filePath)
        {
            try
            {
                // Validación de archivo
                if (!File.Exists(filePath))
                {
                    ShowError("Archivo no encontrado", $"No se pudo encontrar:\n{filePath}");
                    return;
                }

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp")
                {
                    ShowError("Formato no soportado",
                        "Solo se admiten imágenes JPG, PNG y BMP.");
                    return;
                }

                TxtStatus.Text = "Cargando imagen...";

                // Cargar bitmap original
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                _originalBitmap = bitmap;
                _editableBitmap = new WriteableBitmap(bitmap);
                _currentFilePath = filePath;

                // Resetear transformaciones
                _rotationAngle = 0;
                _isFlippedH = false;
                _isFlippedV = false;
                _currentBrightness = 0;
                SliderBrightness.Value = 0;

                // Aplicar imagen
                ApplyCurrentImage();

                // Actualizar UI
                NoImageOverlay.Visibility = Visibility.Collapsed;
                TxtFileName.Text = Path.GetFileName(filePath);
                TxtStatus.Text = $"Imagen cargada: {Path.GetFileName(filePath)}";
                _hasUnsavedChanges = false;

                // Cargar propiedades
                LoadImageProperties(filePath);

                // Zoom inicial: ajustar a ventana
                ZoomToFit();
            }
            catch (Exception ex)
            {
                ShowError("Error al cargar imagen", ex.Message);
                TxtStatus.Text = "Error al cargar imagen";
            }
        }

        private void ApplyCurrentImage()
        {
            if (_editableBitmap == null) return;

            MainImage.Source = _editableBitmap;
            MainImage.RenderTransform = BuildTransform();
        }

        private Transform BuildTransform()
        {
            var group = new TransformGroup();

            // Rotación
            if (_rotationAngle != 0)
                group.Children.Add(new RotateTransform(_rotationAngle));

            // Volteos
            var scaleX = _isFlippedH ? -1 : 1;
            var scaleY = _isFlippedV ? -1 : 1;
            if (scaleX != 1 || scaleY != 1)
                group.Children.Add(new ScaleTransform(scaleX, scaleY));

            return group;
        }

        // ══════════════════════════════════════════════════════════════════
        //  DRAG & DROP
        // ══════════════════════════════════════════════════════════════════

        private void MainImage_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void MainImage_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    LoadImage(files[0]);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PROPIEDADES DE IMAGEN
        // ══════════════════════════════════════════════════════════════════

        private void LoadImageProperties(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);

                // ── Información general ───────────────────────────────────
                PropFileName.Text = fileInfo.Name;
                PropFilePath.Text  = fileInfo.DirectoryName ?? "—";
                PropExtension.Text = fileInfo.Extension.ToUpperInvariant().TrimStart('.');
                PropFileSize.Text  = FormatFileSize(fileInfo.Length);
                PropCreated.Text   = fileInfo.CreationTime.ToString("dd/MM/yyyy\nHH:mm");
                PropModified.Text  = fileInfo.LastWriteTime.ToString("dd/MM/yyyy\nHH:mm");

                // ── Información de imagen ─────────────────────────────────
                if (_originalBitmap != null)
                {
                    PropResolution.Text  = $"{_originalBitmap.PixelWidth} × {_originalBitmap.PixelHeight} px";
                    PropDpiX.Text        = $"{_originalBitmap.DpiX:F0} ppp";
                    PropDpiY.Text        = $"{_originalBitmap.DpiY:F0} ppp";
                    PropFormat.Text      = fileInfo.Extension.ToUpperInvariant().TrimStart('.');
                    PropColorDepth.Text  = GetColorDepthDescription(_originalBitmap.Format);
                }

                // ── Metadatos EXIF ────────────────────────────────────────
                LoadExifMetadata(filePath);
            }
            catch
            {
                PropFileName.Text = "Error al cargar propiedades";
            }
        }

        private void LoadExifMetadata(string filePath)
        {
            try
            {
                ExifDataPanel.Visibility  = Visibility.Collapsed;
                ExifEmptyPanel.Visibility = Visibility.Visible;

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = BitmapDecoder.Create(stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.None);

                if (decoder.Frames.Count == 0) return;

                var frame    = decoder.Frames[0];
                var metadata = frame.Metadata as BitmapMetadata;

                if (metadata == null) return;

                bool hasAny = false;

                // Cámara (fabricante)
                string? make = SafeGetMetadata<string>(metadata, "/app1/ifd/{ushort=271}");
                if (!string.IsNullOrWhiteSpace(make))
                {
                    PropExifMake.Text = make.Trim();
                    hasAny = true;
                }

                // Modelo de dispositivo
                string? model = SafeGetMetadata<string>(metadata, "/app1/ifd/{ushort=272}");
                if (!string.IsNullOrWhiteSpace(model))
                {
                    PropExifModel.Text = model.Trim();
                    hasAny = true;
                }

                // Fecha de captura
                string? dateTaken = SafeGetMetadata<string>(metadata, "/app1/ifd/exif/{ushort=36867}");
                if (!string.IsNullOrWhiteSpace(dateTaken))
                {
                    PropExifDateTaken.Text = FormatExifDate(dateTaken);
                    hasAny = true;
                }

                // ISO
                ushort? iso = SafeGetMetadata<ushort>(metadata, "/app1/ifd/exif/{ushort=34855}");
                if (iso.HasValue)
                {
                    PropExifISO.Text = $"ISO {iso.Value}";
                    hasAny = true;
                }

                // Apertura (FNumber)
                ulong? fnRaw = SafeGetMetadata<ulong>(metadata, "/app1/ifd/exif/{ushort=33437}");
                if (fnRaw.HasValue)
                {
                    double fn = ExifRationalToDouble(fnRaw.Value);
                    PropExifAperture.Text = fn > 0 ? $"f/{fn:F1}" : "—";
                    hasAny = true;
                }

                // GPS — latitud / longitud
                string gpsText = ReadGpsCoordinates(metadata);
                if (!string.IsNullOrEmpty(gpsText))
                {
                    PropExifGps.Text = gpsText;
                    hasAny = true;
                }

                if (hasAny)
                {
                    ExifDataPanel.Visibility  = Visibility.Visible;
                    ExifEmptyPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // Sin EXIF o formato no soportado → panel vacío ya está visible
                ExifDataPanel.Visibility  = Visibility.Collapsed;
                ExifEmptyPanel.Visibility = Visibility.Visible;
            }
        }

        private static T? SafeGetMetadata<T>(BitmapMetadata metadata, string query)
        {
            try
            {
                var val = metadata.GetQuery(query);
                if (val is T typed) return typed;
            }
            catch { /* campo no existe */ }
            return default;
        }

        private static double ExifRationalToDouble(ulong rational)
        {
            uint numerator   = (uint)(rational & 0xFFFFFFFF);
            uint denominator = (uint)(rational >> 32);
            return denominator == 0 ? 0 : (double)numerator / denominator;
        }

        private static string FormatExifDate(string raw)
        {
            // Formato EXIF: "YYYY:MM:DD HH:MM:SS"
            try
            {
                if (raw.Length >= 19)
                {
                    var normalized = raw.Replace(':', '-').Substring(0, 10) + raw.Substring(10);
                    if (DateTime.TryParse(normalized, out var dt))
                        return dt.ToString("dd/MM/yyyy  HH:mm:ss");
                }
            }
            catch { /* fallback */ }
            return raw;
        }

        private static string ReadGpsCoordinates(BitmapMetadata metadata)
        {
            try
            {
                var latRef  = SafeGetMetadata<string>(metadata, "/app1/ifd/gps/{ushort=1}");
                var lonRef  = SafeGetMetadata<string>(metadata, "/app1/ifd/gps/{ushort=3}");
                var latArr  = metadata.GetQuery("/app1/ifd/gps/{ushort=2}")  as ulong[];
                var lonArr  = metadata.GetQuery("/app1/ifd/gps/{ushort=4}")  as ulong[];

                if (latArr != null && latArr.Length == 3 && lonArr != null && lonArr.Length == 3)
                {
                    double lat = DmsToDecimal(latArr);
                    double lon = DmsToDecimal(lonArr);
                    if (latRef == "S") lat = -lat;
                    if (lonRef == "W") lon = -lon;
                    return $"{lat:F5}°, {lon:F5}°";
                }
            }
            catch { /* sin GPS */ }
            return string.Empty;
        }

        private static double DmsToDecimal(ulong[] dms)
        {
            double degrees = ExifRationalToDouble(dms[0]);
            double minutes = ExifRationalToDouble(dms[1]);
            double seconds = ExifRationalToDouble(dms[2]);
            return degrees + minutes / 60.0 + seconds / 3600.0;
        }

        private static string GetColorDepthDescription(PixelFormat fmt)
        {
            if (fmt == PixelFormats.Bgr24 || fmt == PixelFormats.Rgb24)  return "24 bits";
            if (fmt == PixelFormats.Bgr32 || fmt == PixelFormats.Bgra32 ||
                fmt == PixelFormats.Pbgra32)                              return "32 bits";
            if (fmt == PixelFormats.Gray8)                                return "8 bits (gris)";
            if (fmt == PixelFormats.Gray16)                               return "16 bits (gris)";
            if (fmt == PixelFormats.Rgb48 || fmt == PixelFormats.Rgba64) return "48/64 bits";
            if (fmt == PixelFormats.Indexed8 || fmt == PixelFormats.Indexed4) return "Indexado";
            return fmt.ToString();
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }

        // ══════════════════════════════════════════════════════════════════
        //  BOTONES TOOLBAR — ARCHIVO
        // ══════════════════════════════════════════════════════════════════

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(this,
                    "Hay cambios sin guardar. ¿Deseas continuar sin guardar?",
                    "Cambios sin guardar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Abrir imagen",
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp|Todos los archivos|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
            {
                LoadImage(dialog.FileName);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                BtnSaveAs_Click(sender, e);
                return;
            }

            SaveImage(_currentFilePath);
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null)
            {
                ShowError("Sin imagen", "No hay ninguna imagen cargada para guardar.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Guardar imagen como",
                Filter = "JPEG (*.jpg)|*.jpg|PNG (*.png)|*.png|BMP (*.bmp)|*.bmp",
                FileName = Path.GetFileNameWithoutExtension(_currentFilePath ?? "imagen")
            };

            if (dialog.ShowDialog(this) == true)
            {
                SaveImage(dialog.FileName);
            }
        }

        private void SaveImage(string path)
        {
            try
            {
                TxtStatus.Text = "Guardando imagen...";

                BitmapEncoder encoder;
                var ext = Path.GetExtension(path).ToLowerInvariant();

                switch (ext)
                {
                    case ".png":
                        encoder = new PngBitmapEncoder();
                        break;
                    case ".bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    default:
                        encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                        break;
                }

                // Renderizar imagen con todas las transformaciones
                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    context.PushTransform(BuildTransform());
                    context.DrawImage(_editableBitmap, new Rect(0, 0, _editableBitmap.Width, _editableBitmap.Height));
                }

                var renderBitmap = new RenderTargetBitmap(
                    _editableBitmap.PixelWidth,
                    _editableBitmap.PixelHeight,
                    96, 96, PixelFormats.Pbgra32);
                renderBitmap.Render(visual);

                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                using var stream = new FileStream(path, FileMode.Create);
                encoder.Save(stream);

                _hasUnsavedChanges = false;
                TxtStatus.Text = $"Imagen guardada: {Path.GetFileName(path)}";
                MessageBox.Show(this, $"Imagen guardada correctamente en:\n{path}",
                    "Guardado exitoso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Error al guardar", ex.Message);
                TxtStatus.Text = "Error al guardar imagen";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  BOTONES TOOLBAR — TRANSFORMACIONES
        // ══════════════════════════════════════════════════════════════════

        private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;
            _rotationAngle = (_rotationAngle - 90) % 360;
            ApplyCurrentImage();
            _hasUnsavedChanges = true;
            TxtStatus.Text = "Imagen rotada 90° a la izquierda";
        }

        private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;
            _rotationAngle = (_rotationAngle + 90) % 360;
            ApplyCurrentImage();
            _hasUnsavedChanges = true;
            TxtStatus.Text = "Imagen rotada 90° a la derecha";
        }

        private void BtnFlipHorizontal_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;
            _isFlippedH = !_isFlippedH;
            ApplyCurrentImage();
            _hasUnsavedChanges = true;
            TxtStatus.Text = "Imagen volteada horizontalmente";
        }

        private void BtnFlipVertical_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;
            _isFlippedV = !_isFlippedV;
            ApplyCurrentImage();
            _hasUnsavedChanges = true;
            TxtStatus.Text = "Imagen volteada verticalmente";
        }

        // ══════════════════════════════════════════════════════════════════
        //  BOTONES TOOLBAR — FILTROS
        // ══════════════════════════════════════════════════════════════════

        private void BtnGrayscale_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;

            TxtStatus.Text = "Aplicando escala de grises...";
            ApplyGrayscale();
            _hasUnsavedChanges = true;
            TxtStatus.Text = "Filtro escala de grises aplicado";
        }

        private void BtnNegative_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;

            TxtStatus.Text = "Aplicando negativo...";
            ApplyNegative();
            _hasUnsavedChanges = true;
            TxtStatus.Text = "Filtro negativo aplicado";
        }

        private void ApplyGrayscale()
        {
            if (_editableBitmap == null) return;

            var wb = _editableBitmap;
            wb.Lock();

            unsafe
            {
                var buffer = (byte*)wb.BackBuffer;
                int stride = wb.BackBufferStride;
                int height = wb.PixelHeight;

                for (int y = 0; y < height; y++)
                {
                    byte* row = buffer + (y * stride);
                    for (int x = 0; x < wb.PixelWidth; x++)
                    {
                        int offset = x * 4;
                        byte b = row[offset];
                        byte g = row[offset + 1];
                        byte r = row[offset + 2];

                        byte gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                        row[offset] = gray;
                        row[offset + 1] = gray;
                        row[offset + 2] = gray;
                    }
                }
            }

            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
            wb.Unlock();
        }

        private void ApplyNegative()
        {
            if (_editableBitmap == null) return;

            var wb = _editableBitmap;
            wb.Lock();

            unsafe
            {
                var buffer = (byte*)wb.BackBuffer;
                int stride = wb.BackBufferStride;
                int height = wb.PixelHeight;

                for (int y = 0; y < height; y++)
                {
                    byte* row = buffer + (y * stride);
                    for (int x = 0; x < wb.PixelWidth; x++)
                    {
                        int offset = x * 4;
                        row[offset] = (byte)(255 - row[offset]);       // B
                        row[offset + 1] = (byte)(255 - row[offset + 1]); // G
                        row[offset + 2] = (byte)(255 - row[offset + 2]); // R
                    }
                }
            }

            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
            wb.Unlock();
        }

        // ══════════════════════════════════════════════════════════════════
        //  BRILLO
        // ══════════════════════════════════════════════════════════════════

        private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_editableBitmap == null || _originalBitmap == null) return;

            double delta = e.NewValue - _currentBrightness;
            _currentBrightness = e.NewValue;

            TxtBrightness.Text = $"{(int)e.NewValue}";
            ApplyBrightnessDelta(delta);
            _hasUnsavedChanges = true;
        }

        private void ApplyBrightnessDelta(double delta)
        {
            if (_editableBitmap == null) return;

            var wb = _editableBitmap;
            wb.Lock();

            int offset = (int)delta;

            unsafe
            {
                var buffer = (byte*)wb.BackBuffer;
                int stride = wb.BackBufferStride;
                int height = wb.PixelHeight;

                for (int y = 0; y < height; y++)
                {
                    byte* row = buffer + (y * stride);
                    for (int x = 0; x < wb.PixelWidth; x++)
                    {
                        int idx = x * 4;
                        row[idx] = ClampByte(row[idx] + offset);       // B
                        row[idx + 1] = ClampByte(row[idx + 1] + offset); // G
                        row[idx + 2] = ClampByte(row[idx + 2] + offset); // R
                    }
                }
            }

            wb.AddDirtyRect(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight));
            wb.Unlock();
        }

        private byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);

        // ══════════════════════════════════════════════════════════════════
        //  ZOOM
        // ══════════════════════════════════════════════════════════════════

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;
            SetZoom(_currentZoom + ZoomStep);
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;
            SetZoom(_currentZoom - ZoomStep);
        }

        private void BtnZoomFit_Click(object sender, RoutedEventArgs e)
        {
            if (_editableBitmap == null) return;
            ZoomToFit();
        }

        private void SetZoom(double zoom)
        {
            _currentZoom = Math.Clamp(zoom, ZoomMin, ZoomMax);
            ImageScale.ScaleX = _currentZoom;
            ImageScale.ScaleY = _currentZoom;
            TxtZoom.Text = $"{(int)(_currentZoom * 100)}%";
        }

        private void ZoomToFit()
        {
            if (_editableBitmap == null) return;

            double viewWidth = ImageScrollViewer.ActualWidth;
            double viewHeight = ImageScrollViewer.ActualHeight;
            double imgWidth = _editableBitmap.PixelWidth;
            double imgHeight = _editableBitmap.PixelHeight;

            if (viewWidth <= 0 || viewHeight <= 0) return;

            double zoomX = viewWidth / imgWidth;
            double zoomY = viewHeight / imgHeight;
            double fitZoom = Math.Min(zoomX, zoomY) * 0.95;

            SetZoom(fitZoom);
        }

        // ══════════════════════════════════════════════════════════════════
        //  RESET
        // ══════════════════════════════════════════════════════════════════

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null) return;

            var result = MessageBox.Show(this,
                "¿Deseas restablecer la imagen a su estado original? Se perderán todos los cambios.",
                "Restablecer imagen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _editableBitmap = new WriteableBitmap(_originalBitmap);
                _rotationAngle = 0;
                _isFlippedH = false;
                _isFlippedV = false;
                _currentBrightness = 0;
                SliderBrightness.Value = 0;
                ApplyCurrentImage();
                SetZoom(1.0);
                _hasUnsavedChanges = false;
                TxtStatus.Text = "Imagen restablecida al original";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  CONTROLES DE VENTANA
        // ══════════════════════════════════════════════════════════════════

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void PhotoEditorWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(this,
                    "Hay cambios sin guardar. ¿Deseas salir sin guardar?",
                    "Cambios sin guardar", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════

        private void ShowError(string title, string message) =>
            MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
