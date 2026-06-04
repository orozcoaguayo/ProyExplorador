using System.Data;

namespace ProyExplorador.Parsers
{
    public interface IDataParser
    {
        string[] SupportedExtensions { get; }
        string Name { get; }
        /// <summary>
        /// Parse raw text content into a DataTable.
        /// </summary>
        DataTable Parse(string content);
    }
}
