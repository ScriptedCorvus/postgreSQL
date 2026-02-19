using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DatabaseClient.App.Views;

public partial class TableDataTabView : UserControl
{
    private static readonly SolidColorBrush ModifiedBrush = new(Color.FromArgb(40, 255, 193, 7));   // yellow tint
    private static readonly SolidColorBrush DeletedBrush = new(Color.FromArgb(40, 229, 57, 53));    // red tint
    private static readonly SolidColorBrush NewRowBrush = new(Color.FromArgb(40, 76, 175, 80));     // green tint

    public TableDataTabView()
    {
        InitializeComponent();
    }

    private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (DataContext is ViewModels.TableDataTabViewModel vm && e.EditAction == DataGridEditAction.Commit)
        {
            var rowIndex = MainDataGrid.Items.IndexOf(e.Row.Item);
            vm.MarkRowModified(rowIndex);
            // Refresh row background after editing
            e.Row.Background = ModifiedBrush;
        }
    }

    private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (DataContext is not ViewModels.TableDataTabViewModel vm) return;
        if (e.Row.Item is not DataRowView rowView) return;

        int rowIndex = MainDataGrid.Items.IndexOf(rowView);

        if (vm.IsNewRow(rowView.Row))
        {
            e.Row.Background = NewRowBrush;
            e.Row.ToolTip = "New row (unsaved)";
        }
        else if (vm.DeletedRows.Contains(rowIndex))
        {
            e.Row.Background = DeletedBrush;
            e.Row.ToolTip = "Marked for deletion";
            e.Row.IsEnabled = false;
        }
        else if (vm.ModifiedRows.Contains(rowIndex))
        {
            e.Row.Background = ModifiedBrush;
            e.Row.ToolTip = "Modified (unsaved)";
        }
        else
        {
            e.Row.Background = Brushes.Transparent;
            e.Row.ToolTip = null;
            e.Row.IsEnabled = true;
        }
    }

    private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // Make column header clickable info (shows data type)
        if (e.PropertyDescriptor is System.ComponentModel.PropertyDescriptor pd)
        {
            e.Column.Header = e.PropertyName;
            e.Column.MinWidth = 60;

            // Replace auto-generated column with template column for NULL highlighting
            var templateCol = new DataGridTemplateColumn
            {
                Header = e.PropertyName,
                MinWidth = 60,
                SortMemberPath = e.PropertyName,
            };

            // Cell template (display mode)
            var cellTemplate = new DataTemplate();
            var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
            tbFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(e.PropertyName)
            {
                TargetNullValue = "NULL",
                StringFormat = "{0}"
            });
            tbFactory.SetValue(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2));
            tbFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            cellTemplate.VisualTree = tbFactory;
            templateCol.CellTemplate = cellTemplate;

            // Edit template (edit mode)
            var editTemplate = new DataTemplate();
            var editFactory = new FrameworkElementFactory(typeof(TextBox));
            editFactory.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(e.PropertyName)
            {
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                TargetNullValue = ""
            });
            editFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
            editTemplate.VisualTree = editFactory;
            templateCol.CellEditingTemplate = editTemplate;

            e.Column = templateCol;
        }
    }

    private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.TableDataTabViewModel vm && sender is ComboBox combo && combo.SelectedItem is int size)
        {
            _ = vm.ChangePageSizeCommand.ExecuteAsync(size);
        }
    }

    private void SetNull_Click(object sender, RoutedEventArgs e)
    {
        if (MainDataGrid.CurrentCell.Item is DataRowView rowView && MainDataGrid.CurrentCell.Column is not null)
        {
            var colName = MainDataGrid.CurrentCell.Column.Header?.ToString();
            if (colName is not null && rowView.Row.Table.Columns.Contains(colName))
            {
                rowView.Row[colName] = DBNull.Value;
                if (DataContext is ViewModels.TableDataTabViewModel vm)
                {
                    var rowIndex = MainDataGrid.Items.IndexOf(rowView);
                    vm.MarkRowModified(rowIndex);
                }
            }
        }
    }

    private void CopyAsInsert_Click(object sender, RoutedEventArgs e)
    {
        if (MainDataGrid.SelectedItem is not DataRowView rowView) return;
        var row = rowView.Row;
        var table = row.Table;

        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {table.TableName} (");

        var columns = new List<string>();
        var values = new List<string>();

        foreach (DataColumn col in table.Columns)
        {
            columns.Add(col.ColumnName);
            var val = row[col];
            if (val == DBNull.Value || val is null)
                values.Add("NULL");
            else if (val is string s)
                values.Add($"'{s.Replace("'", "''")}'");
            else if (val is DateTime dt)
                values.Add($"'{dt:yyyy-MM-dd HH:mm:ss}'");
            else if (val is bool b)
                values.Add(b ? "TRUE" : "FALSE");
            else
                values.Add(val.ToString() ?? "NULL");
        }

        sb.Append(string.Join(", ", columns));
        sb.Append(") VALUES (");
        sb.Append(string.Join(", ", values));
        sb.Append(");");

        Clipboard.SetText(sb.ToString());
    }
}
