using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProyExplorador.Parsers;
using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace ProyExplorador.ViewModels
{
    public partial class DataViewerViewModel : ViewModelBase
    {
        private readonly ILogger<DataViewerViewModel> _logger;
        private readonly IDataParser[] _parsers;

        [ObservableProperty] private string _viewTitle = "Visualizador y Analizador de Datos";
        [ObservableProperty] private string _viewSubtitle = "Carga, analiza y visualiza información desde múltiples formatos de archivo.";
        [ObservableProperty] private string _loadedFileName = string.Empty;
        [ObservableProperty] private string _fileContent = string.Empty;
        [ObservableProperty] private DataTable _table = new();
        [ObservableProperty] private bool _isContentVisible = false;
        [ObservableProperty] private bool _isTableVisible = false;
        [ObservableProperty] private bool _isChartVisible = false;
        [ObservableProperty] private string _detectedType = string.Empty;
        [ObservableProperty] private int _recordsCount = 0;
        [ObservableProperty] private string _loadedAt = string.Empty;

        public DataViewerViewModel(ILogger<DataViewerViewModel> logger, JsonParser json, CsvParser csv, XmlParser xml, HtmlParser html)
        {
            _logger = logger;
            _parsers = new IDataParser[] { json, csv, xml, html };
        }

        [RelayCommand]
        public async Task LoadFileAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Datos|*.json;*.csv;*.txt;*.xml;*.html;*.htm;*.dat" };
            if (dlg.ShowDialog() != true) return;
            var path = dlg.FileName;
            LoadedFileName = path;
            LoadedAt = DateTime.Now.ToString("g");

            try
            {
                FileContent = await File.ReadAllTextAsync(path);
                DetectedType = Path.GetExtension(path).ToLowerInvariant();

                // intentar parsear a tabla
                Table = new DataTable();
                foreach (var p in _parsers)
                {
                    if (Array.IndexOf(p.SupportedExtensions, DetectedType) >= 0)
                    {
                        Table = p.Parse(FileContent);
                        break;
                    }
                }

                RecordsCount = Table.Rows.Count;
                IsContentVisible = true;
                IsTableVisible = false;
                IsChartVisible = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando archivo");
                // mostrar mensaje amigable
            }
        }

        [RelayCommand]
        public void ShowContent()
        {
            IsContentVisible = true; IsTableVisible = false; IsChartVisible = false;
        }

        [RelayCommand]
        public void ShowTable()
        {
            IsContentVisible = false; IsTableVisible = true; IsChartVisible = false;
        }

        [RelayCommand]
        public void ShowBarChart()
        {
            IsContentVisible = false; IsTableVisible = false; IsChartVisible = true;
            // generar chart data (pendiente integración LiveCharts2)
        }

        [RelayCommand]
        public void ShowPieChart()
        {
            IsContentVisible = false; IsTableVisible = false; IsChartVisible = true;
            // generar chart data (pendiente integración LiveCharts2)
        }
    }
}
