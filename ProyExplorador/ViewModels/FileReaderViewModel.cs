using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using ProyExplorador.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using A = DocumentFormat.OpenXml.Drawing;

namespace ProyExplorador.ViewModels
{
    /// <summary>
    /// Lector de archivos de texto con soporte para TXT, JSON, XML y CSV.
    /// Expone el contenido en un formato apto para visualización en la UI.
    /// </summary>
    public partial class FileReaderViewModel : ViewModelBase
    {
        private readonly IFileService _fileService;
        private readonly INavigationService _navigation;
        private readonly MultimediaViewModel _multimediaVm;

        [ObservableProperty] private string  _filePath      = string.Empty;
        [ObservableProperty] private string  _fileName      = "Sin archivo";
        [ObservableProperty] private string  _fileExtension = string.Empty;
        [ObservableProperty] private string  _rawContent    = string.Empty;
        [ObservableProperty] private string  _fileType      = "none";  // txt | json | xml | csv | office
        [ObservableProperty] private string  _errorMessage  = string.Empty;
        [ObservableProperty] private long    _fileSize;
        [ObservableProperty] private int     _lineCount;
        [ObservableProperty] private bool    _hasContent;
        [ObservableProperty] private string  _prettyJson    = string.Empty;
        [ObservableProperty] private string  _imagePath     = string.Empty;

        /// <summary>Columnas del CSV para el DataGrid.</summary>
        public ObservableCollection<string>                    CsvHeaders { get; } = [];
        /// <summary>Filas del CSV — cada fila es un diccionario columna→valor.</summary>
        public ObservableCollection<Dictionary<string, string>> CsvRows   { get; } = [];
        /// <summary>Nodos del árbol JSON/XML.</summary>
        public ObservableCollection<TreeNode>  TreeNodes { get; } = [];

        public FileReaderViewModel(IFileService fileService, INavigationService navigation, MultimediaViewModel multimediaVm)
        {
            _fileService    = fileService;
            _navigation     = navigation;
            _multimediaVm   = multimediaVm;
        }

        // ── Abrir desde diálogo ───────────────────────────────────────────
        [RelayCommand]
        public async Task OpenFileDialogAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Abrir archivo",
                Filter = "Todos compatibles|*.txt;*.log;*.json;*.xml;*.csv;*.docx;*.xlsx;*.pptx;*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff|Texto|*.txt;*.log|JSON|*.json|XML|*.xml|CSV|*.csv|Word|*.docx|Excel|*.xlsx|PowerPoint|*.pptx|Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff|Todos|*.*"
            };

            if (dialog.ShowDialog() != true) return;
            await LoadFileAsync(dialog.FileName);
        }

        // ── Cargar archivo desde ruta ─────────────────────────────────────
        [RelayCommand]
        public async Task LoadFileAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            SetBusy(true, $"Leyendo {Path.GetFileName(path)}...");
            ErrorMessage = string.Empty;
            ClearCollections();

            try
            {
                FilePath = path;
                FileName = Path.GetFileName(path);
                FileExtension = Path.GetExtension(path).ToLowerInvariant();
                FileSize = new FileInfo(path).Length;

                // Office y PDF: delegar a la app nativa
                if (FileOpenerService.ShouldOpenNatively(FileExtension))
                {
                    FileType = "external";
                    await Task.Run(() => FileOpenerService.OpenWithDefaultApp(path));
                    return;
                }

                // Imágenes compatibles con el editor interno
                var imageExts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
                if (imageExts.Contains(FileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    // Delegar al editor de fotos (ventana dedicada) desde la capa que llama.
                    // Aquí simplemente preparamos el ViewModel para que la UI pueda reaccionar
                    // si fuera necesario; sin embargo, en el flujo normal el FileExplorer
                    // abrirá PhotoEditorWindow directamente. Mantener estado por compatibilidad.
                    FileType = "image";
                    ImagePath = path;
                    LineCount = 0;
                    HasContent = true;
                    return;
                }

                // Multimedia (vídeo / audio) — delegar al reproductor multimedia
                var mediaExts = new[] { ".mp4", ".avi", ".mkv", ".mov", ".mp3", ".wav", ".wma" };
                if (mediaExts.Contains(FileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    // Construir un FileItem mínimo y pedir al MultimediaViewModel que lo reproduzca
                    var fi = new ProyExplorador.Models.FileItem
                    {
                        Name = Path.GetFileName(path),
                        FullPath = path,
                        Extension = FileExtension
                    };
                    // Añadir y reproducir mediante el MultimediaViewModel
                    await _multimediaVm.PlayItemAsync(fi);
                    // Navegar a la vista Multimedia
                    _navigation.NavigateTo("Multimedia");
                    return;
                }

                // Sólo permitir ciertas extensiones como texto dentro del lector
                var allowedText = new[] { ".txt", ".cs", ".json", ".xml", ".html", ".htm", ".css", ".js", ".log", ".md", ".config" };
                if (!allowedText.Contains(FileExtension))
                {
                    FileType = "external";
                    await Task.Run(() => FileOpenerService.OpenWithDefaultApp(path));
                    return;
                }

                var content = await _fileService.ReadTextFileAsync(path);
                RawContent = content;
                LineCount = content.Split('\n').Length;
                HasContent = !string.IsNullOrEmpty(content);

                FileType = FileExtension switch
                {
                    ".json" => "json",
                    ".xml"  => "xml",
                    ".txt" or ".log" or ".md" or ".cs" or ".config" or ".html" or ".htm" or ".css" or ".js" => "txt",
                    _ => "txt"
                };

                switch (FileType)
                {
                    case "json": await ParseJsonAsync(content); break;
                    case "xml":  await ParseXmlAsync(content);  break;
                    case "csv":  await ParseCsvAsync(content);  break;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error al leer el archivo: {ex.Message}";
                FileType     = "error";
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ── Parser Office (docx / xlsx / pptx) ──────────────────────────────

        private static Task<string> ParseOfficeAsync(string path)
        {
            return Task.Run(() =>
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                return ext switch
                {
                    ".docx" => ReadDocx(path),
                    ".xlsx" => ReadXlsx(path),
                    ".pptx" => ReadPptx(path),
                    _       => string.Empty
                };
            });
        }

        private static string ReadDocx(string path)
        {
            var sb = new StringBuilder();
            using var doc = WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null) return string.Empty;
            foreach (var para in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                sb.AppendLine(para.InnerText);
            }
            return sb.ToString();
        }

        private static string ReadXlsx(string path)
        {
            var sb = new StringBuilder();
            using var doc = SpreadsheetDocument.Open(path, false);
            var workbook = doc.WorkbookPart;
            if (workbook is null) return string.Empty;

            var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable
                                        .Elements<SharedStringItem>()
                                        .Select(s => s.InnerText)
                                        .ToArray() ?? [];

            foreach (var sheet in workbook.Workbook.Descendants<Sheet>())
            {
                sb.AppendLine($"=== {sheet.Name} ===");
                if (workbook.GetPartById(sheet.Id!) is not WorksheetPart wsPart) continue;
                var rows = wsPart.Worksheet.Descendants<Row>();
                foreach (var row in rows)
                {
                    var cells = row.Descendants<Cell>()
                                   .Select(c =>
                                   {
                                       if (c.DataType?.Value == CellValues.SharedString &&
                                           int.TryParse(c.InnerText, out int idx) &&
                                           idx < sharedStrings.Length)
                                           return sharedStrings[idx];
                                       return c.InnerText;
                                   });
                    sb.AppendLine(string.Join("\t", cells));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string ReadPptx(string path)
        {
            var sb = new StringBuilder();
            using var doc = PresentationDocument.Open(path, false);
            var pres = doc.PresentationPart;
            if (pres is null) return string.Empty;

            int slideNum = 1;
            foreach (var slideId in pres.Presentation.SlideIdList?.OfType<SlideId>() ?? [])
            {
                if (pres.GetPartById(slideId.RelationshipId!) is not SlidePart slidePart) continue;
                sb.AppendLine($"--- Diapositiva {slideNum++} ---");
                var texts = slidePart.Slide.Descendants<A.Paragraph>()
                                     .Select(p => p.InnerText)
                                     .Where(t => !string.IsNullOrWhiteSpace(t));
                foreach (var t in texts) sb.AppendLine(t);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ── Parsers ───────────────────────────────────────────────────────

        private async Task ParseJsonAsync(string content)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Formatear JSON para mostrar en caja de texto
                    var doc     = JsonDocument.Parse(content);
                    PrettyJson  = JsonSerializer.Serialize(
                        doc.RootElement,
                        new JsonSerializerOptions { WriteIndented = true });

                    // Construir árbol visual
                    var root = new TreeNode { Label = "root", IsExpanded = true };
                    BuildJsonTree(doc.RootElement, root);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        TreeNodes.Clear();
                        TreeNodes.Add(root);
                    });
                }
                catch (JsonException ex)
                {
                    ErrorMessage = $"JSON inválido: {ex.Message}";
                    PrettyJson   = content;
                }
            });
        }

        private static void BuildJsonTree(JsonElement element, TreeNode parent)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        var node = new TreeNode
                        {
                            Label      = prop.Name,
                            Icon       = GetJsonIcon(prop.Value.ValueKind),
                            IsExpanded = false
                        };
                        BuildJsonTree(prop.Value, node);
                        parent.Children.Add(node);
                    }
                    break;

                case JsonValueKind.Array:
                    int idx = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var node = new TreeNode
                        {
                            Label = $"[{idx++}]",
                            Icon  = GetJsonIcon(item.ValueKind)
                        };
                        BuildJsonTree(item, node);
                        parent.Children.Add(node);
                    }
                    break;

                default:
                    parent.Value = element.ToString();
                    parent.Icon  = GetJsonIcon(element.ValueKind);
                    break;
            }
        }

        private static string GetJsonIcon(JsonValueKind kind) => kind switch
        {
            JsonValueKind.Object  => "{}",
            JsonValueKind.Array   => "[]",
            JsonValueKind.String  => "\"\"",
            JsonValueKind.Number  => "123",
            JsonValueKind.True    => "✓",
            JsonValueKind.False   => "✗",
            JsonValueKind.Null    => "∅",
            _                    => "?"
        };

        private async Task ParseXmlAsync(string content)
        {
            await Task.Run(() =>
            {
                try
                {
                    var doc  = XDocument.Parse(content);
                    var root = new TreeNode { Label = doc.Root?.Name.LocalName ?? "root", IsExpanded = true };
                    if (doc.Root is not null)
                        BuildXmlTree(doc.Root, root);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        TreeNodes.Clear();
                        TreeNodes.Add(root);
                    });
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"XML inválido: {ex.Message}";
                }
            });
        }

        private static void BuildXmlTree(XElement element, TreeNode parent)
        {
            // Atributos del nodo
            foreach (var attr in element.Attributes())
                parent.Children.Add(new TreeNode
                {
                    Label = $"@{attr.Name.LocalName}",
                    Value = attr.Value,
                    Icon  = "📌"
                });

            // Hijos elemento
            foreach (var child in element.Elements())
            {
                var node = new TreeNode
                {
                    Label      = child.Name.LocalName,
                    Icon       = child.HasElements ? "🏷️" : "📄",
                    IsExpanded = false
                };

                if (!child.HasElements && !string.IsNullOrWhiteSpace(child.Value))
                    node.Value = child.Value.Trim();

                BuildXmlTree(child, node);
                parent.Children.Add(node);
            }
        }

        private async Task ParseCsvAsync(string content)
        {
            await Task.Run(() =>
            {
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return;

                var headers = SplitCsvLine(lines[0]);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CsvHeaders.Clear();
                    foreach (var h in headers) CsvHeaders.Add(h.Trim('"', ' '));

                    CsvRows.Clear();
                    for (int i = 1; i < lines.Length && i <= 1000; i++)
                    {
                        var cells = SplitCsvLine(lines[i]);
                        var row   = new Dictionary<string, string>();
                        for (int j = 0; j < headers.Length; j++)
                        {
                            var key = headers[j].Trim('"', ' ');
                            row[key] = j < cells.Length ? cells[j].Trim('"', ' ') : string.Empty;
                        }
                        CsvRows.Add(row);
                    }
                });
            });
        }

        private static string[] SplitCsvLine(string line)
        {
            // Soporte básico para campos con comas dentro de comillas
            var result    = new List<string>();
            var current   = new System.Text.StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            result.Add(current.ToString());
            return [.. result];
        }

        [RelayCommand]
        private void CloseFile()
        {
            FilePath      = string.Empty;
            FileName      = "Sin archivo";
            FileExtension = string.Empty;
            RawContent    = string.Empty;
            PrettyJson    = string.Empty;
            ImagePath     = string.Empty;
            FileType      = "none";
            LineCount     = 0;
            HasContent    = false;
            ErrorMessage  = string.Empty;
            ClearCollections();
        }

        private void ClearCollections()
        {
            CsvHeaders.Clear();
            CsvRows.Clear();
            TreeNodes.Clear();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Nodo del árbol JSON/XML
    // ─────────────────────────────────────────────────────────────────────
    public partial class TreeNode : ObservableObject
    {
        [ObservableProperty] private string _label      = string.Empty;
        [ObservableProperty] private string _value      = string.Empty;
        [ObservableProperty] private string _icon       = "📄";
        [ObservableProperty] private bool   _isExpanded;

        public ObservableCollection<TreeNode> Children { get; } = [];

        /// <summary>Texto completo que se muestra en la celda del árbol.</summary>
        public string DisplayText => string.IsNullOrEmpty(Value)
            ? Label
            : $"{Label}  :  {Value}";
    }
}
