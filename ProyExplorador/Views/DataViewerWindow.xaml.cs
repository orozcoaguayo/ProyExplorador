using ProyExplorador.Parsers;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using LiveChartsCore.Measure;

namespace ProyExplorador.Views
{
    public partial class DataViewerWindow : Window
    {
        private readonly IDataParser[] _parsers;
        private DataTable _table = new();
        private System.Text.Json.Nodes.JsonNode? _jsonRoot;
        private System.Text.Json.Nodes.JsonArray? _jsonArray;

        public DataViewerWindow()
        {
            InitializeComponent();

            // Reutilizar los parsers existentes
            _parsers = new IDataParser[]
            {
                new JsonParser(),
                new CsvParser(),
                new XmlParser(),
                new HtmlParser()
            };
        }

        private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Datos|*.json;*.csv;*.txt;*.xml;*.html;*.htm;*.dat" };
            if (dlg.ShowDialog() != true) return;
            var path = dlg.FileName;
            TxtFileName.Text = System.IO.Path.GetFileName(path);
            TxtLoadedAt.Text = DateTime.Now.ToString("g");

            try
            {
                var content = File.ReadAllText(path);
                var ext = Path.GetExtension(path).ToLowerInvariant();
                TxtDetectedType.Text = ext;

                // If JSON, keep parsed JsonNode for advanced charts
                _jsonRoot = null;
                _jsonArray = null;
                if (ext == ".json")
                {
                    try
                    {
                        var node = System.Text.Json.Nodes.JsonNode.Parse(content);
                        _jsonRoot = node;
                        if (node is System.Text.Json.Nodes.JsonArray ja) _jsonArray = ja;
                    }
                    catch { /* ignore parse here, parser will try */ }
                }

                // intentar parsear a tabla
                _table = new DataTable();
                foreach (var p in _parsers)
                {
                    if (p.SupportedExtensions.Contains(ext))
                    {
                        _table = p.Parse(content);
                        break;
                    }
                }

                TxtRecordsCount.Text = _table.Rows.Count.ToString();
                TxtContent.Text = content;

                // Mostrar contenido por defecto
                ShowContentView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnShowContent_Click(object sender, RoutedEventArgs e)
        {
            ShowContentView();
        }

        private void BtnShowTable_Click(object sender, RoutedEventArgs e)
        {
            ShowTableView();
        }

        private void BtnBarChart_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonArray != null) GenerateBarChartForJson();
            else GenerateBarChart();
        }

        private void BtnPieChart_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonArray != null) GeneratePieChartForJson();
            else GeneratePieChart();
        }

        private void ShowContentView()
        {
            TxtContent.Visibility = Visibility.Visible;
            GridTable.Visibility = Visibility.Collapsed;
            ChartArea.Visibility = Visibility.Collapsed;
        }

        private void ShowTableView()
        {
            GridTable.ItemsSource = _table?.DefaultView;
            GridTable.Visibility = Visibility.Visible;
            TxtContent.Visibility = Visibility.Collapsed;
            ChartArea.Visibility = Visibility.Collapsed;
        }

        private void ShowChartView()
        {
            TxtContent.Visibility = Visibility.Collapsed;
            GridTable.Visibility = Visibility.Collapsed;
            ChartArea.Visibility = Visibility.Visible;
            var chartHeader = FindName("ChartHeader") as FrameworkElement;
            if (chartHeader != null) chartHeader.Visibility = Visibility.Visible;
            var barChartObj = FindName("BarChart") as FrameworkElement;
            var pieChartObj = FindName("PieChart") as FrameworkElement;
            if (barChartObj != null) barChartObj.Visibility = Visibility.Collapsed;
            if (pieChartObj != null) pieChartObj.Visibility = Visibility.Collapsed;
        }

        private bool TryDetectColumns(out DataColumn? labelCol, out DataColumn? valueCol)
        {
            labelCol = null;
            valueCol = null;
            if (_table == null || _table.Columns.Count == 0) return false;
            // first string column
            labelCol = _table.Columns.Cast<DataColumn>().FirstOrDefault(c => c.DataType == typeof(string));

            // If all columns are strings (common when parsing JSON), detect numeric-like columns by sampling values
            // Find first column with a high ratio of parseable doubles
            DataColumn? numericCandidate = null;
            foreach (var c in _table.Columns.Cast<DataColumn>())
            {
                int total = 0; int ok = 0;
                foreach (DataRow r in _table.Rows)
                {
                    total++;
                    var v = r[c];
                    if (v == null || v == DBNull.Value) continue;
                    var s = v.ToString();
                    if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)) ok++;
                }
                if (total > 0 && ((double)ok / total) > 0.6)
                {
                    numericCandidate = c;
                    break;
                }
            }

            // use numericCandidate if column types are not numeric
            valueCol = _table.Columns.Cast<DataColumn>().FirstOrDefault(c =>
            {
                var tc = Type.GetTypeCode(c.DataType);
                return tc == TypeCode.Int16 || tc == TypeCode.Int32 || tc == TypeCode.Int64 || tc == TypeCode.Decimal || tc == TypeCode.Double || tc == TypeCode.Single || tc == TypeCode.UInt16 || tc == TypeCode.UInt32 || tc == TypeCode.UInt64;
            }) ?? numericCandidate;

            // If still null for label, try to use any column and convert ToString
            if (labelCol == null && _table.Columns.Count > 0)
                labelCol = _table.Columns[0];

            return valueCol != null && labelCol != null;
        }

        // JSON-specific helpers
        private string? GetJsonStringValue(System.Text.Json.Nodes.JsonObject obj, params string[] path)
        {
            System.Text.Json.Nodes.JsonNode? current = obj;
            foreach (var p in path)
            {
                if (current is System.Text.Json.Nodes.JsonObject jo && jo.TryGetPropertyValue(p, out var next)) current = next;
                else return null;
            }
            return current?.ToString();
        }

        private void GenerateBarChartForJson()
        {
            if (_jsonArray == null || _jsonArray.Count == 0)
            {
                MessageBox.Show("JSON no contiene array de objetos para graficar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Attempt: group by city and count active/inactive per city
            var cityKeys = new[] { "ubicacion", "ubicacion", "location", "ciudad", "city" };
            var cityList = _jsonArray.Select(n =>
            {
                if (n is System.Text.Json.Nodes.JsonObject jo)
                {
                    // try nested fields
                    foreach (var ckey in new[] { "ubicacion", "location", "ubicacion", "ubicacion" })
                    {
                        if (jo.TryGetPropertyValue(ckey, out var loc))
                        {
                            if (loc is System.Text.Json.Nodes.JsonObject locObj)
                            {
                                if (locObj.TryGetPropertyValue("city", out var city1)) return city1?.ToString() ?? string.Empty;
                                if (locObj.TryGetPropertyValue("ciudad", out var city2)) return city2?.ToString() ?? string.Empty;
                            }
                            else
                            {
                                // direct value
                                return loc?.ToString() ?? string.Empty;
                            }
                        }
                    }

                    // try direct city
                    if (jo.TryGetPropertyValue("city", out var cval)) return cval?.ToString() ?? string.Empty;
                    if (jo.TryGetPropertyValue("ciudad", out var cval2)) return cval2?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }).ToArray();

            var cities = cityList.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
            if (cities.Length == 0)
            {
                // fallback: simple count per top-level key 'location' missing
                MessageBox.Show("No se encontraron ciudades en el JSON para generar la gráfica por ciudad.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var activeCounts = new double[cities.Length];
            var inactiveCounts = new double[cities.Length];

            for (int i = 0; i < cities.Length; i++)
            {
                var city = cities[i];
                var rows = _jsonArray.Where(n => (n as System.Text.Json.Nodes.JsonObject) != null && ((n as System.Text.Json.Nodes.JsonObject)!).ToString().Contains(city));
                foreach (var r in rows)
                {
                    if (r is System.Text.Json.Nodes.JsonObject ro)
                    {
                        bool active = false;
                        if (ro.TryGetPropertyValue("activo", out var a)) bool.TryParse(a?.ToString(), out active);
                        else if (ro.TryGetPropertyValue("active", out var a2)) bool.TryParse(a2?.ToString(), out active);
                        if (active) activeCounts[i]++;
                        else inactiveCounts[i]++;
                    }
                }
            }

            var labels = cities;
            var series = new ISeries[]
            {
                new ColumnSeries<double> { Values = activeCounts, Name = "Activos" },
                new ColumnSeries<double> { Values = inactiveCounts, Name = "Inactivos" }
            };

            var barChart = FindName("BarChart");
            if (barChart != null)
            {
                dynamic bc = barChart;
                bc.Series = series;
                bc.XAxes = new Axis[] { new Axis { Labels = labels } };
            }

            // update header
            var chFile = FindName("ChartFileName") as System.Windows.Controls.TextBlock;
            var chRecords = FindName("ChartRecords") as System.Windows.Controls.TextBlock;
            var chCategory = FindName("ChartCategory") as System.Windows.Controls.TextBlock;
            var chValue = FindName("ChartValueCol") as System.Windows.Controls.TextBlock;
            if (chFile != null) chFile.Text = TxtFileName.Text;
            if (chRecords != null) chRecords.Text = TxtRecordsCount.Text;
            if (chCategory != null) chCategory.Text = "Ciudad";
            if (chValue != null) chValue.Text = "Activos / Inactivos";

            ShowChartView();
            var barObj = FindName("BarChart") as FrameworkElement; if (barObj != null) barObj.Visibility = Visibility.Visible;
            var pieObj = FindName("PieChart") as FrameworkElement; if (pieObj != null) pieObj.Visibility = Visibility.Collapsed;
        }

        private void GeneratePieChartForJson()
        {
            if (_jsonArray == null || _jsonArray.Count == 0)
            {
                MessageBox.Show("JSON no contiene array de objetos para graficar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Try by perfil_profesional.puesto
            var puestos = _jsonArray.Select(n =>
            {
                if (n is System.Text.Json.Nodes.JsonObject jo)
                {
                    if (jo.TryGetPropertyValue("perfil_profesional", out var pf) && pf is System.Text.Json.Nodes.JsonObject pfObj)
                    {
                        if (pfObj.TryGetPropertyValue("puesto", out var pu)) return pu?.ToString() ?? string.Empty;
                        if (pfObj.TryGetPropertyValue("position", out var pu2)) return pu2?.ToString() ?? string.Empty;
                    }
                    // fallback
                    if (jo.TryGetPropertyValue("puesto", out var p0)) return p0?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            if (puestos.Length > 0)
            {
                var groups = puestos.GroupBy(s => s).Select(g => new { Label = g.Key, Value = g.Count() }).ToArray();
                var series = groups.Select(g => (ISeries)new PieSeries<double> { Values = new double[] { g.Value }, Name = g.Label }).ToArray();
                var pieChart = FindName("PieChart");
                if (pieChart != null)
                {
                    dynamic pc = pieChart;
                    pc.Series = series;
                    try { pc.LegendPosition = LegendPosition.Right; } catch { }
                }

                var chFile2 = FindName("ChartFileName") as System.Windows.Controls.TextBlock;
                var chRecords2 = FindName("ChartRecords") as System.Windows.Controls.TextBlock;
                var chCategory2 = FindName("ChartCategory") as System.Windows.Controls.TextBlock;
                var chValue2 = FindName("ChartValueCol") as System.Windows.Controls.TextBlock;
                if (chFile2 != null) chFile2.Text = TxtFileName.Text;
                if (chRecords2 != null) chRecords2.Text = TxtRecordsCount.Text;
                if (chCategory2 != null) chCategory2.Text = "Puesto";
                if (chValue2 != null) chValue2.Text = "Conteo";

                ShowChartView();
                var pieObj = FindName("PieChart") as FrameworkElement; if (pieObj != null) pieObj.Visibility = Visibility.Visible;
                var barObj = FindName("BarChart") as FrameworkElement; if (barObj != null) barObj.Visibility = Visibility.Collapsed;
                return;
            }

            // Try interests array
            var interests = _jsonArray.SelectMany(n =>
            {
                if (n is System.Text.Json.Nodes.JsonObject jo)
                {
                    if (jo.TryGetPropertyValue("intereses", out var it) && it is System.Text.Json.Nodes.JsonArray ia)
                    {
                        return ia.Select(x => x?.ToString() ?? string.Empty);
                    }
                }
                return Enumerable.Empty<string>();
            }).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            if (interests.Length > 0)
            {
                var groups = interests.GroupBy(s => s).Select(g => new { Label = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).Take(12).ToArray();
                var series = groups.Select(g => (ISeries)new PieSeries<double> { Values = new double[] { g.Value }, Name = g.Label }).ToArray();
                var pieChart = FindName("PieChart");
                if (pieChart != null)
                {
                    dynamic pc = pieChart;
                    pc.Series = series;
                    try { pc.LegendPosition = LegendPosition.Right; } catch { }
                }

                var chFile2 = FindName("ChartFileName") as System.Windows.Controls.TextBlock;
                var chRecords2 = FindName("ChartRecords") as System.Windows.Controls.TextBlock;
                var chCategory2 = FindName("ChartCategory") as System.Windows.Controls.TextBlock;
                var chValue2 = FindName("ChartValueCol") as System.Windows.Controls.TextBlock;
                if (chFile2 != null) chFile2.Text = TxtFileName.Text;
                if (chRecords2 != null) chRecords2.Text = TxtRecordsCount.Text;
                if (chCategory2 != null) chCategory2.Text = "Interés";
                if (chValue2 != null) chValue2.Text = "Conteo";

                ShowChartView();
                var pieObj = FindName("PieChart") as FrameworkElement; if (pieObj != null) pieObj.Visibility = Visibility.Visible;
                var barObj = FindName("BarChart") as FrameworkElement; if (barObj != null) barObj.Visibility = Visibility.Collapsed;
                return;
            }

            MessageBox.Show("No se encontraron datos válidos en JSON para generar gráficas de pastel.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GenerateBarChart()
        {
            if (!TryDetectColumns(out var labelCol, out var valueCol))
            {
                MessageBox.Show(
                    "No se encontraron columnas válidas para generar gráficas.",
                    "Información",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Build labels and values
            var labels = _table.Rows.Cast<DataRow>().Select(r => (r[labelCol].ToString() ?? string.Empty)).ToArray();
            var values = _table.Rows.Cast<DataRow>().Select(r =>
            {
                try { return Convert.ToDouble(r[valueCol]); }
                catch { return 0.0; }
            }).ToArray();

            // Create a single ColumnSeries with values
            var series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = values,
                    Name = valueCol.ColumnName
                }
            };

            // Assign to chart via dynamic to avoid tight compile-time API binding
            var barChart = FindName("BarChart");
            if (barChart != null)
            {
                dynamic bc = barChart;
                bc.Series = series;
                bc.XAxes = new Axis[] { new Axis { Labels = labels } };
            }

            // Update header info via FindName
            var chFile = FindName("ChartFileName") as System.Windows.Controls.TextBlock;
            var chRecords = FindName("ChartRecords") as System.Windows.Controls.TextBlock;
            var chCategory = FindName("ChartCategory") as System.Windows.Controls.TextBlock;
            var chValue = FindName("ChartValueCol") as System.Windows.Controls.TextBlock;
            if (chFile != null) chFile.Text = TxtFileName.Text;
            if (chRecords != null) chRecords.Text = TxtRecordsCount.Text;
            if (chCategory != null) chCategory.Text = labelCol.ColumnName;
            if (chValue != null) chValue.Text = valueCol.ColumnName;

            // Show chart area
            ShowChartView();
            // set chart visibility
            var barObj = FindName("BarChart") as FrameworkElement;
            var pieObj = FindName("PieChart") as FrameworkElement;
            if (barObj != null) barObj.Visibility = Visibility.Visible;
            if (pieObj != null) pieObj.Visibility = Visibility.Collapsed;
        }

        private void GeneratePieChart()
        {
            if (!TryDetectColumns(out var labelCol, out var valueCol))
            {
                MessageBox.Show(
                    "No se encontraron columnas válidas para generar gráficas.",
                    "Información",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Aggregate values by label
            var groups = _table.Rows.Cast<DataRow>()
                .GroupBy(r => r[labelCol].ToString() ?? string.Empty)
                .Select(g => new { Label = g.Key, Value = g.Sum(r =>
                {
                    try { return Convert.ToDouble(r[valueCol]); }
                    catch { return 0.0; }
                }) })
                .Where(x => x.Value > 0)
                .ToArray();

            if (groups.Length == 0)
            {
                MessageBox.Show(
                    "No se encontraron columnas válidas para generar gráficas.",
                    "Información",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Build pie series (one slice per group)
            var series = groups.Select(g => (ISeries)new PieSeries<double>
            {
                Values = new double[] { g.Value },
                Name = g.Label
            }).ToArray();

            var pieChart = FindName("PieChart");
            if (pieChart != null)
            {
                dynamic pc = pieChart;
                pc.Series = series;
                try { pc.LegendPosition = LegendPosition.Right; } catch { }
            }

            // Update header info via FindName
            var chFile2 = FindName("ChartFileName") as System.Windows.Controls.TextBlock;
            var chRecords2 = FindName("ChartRecords") as System.Windows.Controls.TextBlock;
            var chCategory2 = FindName("ChartCategory") as System.Windows.Controls.TextBlock;
            var chValue2 = FindName("ChartValueCol") as System.Windows.Controls.TextBlock;
            if (chFile2 != null) chFile2.Text = TxtFileName.Text;
            if (chRecords2 != null) chRecords2.Text = TxtRecordsCount.Text;
            if (chCategory2 != null) chCategory2.Text = labelCol.ColumnName;
            if (chValue2 != null) chValue2.Text = valueCol.ColumnName;

            // Show chart area
            ShowChartView();
            // set chart visibility
            var barObj2 = FindName("BarChart") as FrameworkElement;
            var pieObj2 = FindName("PieChart") as FrameworkElement;
            if (pieObj2 != null) pieObj2.Visibility = Visibility.Visible;
            if (barObj2 != null) barObj2.Visibility = Visibility.Collapsed;
        }
    }
}
