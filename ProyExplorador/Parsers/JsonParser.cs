using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProyExplorador.Parsers
{
    public class JsonParser : IDataParser
    {
        public string[] SupportedExtensions => new[] { ".json" };
        public string Name => "JSON";

        public DataTable Parse(string content)
        {
            var table = new DataTable();
            try
            {
                var node = JsonNode.Parse(content);
                if (node is JsonArray arr)
                {
                    // array de objetos
                    foreach (var item in arr)
                    {
                        if (item is JsonObject obj)
                        {
                            foreach (var kv in obj)
                            {
                                if (!table.Columns.Contains(kv.Key)) table.Columns.Add(kv.Key);
                            }
                        }
                    }

                    foreach (var item in arr)
                    {
                        var row = table.NewRow();
                        if (item is JsonObject obj)
                        {
                            foreach (var c in table.Columns.Cast<DataColumn>())
                            {
                                if (obj.TryGetPropertyValue(c.ColumnName, out var v)) row[c.ColumnName] = v?.ToString() ?? string.Empty;
                                else row[c.ColumnName] = string.Empty;
                            }
                        }
                        table.Rows.Add(row);
                    }
                }
                else if (node is JsonObject single)
                {
                    // objeto único: crear columnas y una fila
                    foreach (var kv in single)
                    {
                        if (!table.Columns.Contains(kv.Key)) table.Columns.Add(kv.Key);
                    }
                    var row = table.NewRow();
                    foreach (var c in table.Columns.Cast<DataColumn>()) row[c.ColumnName] = single[c.ColumnName]?.ToString() ?? string.Empty;
                    table.Rows.Add(row);
                }
            }
            catch
            {
                // en caso de error devolver tabla vacía
            }
            return table;
        }
    }
}
