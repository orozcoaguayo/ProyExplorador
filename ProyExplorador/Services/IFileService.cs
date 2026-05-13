using ProyExplorador.Models;
using System.Runtime.CompilerServices;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Contrato del servicio de acceso al sistema de archivos.
    /// </summary>
    public interface IFileService
    {
        // Listado incremental — preferir sobre GetItemsAsync en vistas con muchos archivos
        IAsyncEnumerable<FileItem> EnumerateItemsAsync(string path, bool showHidden = false, CancellationToken ct = default);

        // Compatibilidad con código existente
        Task<IEnumerable<FileItem>> GetItemsAsync(string path);

        Task<IEnumerable<FileItem>> SearchAsync(string rootPath, string query, string? extension = null);
        Task<IEnumerable<DriveItem>> GetDrivesAsync();
        Task<IEnumerable<RecentFile>> GetRecentFilesAsync(int maxCount = 20);
        Task<string> ReadTextFileAsync(string path);
        Task<long> GetFolderSizeAsync(string path);
        string GetFileIcon(string extension, bool isDirectory);
        Task<bool> DeleteItemAsync(string path, bool recycle = true);
        Task<bool> RenameItemAsync(string path, string newName);
        Task OpenFileAsync(string path);
    }
}
