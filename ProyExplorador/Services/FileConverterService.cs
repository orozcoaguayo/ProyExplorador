using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;

namespace ProyExplorador.Services
{
    /// <summary>
    /// Servicio responsable de cargar, convertir y guardar archivos.
    /// Soporta TXT, JSON, XML y CSV (con versiones básicas de conversión).
    /// </summary>
    public class FileConverterService
    {
        /// <summary>
        /// Carga el contenido de un archivo de texto de forma asíncrona.
        /// </summary>
        public async Task<string> LoadFileAsync(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta)) throw new ArgumentNullException(nameof(ruta));
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            return await sr.ReadToEndAsync();
        }

        /// <summary>
        /// Convierte el contenido entre formatos soportados.
        /// extOrigen puede estar con o sin punto y en mayúsculas/minúsculas.
        /// formatoSalida espera TXT/JSON/XML/CSV.
        /// </summary>
        public Task<string> ConvertAsync(string contenido, string extOrigen, string formatoSalida)
        {
            return Task.Run(() => ConvertSync(contenido, extOrigen, formatoSalida));
        }

        private string ConvertSync(string contenido, string extOrigen, string formatoSalida)
        {
            var origen = NormalizarExtension(extOrigen);
            var destino = formatoSalida?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(formatoSalida));

            if (string.Equals(origen, destino, StringComparison.OrdinalIgnoreCase))
                return contenido;

            // Normalizar entrada a un objeto intermedio: lista de registros o texto libre
            if (origen == "TXT")
            {
                // Si origen es TXT, tratar como texto bruto; para convertir a estructurado intentaremos parsear líneas
                if (destino == "JSON") return TxtToJson(contenido);
                if (destino == "XML") return TxtToXml(contenido);
                if (destino == "CSV") return TxtToCsv(contenido);
            }
            if (origen == "JSON")
            {
                if (destino == "TXT") return JsonToTxt(contenido);
                if (destino == "XML") return JsonToXml(contenido);
                if (destino == "CSV") return JsonToCsv(contenido);
            }
            if (origen == "XML")
            {
                if (destino == "TXT") return XmlToTxt(contenido);
                if (destino == "JSON") return XmlToJson(contenido);
                if (destino == "CSV") return XmlToCsv(contenido);
            }
            if (origen == "CSV")
            {
                if (destino == "TXT") return CsvToTxt(contenido);
                if (destino == "JSON") return CsvToJson(contenido);
                if (destino == "XML") return CsvToXml(contenido);
            }

            throw new NotSupportedException($"Conversión {origen} → {destino} no soportada.");
        }

        /// <summary>
        /// Guarda contenido en ruta especificada de forma asíncrona.
        /// </summary>
        public async Task SaveFileAsync(string ruta, string contenido)
        {
            if (string.IsNullOrWhiteSpace(ruta)) throw new ArgumentNullException(nameof(ruta));
            using var fs = new FileStream(ruta, FileMode.Create, FileAccess.Write, FileShare.None);
            using var sw = new StreamWriter(fs, Encoding.UTF8);
            await sw.WriteAsync(contenido ?? string.Empty);
        }

        #region Helpers de conversión (implementaciones simples)

        private string NormalizarExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return "TXT";
            var e = ext.Trim().TrimStart('.').ToUpperInvariant();
            return e;
        }

        private string TxtToJson(string txt)
        {
            // Convertir líneas a array de strings
            var lines = SplitLines(txt);
            return JsonSerializer.Serialize(lines, new JsonSerializerOptions { WriteIndented = true });
        }

        private string TxtToXml(string txt)
        {
            var lines = SplitLines(txt);
            var doc = new XDocument(new XElement("root", lines.Select(l => new XElement("line", l))));
            return doc.ToString();
        }

        private string TxtToCsv(string txt)
        {
            // Simple: cada línea es una fila con una columna
            var lines = SplitLines(txt);
            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                sb.AppendLine(EscapeCsv(l));
            }
            return sb.ToString();
        }

        private string JsonToTxt(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var el in doc.RootElement.EnumerateArray()) sb.AppendLine(el.ToString());
                    return sb.ToString();
                }
                return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json), new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json; // fallback
            }
        }

        private string JsonToXml(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = new XElement("root");
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        root.Add(new XElement("item", item.ToString()));
                    }
                }
                else
                {
                    root.Add(new XElement("value", doc.RootElement.ToString()));
                }
                return new XDocument(root).ToString();
            }
            catch
            {
                return json;
            }
        }

        private string JsonToCsv(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var rows = new List<Dictionary<string, string>>();
                    var headers = new HashSet<string>();
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.Object)
                        {
                            var dict = new Dictionary<string, string>();
                            foreach (var prop in el.EnumerateObject())
                            {
                                dict[prop.Name] = prop.Value.ToString();
                                headers.Add(prop.Name);
                            }
                            rows.Add(dict);
                        }
                        else
                        {
                            headers.Add("value");
                            rows.Add(new Dictionary<string, string> { ["value"] = el.ToString() });
                        }
                    }
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Join(',', headers));
                    foreach (var r in rows)
                    {
                        var vals = headers.Select(h => EscapeCsv(r.TryGetValue(h, out var v) ? v : string.Empty));
                        sb.AppendLine(string.Join(',', vals));
                    }
                    return sb.ToString();
                }
                return json;
            }
            catch
            {
                return json;
            }
        }

        private string XmlToTxt(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                return doc.Root?.Value ?? xml;
            }
            catch
            {
                return xml;
            }
        }

        private string XmlToJson(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var items = doc.Root?.Elements().Select(e => new Dictionary<string, string> { [e.Name.LocalName] = e.Value });
                return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return xml;
            }
        }

        private string XmlToCsv(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var rows = doc.Root?.Elements();
                if (rows == null) return xml;
                var sb = new StringBuilder();
                foreach (var r in rows)
                {
                    sb.AppendLine(EscapeCsv(r.Value));
                }
                return sb.ToString();
            }
            catch
            {
                return xml;
            }
        }

        private string CsvToTxt(string csv)
        {
            // Simple: devolver tal cual
            return csv;
        }

        private string CsvToJson(string csv)
        {
            var lines = SplitLines(csv).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (!lines.Any()) return "[]";
            var headers = ParseCsvLine(lines[0]);
            var rows = new List<Dictionary<string, string>>();
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = ParseCsvLine(lines[i]);
                var dict = new Dictionary<string, string>();
                for (int c = 0; c < headers.Length; c++) dict[headers[c]] = c < cols.Length ? cols[c] : string.Empty;
                rows.Add(dict);
            }
            return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
        }

        private string CsvToXml(string csv)
        {
            var lines = SplitLines(csv).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (!lines.Any()) return string.Empty;
            var headers = ParseCsvLine(lines[0]);
            var root = new XElement("root");
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = ParseCsvLine(lines[i]);
                var item = new XElement("item");
                for (int c = 0; c < headers.Length; c++) item.Add(new XElement(headers[c], c < cols.Length ? cols[c] : string.Empty));
                root.Add(item);
            }
            return new XDocument(root).ToString();
        }

        #endregion

        #region CSV helpers

        private string[] SplitLines(string input)
        {
            return input?.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n') ?? Array.Empty<string>();
        }

        private string EscapeCsv(string input)
        {
            if (input == null) return string.Empty;
            if (input.Contains(',') || input.Contains('"') || input.Contains('\n'))
            {
                return '"' + input.Replace("\"", "\"\"") + '"';
            }
            return input;
        }

        private string[] ParseCsvLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return Array.Empty<string>();
            var parts = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
            parts.Add(sb.ToString());
            return parts.ToArray();
        }

        #endregion
    }
}