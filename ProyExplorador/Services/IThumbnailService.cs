using System.Windows.Media.Imaging;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Servicio de miniaturas para archivos de imagen, video e iconos de shell.
    /// </summary>
    public interface IThumbnailService : IDisposable
    {
        Task<BitmapSource?> GetThumbnailAsync(string fullPath, int pixelSize = 64, CancellationToken ct = default);
        void Invalidate(string fullPath);
        void Clear();
    }
}
