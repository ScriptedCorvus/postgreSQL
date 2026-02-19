using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseClient.Core.ViewModels;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace DatabaseClient.App.ViewModels;

/// <summary>
/// ViewModel for data visualization charts.
/// Supports bar, line, pie, and scatter chart types from query result data.
/// </summary>
public partial class ChartTabViewModel : TabViewModelBase
{
    private DataTable? _sourceData;

    [ObservableProperty]
    private ObservableCollection<ISeries> _series = [];

    [ObservableProperty]
    private Axis[] _xAxes = [new Axis()];

    [ObservableProperty]
    private Axis[] _yAxes = [new Axis()];

    [ObservableProperty]
    private string[] _availableColumns = [];

    [ObservableProperty]
    private string _labelColumn = string.Empty;

    [ObservableProperty]
    private string _valueColumn = string.Empty;

    [ObservableProperty]
    private string _selectedChartType = "Bar";

    [ObservableProperty]
    private string _chartTitle = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _showLegend = true;

    public static string[] ChartTypes { get; } = ["Bar", "Line", "Pie", "Scatter", "Column"];

    public override string TabIconKind => "ChartBar";

    /// <summary>Delegate set by View to export chart as image.</summary>
    public Func<string, Task>? ExportChartDelegate { get; set; }

    public ChartTabViewModel()
    {
        Title = "Chart";
    }

    /// <summary>
    /// Loads data from a DataTable (typically from query results).
    /// </summary>
    public void LoadData(DataTable data, string sourceTitle = "Chart")
    {
        _sourceData = data;
        Title = $"Chart - {sourceTitle}";

        // Extract column names
        var cols = new List<string>();
        foreach (DataColumn col in data.Columns)
            cols.Add(col.ColumnName);
        AvailableColumns = cols.ToArray();

        // Auto-select first string-like column as labels, first numeric as values
        LabelColumn = cols.FirstOrDefault(c => IsStringColumn(data.Columns[c]!)) ?? cols.FirstOrDefault() ?? "";
        ValueColumn = cols.FirstOrDefault(c => IsNumericColumn(data.Columns[c]!)) ?? (cols.Count > 1 ? cols[1] : cols.FirstOrDefault() ?? "");

        BuildChart();
    }

    [RelayCommand]
    private void BuildChart()
    {
        if (_sourceData is null || string.IsNullOrEmpty(ValueColumn)) return;

        try
        {
            Series.Clear();

            switch (SelectedChartType)
            {
                case "Bar":
                    BuildBarChart();
                    break;
                case "Column":
                    BuildColumnChart();
                    break;
                case "Line":
                    BuildLineChart();
                    break;
                case "Pie":
                    BuildPieChart();
                    break;
                case "Scatter":
                    BuildScatterChart();
                    break;
            }

            StatusMessage = $"Chart built with {_sourceData.Rows.Count} data points.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error building chart: {ex.Message}";
        }
    }

    private void BuildBarChart()
    {
        var labels = GetLabels();
        var values = GetNumericValues(ValueColumn);

        var barSeries = new RowSeries<double>
        {
            Values = values,
            Name = ValueColumn,
            Stroke = null,
        };

        Series = new ObservableCollection<ISeries> { barSeries };
        XAxes = [new Axis { Labels = labels, LabelsRotation = 0 }];
        YAxes = [new Axis { Name = ValueColumn }];
    }

    private void BuildColumnChart()
    {
        var labels = GetLabels();
        var values = GetNumericValues(ValueColumn);

        var colSeries = new ColumnSeries<double>
        {
            Values = values,
            Name = ValueColumn,
            Stroke = null,
        };

        Series = new ObservableCollection<ISeries> { colSeries };
        XAxes = [new Axis { Labels = labels, LabelsRotation = 45 }];
        YAxes = [new Axis { Name = ValueColumn }];
    }

    private void BuildLineChart()
    {
        var labels = GetLabels();
        var values = GetNumericValues(ValueColumn);

        var lineSeries = new LineSeries<double>
        {
            Values = values,
            Name = ValueColumn,
            GeometrySize = 8,
            Fill = null,
        };

        Series = new ObservableCollection<ISeries> { lineSeries };
        XAxes = [new Axis { Labels = labels, LabelsRotation = 45 }];
        YAxes = [new Axis { Name = ValueColumn }];
    }

    private void BuildPieChart()
    {
        var labels = GetLabels();
        var values = GetNumericValues(ValueColumn);

        var pieSeries = new List<ISeries>();
        for (int i = 0; i < values.Length && i < labels.Length; i++)
        {
            pieSeries.Add(new PieSeries<double>
            {
                Values = [values[i]],
                Name = labels[i],
            });
        }

        Series = new ObservableCollection<ISeries>(pieSeries);
        XAxes = [new Axis()];
        YAxes = [new Axis()];
    }

    private void BuildScatterChart()
    {
        var values = GetNumericValues(ValueColumn);
        var points = values.Select((v, i) => new ObservablePoint(i, v)).ToArray();

        var scatterSeries = new ScatterSeries<ObservablePoint>
        {
            Values = points,
            Name = ValueColumn,
            GeometrySize = 10,
        };

        Series = new ObservableCollection<ISeries> { scatterSeries };
        XAxes = [new Axis { Name = string.IsNullOrEmpty(LabelColumn) ? "Index" : LabelColumn }];
        YAxes = [new Axis { Name = ValueColumn }];
    }

    [RelayCommand]
    private async Task ExportChart()
    {
        if (ExportChartDelegate is not null)
            await ExportChartDelegate("chart_export.png");
    }

    private string[] GetLabels()
    {
        if (_sourceData is null || string.IsNullOrEmpty(LabelColumn))
            return [];

        return _sourceData.AsEnumerable()
            .Select(r => r[LabelColumn]?.ToString() ?? "")
            .ToArray();
    }

    private double[] GetNumericValues(string columnName)
    {
        if (_sourceData is null || string.IsNullOrEmpty(columnName))
            return [];

        return _sourceData.AsEnumerable()
            .Select(r =>
            {
                var val = r[columnName];
                if (val == DBNull.Value || val is null) return 0.0;
                return Convert.ToDouble(val);
            })
            .ToArray();
    }

    private static bool IsNumericColumn(DataColumn col) =>
        col.DataType == typeof(int) || col.DataType == typeof(long) ||
        col.DataType == typeof(float) || col.DataType == typeof(double) ||
        col.DataType == typeof(decimal) || col.DataType == typeof(short) ||
        col.DataType == typeof(byte);

    private static bool IsStringColumn(DataColumn col) =>
        col.DataType == typeof(string);
}
