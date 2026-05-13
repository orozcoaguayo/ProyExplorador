using System.IO;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Servicio de limpieza del sistema: temporales, caché y papelera.
    /// </summary>
    public class CleanupService
    {
        private static readonly string[] _tempPaths =
        [
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
        ];

        /// <summary>Calcula el espacio que se liberaría (en bytes) antes de limpiar.</summary>
        public async Task<long> CalculateTempSizeAsync()
        {
            return await Task.Run(() =>
            {
                long total = 0;
                foreach (var dir in _tempPaths)
                {
                    if (!Directory.Exists(dir)) continue;
                    try
                    {
                        total += Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                                          .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
                    }
                    catch { /* sin permiso */ }
                }
                return total;
            });
        }

        /// <summary>Elimina archivos temporales y reporta espacio liberado mediante progreso.</summary>
        public async Task<long> CleanTempFilesAsync(IProgress<(int percent, string currentFile)>? progress = null)
        {
            return await Task.Run(() =>
            {
                long freed = 0;
                var allFiles = new List<string>();

                foreach (var dir in _tempPaths)
                {
                    if (!Directory.Exists(dir)) continue;
                    try { allFiles.AddRange(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)); }
                    catch { }
                }

                int total   = allFiles.Count;
                int current = 0;

                foreach (var file in allFiles)
                {
                    current++;
                    try
                    {
                        var info = new FileInfo(file);
                        freed += info.Length;
                        info.Delete();
                    }
                    catch { }

                    if (total > 0)
                    {
                        int pct = (int)((double)current / total * 100);
                        progress?.Report((pct, Path.GetFileName(file)));
                    }
                }

                return freed;
            });
        }

        /// <summary>Vacía la papelera de reciclaje.</summary>
        public Task EmptyRecycleBinAsync() => Task.Run(() =>
        {
            try { NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null!, 0x0001 | 0x0004); }
            catch { }
        });
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("Shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
    }
}
