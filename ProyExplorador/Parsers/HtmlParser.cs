using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProyExplorador.Parsers
{
    public class HtmlParser : IDataParser
    {
        public string[] SupportedExtensions => new[] { ".html", ".htm" };
        public string Name => "HTML";

        public DataTable Parse(string content)
        {
            var table = new DataTable();
            try
            {
                // Simple HTML table extractor (no external libs required)
                var lower = content.ToLowerInvariant();
                var start = lower.IndexOf("<table");
                if (start >= 0)
                {
                    var end = lower.IndexOf("</table>", start);
                    if (end > start)
                    {
                        var tableHtml = content.Substring(start, end - start + 8);
                        // extract rows using regex
                        var rowMatches = Regex.Matches(tableHtml, "<tr[\\s\\S]*?>[\\s\\S]*?<\\/tr>", RegexOptions.IgnoreCase);
                        bool headersAdded = false;
                        foreach (Match rm in rowMatches.Cast<Match>())
                        {
                            var rowHtml = rm.Value;
                            var thMatches = Regex.Matches(rowHtml, "<th[\\s\\S]*?>\\s*([\\s\\S]*?)\\s*<\\/th>", RegexOptions.IgnoreCase);
                            var tdMatches = Regex.Matches(rowHtml, "<td[\\s\\S]*?>\\s*([\\s\\S]*?)\\s*<\\/td>", RegexOptions.IgnoreCase);
                            if (thMatches.Count > 0 && !headersAdded)
                            {
                                foreach (Match h in thMatches.Cast<Match>())
                                {
                                    var txt = Regex.Replace(h.Groups[1].Value, "<.*?>", string.Empty).Trim();
                                    table.Columns.Add(string.IsNullOrEmpty(txt) ? "Col" + table.Columns.Count : txt);
                                }
                                headersAdded = true;
                                continue;
                            }
                            if (tdMatches.Count > 0)
                            {
                                if (!headersAdded)
                                {
                                    for (int i = 0; i < tdMatches.Count; i++) table.Columns.Add("Col" + i);
                                    headersAdded = true;
                                }
                                var row = table.NewRow();
                                for (int i = 0; i < table.Columns.Count && i < tdMatches.Count; i++)
                                    row[i] = Regex.Replace(tdMatches[i].Groups[1].Value, "<.*?>", string.Empty).Trim();
                                table.Rows.Add(row);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return table;
        }
    }
}
