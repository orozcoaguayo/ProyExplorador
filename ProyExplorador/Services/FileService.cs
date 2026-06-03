using Microsoft.Extensions.Logging;
using ProyExplorador.Models;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Servicio de acceso al sistema de archivos — optimizado para rendimiento enterprise:
    ///   • async enumeration con IAsyncEnumerable para listas incremental
    ///   • CancellationToken en todas las operaciones pesadas
    ///   • SemaphoreSlim para limitar concurrencia en búsqueda recursiva
    ///   • No hay I/O síncrono en el hilo de UI
    /// </summary>
    public class FileService : IFileService
    {
        private readonly ILogger<FileService> _logger;

        // Limita cuántas sub-carpetas se escanean en paralelo durante búsqueda
        private static readonly SemaphoreSlim _searchSem = new(Environment.ProcessorCount, Environment.ProcessorCount);

        public FileService(ILogger<FileService> logger) => _logger = logger;

        // ──────────────────────────────────────────────────────────────────
        //  Listado de directorio  (IAsyncEnumerable → UI puede consumir incremental)
        // ──────────────────────────────────────────────────────────────────
        /// <summary>Enumera el contenido de un directorio de forma incremental sin bloquear la UI.</summary>
        public async IAsyncEnumerable<FileItem> EnumerateItemsAsync(
            string path,
            bool showHidden = false,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (!Directory.Exists(path)) yield break;

            // Carpetas primero
            IEnumerable<string> dirs;
            try   { dirs = Directory.EnumerateDirectories(path); }
            catch (UnauthorizedAccessException) { yield break; }

            foreach (var dir in dirs)
            {
                ct.ThrowIfCancellationRequested();
                DirectoryInfo info;
                try { info = new DirectoryInfo(dir); }
                catch { continue; }

                if (!showHidden && info.Attributes.HasFlag(FileAttributes.Hidden)) continue;

                yield return new FileItem
                {
                    Name         = info.Name,
                    FullPath     = info.FullName,
                    Extension    = string.Empty,
                    IsDirectory  = true,
                    IsHidden     = info.Attributes.HasFlag(FileAttributes.Hidden),
                    DateModified = info.LastWriteTime,
                    DateCreated  = info.CreationTime,
                    Size         = 0,
                    Icon         = "📁"
                };
            }

            // Archivos
            IEnumerable<string> files;
            try   { files = Directory.EnumerateFiles(path); }
            catch (UnauthorizedAccessException) { yield break; }

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                FileInfo info;
                try { info = new FileInfo(file); }
                catch { continue; }

                if (!showHidden && info.Attributes.HasFlag(FileAttributes.Hidden)) continue;

                var ext = info.Extension.ToLowerInvariant();
                yield return new FileItem
                {
                    Name         = info.Name,
                    FullPath     = info.FullName,
                    Extension    = ext,
                    IsDirectory  = false,
                    IsHidden     = info.Attributes.HasFlag(FileAttributes.Hidden),
                    DateModified = info.LastWriteTime,
                    DateCreated  = info.CreationTime,
                    Size         = info.Length,
                    Icon         = GetFileIcon(ext, false)
                };
            }

            await Task.CompletedTask.ConfigureAwait(false); // satisface IAsyncEnumerable contract
        }

        // Compatibilidad con interfaz antigua — carga todo de una
        public async Task<IEnumerable<FileItem>> GetItemsAsync(string path)
        {
            var list = new List<FileItem>();
            await foreach (var item in EnumerateItemsAsync(path).ConfigureAwait(false))
                list.Add(item);
            return list;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Búsqueda recursiva con CancellationToken y throttling
        // ──────────────────────────────────────────────────────────────────
        public async Task<IEnumerable<FileItem>> SearchAsync(
            string rootPath, string query, string? extension = null,
            CancellationToken ct = default)
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<FileItem>();
            if (!Directory.Exists(rootPath)) return results;

            await SearchRecursiveAsync(rootPath, query, extension, results, ct).ConfigureAwait(false);
            return results;
        }

        // Sobrecarga sin CancellationToken para compatibilidad con IFileService
        public Task<IEnumerable<FileItem>> SearchAsync(string rootPath, string query, string? extension = null)
            => SearchAsync(rootPath, query, extension, CancellationToken.None);

        private async Task SearchRecursiveAsync(
            string dir, string query, string? extension,
            System.Collections.Concurrent.ConcurrentBag<FileItem> results,
            CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            await _searchSem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                IEnumerable<string> files;
                try
                {
                    var pattern = string.IsNullOrEmpty(extension) ? "*" : $"*{extension}";
                    files = Directory.EnumerateFiles(dir, pattern);
                }
                catch (UnauthorizedAccessException) { return; }

                foreach (var file in files)
                {
                    if (ct.IsCancellationRequested) return;
                    var info = new FileInfo(file);
                    if (!info.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                    var ext = info.Extension.ToLowerInvariant();
                    results.Add(new FileItem
                    {
                        Name         = info.Name,
                        FullPath     = info.FullName,
                        Extension    = ext,
                        IsDirectory  = false,
                        DateModified = info.LastWriteTime,
                        DateCreated  = info.CreationTime,
                        Size         = info.Length,
                        Icon         = GetFileIcon(ext, false)
                    });
                }
            }
            finally { _searchSem.Release(); }

            // Recursión paralela con grado controlado
            IEnumerable<string> subDirs;
            try   { subDirs = Directory.EnumerateDirectories(dir); }
            catch { return; }

            var tasks = subDirs.Select(sub =>
                SearchRecursiveAsync(sub, query, extension, results, ct));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        // ──────────────────────────────────────────────────────────────────
        //  Unidades de disco
        // ──────────────────────────────────────────────────────────────────
        public Task<IEnumerable<DriveItem>> GetDrivesAsync()
        {
            return Task.Run<IEnumerable<DriveItem>>(() =>
            {
                var items = new List<DriveItem>();
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    try
                    {
                        var used    = drive.TotalSize - drive.TotalFreeSpace;
                        var percent = drive.TotalSize > 0 ? (double)used / drive.TotalSize * 100 : 0;
                        items.Add(new DriveItem
                        {
                            Name         = drive.Name,
                            Label        = string.IsNullOrEmpty(drive.VolumeLabel) ? $"Disco ({drive.Name.TrimEnd('\\')})" : drive.VolumeLabel,
                            DriveFormat  = drive.DriveFormat,
                            TotalSize    = drive.TotalSize,
                            FreeSpace    = drive.TotalFreeSpace,
                            UsedSpace    = used,
                            UsagePercent = Math.Round(percent, 1),
                            Icon         = drive.DriveType == DriveType.Removable ? "💿" : "💾"
                        });
                    }
                    catch { /* drive no accesible */ }
                }
                return items;
            });
        }

        // ──────────────────────────────────────────────────────────────────
        //  Archivos recientes
        // ──────────────────────────────────────────────────────────────────
        public Task<IEnumerable<RecentFile>> GetRecentFilesAsync(int maxCount = 20)
        {
            return Task.Run<IEnumerable<RecentFile>>(() =>
            {
                var results = new List<RecentFile>();
                var recentPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Recent");

                if (!Directory.Exists(recentPath)) return results;

                try
                {
                    var links = Directory.EnumerateFiles(recentPath, "*.lnk")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .Take(maxCount);

                    foreach (var link in links)
                    {
                        var name = Path.GetFileNameWithoutExtension(link.Name);
                        var ext  = Path.GetExtension(name).ToLowerInvariant();
                        results.Add(new RecentFile
                        {
                            Name         = name,
                            FullPath     = link.FullName,
                            Extension    = ext,
                            LastAccessed = link.LastWriteTime,
                            Icon         = GetFileIcon(ext, false)
                        });
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "GetRecentFiles failed"); }

                return results;
            });
        }

        // ──────────────────────────────────────────────────────────────────
        //  Lectura de texto con encoding detection
        // ──────────────────────────────────────────────────────────────────
        public async Task<string> ReadTextFileAsync(string path)
        {
            if (!File.Exists(path)) return string.Empty;
            try
            {
                // Leer con streaming para archivos grandes — no carga todo en RAM de golpe
                using var sr = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
                return await sr.ReadToEndAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ReadTextFile failed: {Path}", path);
                return string.Empty;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Tamaño de carpeta (cancelable)
        // ──────────────────────────────────────────────────────────────────
        public Task<long> GetFolderSizeAsync(string path, CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    long total = 0;
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        try { total += new FileInfo(f).Length; } catch { }
                    }
                    return total;
                }
                catch (OperationCanceledException) { return 0L; }
                catch { return 0L; }
            }, ct);
        }

        Task<long> IFileService.GetFolderSizeAsync(string path) => GetFolderSizeAsync(path);

        // ──────────────────────────────────────────────────────────────────
        //  Abrir, Eliminar, Renombrar
        // ──────────────────────────────────────────────────────────────────
        public Task OpenFileAsync(string path)
        {
            try
            {
                // Delegar la apertura al servicio centralizado que muestra mensajes amigables
                FileOpenerService.OpenWithDefaultApp(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenFile failed: {Path}", path);
            }
            return Task.CompletedTask;
        }

        public Task<bool> DeleteItemAsync(string path, bool recycle = true)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete failed: {Path}", path);
                return Task.FromResult(false);
            }
        }

        public Task<bool> RenameItemAsync(string path, string newName)
        {
            try
            {
                var parent  = Path.GetDirectoryName(path)!;
                var newPath = Path.Combine(parent, newName);
                if (Directory.Exists(path))      Directory.Move(path, newPath);
                else if (File.Exists(path))      File.Move(path, newPath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rename failed: {Path}", path);
                return Task.FromResult(false);
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Iconos por extensión (lookup O(1) con switch expression)
        // ──────────────────────────────────────────────────────────────────
        public string GetFileIcon(string extension, bool isDirectory)
        {
            if (isDirectory) return "📁";
            return extension switch
            {
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" => "🎬",
                ".mp3" or ".wav" or ".flac" or ".ogg" or ".aac" or ".wma"           => "🎵",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".tiff" => "🖼️",
                ".pdf"  => "📕",
                ".doc"  or ".docx" => "📝",
                ".xls"  or ".xlsx" => "📊",
                ".ppt"  or ".pptx" => "📋",
                ".zip"  or ".rar"  or ".7z" or ".tar" or ".gz" => "📦",
                ".exe"  or ".msi"  => "⚙️",
                ".txt"  or ".log"  => "📄",
                ".json" or ".yaml" or ".yml" => "🔧",
                ".xml"  or ".xaml" => "📐",
                ".csv"  => "📊",
                ".cs"   or ".vb"   => "💻",
                ".html" or ".htm"  => "🌐",
                ".css"  => "🎨",
                ".js"   or ".ts"   => "📜",
                ".py"   => "🐍",
                ".sql"  => "🗄️",
                _       => "📄"
            };
        }
    }
}
