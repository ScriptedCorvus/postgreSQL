using System.Windows;
using DatabaseClient.Core.Services;
using MahApps.Metro.Controls;

namespace DatabaseClient.App.Views;

/// <summary>
/// Dialog for collecting parameter values before executing a parameterized query.
/// </summary>
public partial class ParameterDialog : MetroWindow
{
    private readonly IReadOnlyList<SqlParameterInfo> _parameters;

    /// <summary>
    /// Gets the parameter values entered by the user after the dialog is closed with Execute.
    /// Key = parameter name (without prefix), Value = typed value.
    /// </summary>
    public Dictionary<string, object?> ParameterValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ParameterDialog(IReadOnlyList<SqlParameterInfo> parameters)
    {
        InitializeComponent();
        _parameters = parameters;

        // Populate the ComboBox column with SqlParameterType values
        var typeColumn = (System.Windows.Controls.DataGridComboBoxColumn)ParametersGrid.Columns[1];
        typeColumn.ItemsSource = Enum.GetValues<SqlParameterType>();

        ParametersGrid.ItemsSource = parameters;
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        // Commit any pending edits
        ParametersGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        foreach (var param in _parameters)
        {
            var value = ConvertParameterValue(param);
            ParameterValues[param.Prefix + param.Name] = value;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static object? ConvertParameterValue(SqlParameterInfo param)
    {
        if (string.IsNullOrEmpty(param.Value) && param.InferredType != SqlParameterType.String)
            return DBNull.Value;

        if (param.Value?.Equals("NULL", StringComparison.OrdinalIgnoreCase) == true)
            return DBNull.Value;

        return param.InferredType switch
        {
            SqlParameterType.Integer => int.TryParse(param.Value, out var i) ? i : (object)DBNull.Value,
            SqlParameterType.Decimal => decimal.TryParse(param.Value, out var d) ? d : (object)DBNull.Value,
            SqlParameterType.Boolean => param.Value?.ToLowerInvariant() switch
            {
                "true" or "1" or "yes" => true,
                "false" or "0" or "no" => false,
                _ => (object)DBNull.Value
            },
            SqlParameterType.DateTime => DateTime.TryParse(param.Value, out var dt) ? dt : (object)DBNull.Value,
            SqlParameterType.Null => DBNull.Value,
            _ => param.Value ?? (object)DBNull.Value
        };
    }
}
