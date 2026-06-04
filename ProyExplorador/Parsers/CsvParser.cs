using System.Data;
using System.IO;

namespace ProyExplorador.Parsers
{
    public class CsvParser : IDataParser
    {
        public string[] SupportedExtensions => new[] { ".csv", ".txt", ".dat" };
        public string Name => "CSV/TXT";

        public DataTable Parse(string content)
        {
            var table = new DataTable();
            using var sr = new StringReader(content);
            string? header = sr.ReadLine();
            if (header is null) return table;

            var cols = header.Split(',');
            foreach (var c in cols) table.Columns.Add(c.Trim());

            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var parts = line.Split(',');
                var row = table.NewRow();
                for (int i = 0; i < table.Columns.Count && i < parts.Length; i++) row[i] = parts[i].Trim();
                table.Rows.Add(row);
            }
            return table;
        }
    }
}
