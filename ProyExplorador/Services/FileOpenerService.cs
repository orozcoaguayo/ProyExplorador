using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Decide si un archivo se abre con la aplicación nativa de Windows
    /// (Office, PDF, imágenes, vídeos…) o se delega al visor interno.
    /// </summary>
    public static class FileOpenerService
    {
        // ── Extensiones que deben abrirse siempre con app nativa (Office y PDF) ──────────
        private static readonly HashSet<string> NativeExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Office – Excel/Word/PowerPoint
                ".xlsx", ".xls", ".docx", ".doc", ".pptx", ".ppt",
                // PDF
                ".pdf"
            };

        /// <summary>
        /// Retorna <c>true</c> si la extensión debe abrirse con su app nativa.
        /// </summary>
        public static bool ShouldOpenNatively(string extension) =>
            NativeExtensions.Contains(extension);

        /// <summary>
        /// Abre el archivo con la aplicación predeterminada de Windows.
        /// Muestra un <see cref="MessageBox"/> descriptivo ante cualquier error.
        /// </summary>
        /// <returns><c>true</c> si el proceso se lanzó correctamente.</returns>
        public static bool OpenWithDefaultApp(string filePath)
        {
            // ── Validaciones previas ──────────────────────────────────────
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ShowError("Ruta inválida", "La ruta del archivo está vacía o no es válida.");
                return false;
            }

            if (!File.Exists(filePath))
            {
                ShowError("Archivo no encontrado",
                    $"No se encontró el archivo:\n{filePath}\n\nVerifica que no haya sido movido o eliminado.");
                return false;
            }

            // ── Apertura con shell de Windows ─────────────────────────────
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = filePath,
                    UseShellExecute = true   // Windows resuelve la app asociada
                });
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1155)
            {
                // ERROR_NO_ASSOCIATION — no hay aplicación asociada
                ShowError("Sin aplicación asociada",
                    $"Windows no encontró ninguna aplicación para abrir:\n{Path.GetFileName(filePath)}\n\n" +
                    "Instala una aplicación compatible o elige 'Abrir con' en el explorador de archivos.");
                return false;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                // ERROR_ACCESS_DENIED
                ShowError("Acceso denegado",
                    $"No tienes permisos suficientes para abrir:\n{filePath}");
                return false;
            }
            catch (Exception ex)
            {
                ShowError("Error al abrir archivo",
                    $"No se pudo abrir el archivo:\n{Path.GetFileName(filePath)}\n\nDetalle: {ex.Message}");
                return false;
            }
        }

        // ── Helper ────────────────────────────────────────────────────────
        private static void ShowError(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
