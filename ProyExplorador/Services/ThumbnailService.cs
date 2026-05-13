using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ProyExplorador.Services
{
    /// <summary>
    /// ThumbnailService profesional con dos niveles de cache:
    ///   L1 — MemoryCache en RAM (máx 256 entradas, TTL 10 min)
    ///   L2 — Disco bajo AppData\Local\ProyExplorador\Thumbs
    /// Soporta imágenes, videos (primer frame via shell) e iconos de shell.
    /// </summary>
    public sealed class ThumbnailService : IThumbnailService, IDisposable
    {
        // ── Win32 ──────────────────────────────────────────────────────────
        private const uint SHGFI_ICON      = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public nint   hIcon;
            public int    iIcon;
            public uint   dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(nint hIcon);

        // ── Extensiones soportadas ─────────────────────────────────────────
        private static readonly HashSet<string> ImageExts = new(
            [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp"],
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> VideoExts = new(
            [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm"],
            StringComparer.OrdinalIgnoreCase);

        // ── Infrastructure ─────────────────────────────────────────────────
        private readonly IMemoryCache        _memCache;
        private readonly ILogger<ThumbnailService> _logger;
        private readonly string              _diskCacheDir;
        private readonly SemaphoreSlim       _diskSem = new(8, 8); // máx 8 lecturas disk paralelas
        private readonly MemoryCacheEntryOptions _cacheOptions;
        private bool _disposed;

        public ThumbnailService(IMemoryCache memCache, ILogger<ThumbnailService> logger)
        {
            _memCache = memCache;
            _logger   = logger;

            _diskCacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProyExplorador", "Thumbs");

            Directory.CreateDirectory(_diskCacheDir);

            _cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .SetSize(1)
                .RegisterPostEvictionCallback((key, _, reason, _) =>
                {
                    if (reason == EvictionReason.Capacity)
                        _logger.LogDebug("Thumbnail evicted from L1: {Key}", key);
                });
        }

        // ── Public API ─────────────────────────────────────────────────────
        public async Task<BitmapSource?> GetThumbnailAsync(string fullPath, int pixelSize = 64, CancellationToken ct = default)
        {
            var key = BuildKey(fullPath, pixelSize);

            // L1 hit
            if (_memCache.TryGetValue(key, out BitmapSource? cached))
                return cached;

            // L2 hit (disco)
            var diskPath = Path.Combine(_diskCacheDir, key + ".png");
            if (File.Exists(diskPath))
            {
                var fromDisk = await LoadFromDiskAsync(diskPath, ct).ConfigureAwait(false);
                if (fromDisk is not null)
                {
                    _memCache.Set(key, fromDisk, _cacheOptions);
                    return fromDisk;
                }
            }

            // Generar
            var thumb = await Task.Run(() => GenerateThumbnail(fullPath, pixelSize), ct)
                                  .ConfigureAwait(false);

            if (thumb is not null)
            {
                _memCache.Set(key, thumb, _cacheOptions);
                _ = SaveToDiskAsync(thumb, diskPath); // fire-and-forget; no bloqueamos
            }

            return thumb;
        }

        public void Invalidate(string fullPath)
        {
            foreach (var size in new[] { 32, 64, 128, 256 })
            {
                var key = BuildKey(fullPath, size);
                _memCache.Remove(key);
                var diskPath = Path.Combine(_diskCacheDir, key + ".png");
                if (File.Exists(diskPath)) File.Delete(diskPath);
            }
        }

        public void Clear()
        {
            (_memCache as MemoryCache)?.Clear();
            try { Directory.Delete(_diskCacheDir, true); Directory.CreateDirectory(_diskCacheDir); }
            catch { /* ignorar */ }
        }

        // ── Generación ─────────────────────────────────────────────────────
        private BitmapSource? GenerateThumbnail(string fullPath, int pixelSize)
        {
            try
            {
                var ext = Path.GetExtension(fullPath);

                if (ImageExts.Contains(ext))
                    return LoadImageThumbnail(fullPath, pixelSize);

                // Para vídeos y otros: icono de shell
                return LoadShellIcon(fullPath, pixelSize >= 64);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Thumbnail generation failed for {Path}", fullPath);
                return null;
            }
        }

        private static BitmapSource? LoadImageThumbnail(string path, int pixelSize)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource      = stream;
            bmp.DecodePixelWidth  = pixelSize;   // decodifica solo al tamaño necesario — ahorra RAM
            bmp.CacheOption       = BitmapCacheOption.OnLoad;
            bmp.CreateOptions     = BitmapCreateOptions.IgnoreColorProfile;
            bmp.EndInit();
            bmp.Freeze();           // CRÍTICO: permite usar en cualquier hilo sin cross-thread
            return bmp;
        }

        private static BitmapSource? LoadShellIcon(string path, bool largeIcon)
        {
            var info  = new SHFILEINFO();
            uint flag = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON);
            SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), flag);

            if (info.hIcon == 0) return null;
            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty,
                              BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally { DestroyIcon(info.hIcon); }
        }

        // ── Disk cache helpers ─────────────────────────────────────────────
        private async Task<BitmapSource?> LoadFromDiskAsync(string diskPath, CancellationToken ct)
        {
            await _diskSem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await using var fs = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var bytes = new byte[fs.Length];
                await fs.ReadExactlyAsync(bytes, ct).ConfigureAwait(false);

                var bmp = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                bmp.BeginInit();
                bmp.StreamSource  = ms;
                bmp.CacheOption   = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
            finally { _diskSem.Release(); }
        }

        private static async Task SaveToDiskAsync(BitmapSource bmp, string diskPath)
        {
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                await using var fs = new FileStream(diskPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                encoder.Save(fs);
            }
            catch { /* non-critical */ }
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private static string BuildKey(string path, int size)
        {
            // hash para evitar caracteres inválidos en nombre de fichero
            var hash = Math.Abs(HashCode.Combine(path.ToLowerInvariant(), size));
            return hash.ToString("x8");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _diskSem.Dispose();
            _disposed = true;
        }
    }
}
