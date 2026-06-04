using System.Data;
using System.IO;
using System.Xml;

namespace ProyExplorador.Parsers
{
    public class XmlParser : IDataParser
    {
        public string[] SupportedExtensions => new[] { ".xml" };
        public string Name => "XML";

        public DataTable Parse(string content)
        {
            var ds = new DataSet();
            try
            {
                using var sr = new StringReader(content);
                ds.ReadXml(sr);
                if (ds.Tables.Count > 0) return ds.Tables[0];
            }
            catch
            {
            }
            return new DataTable();
        }
    }
}
