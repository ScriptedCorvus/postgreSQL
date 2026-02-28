using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

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
        if (DataContext is not ViewModels.TableDataTabViewModel vm) return;
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not DataRowView rowView) return;

        var colName = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(colName)) return;
        if (!rowView.Row.Table.Columns.Contains(colName)) return;

        // Compare old value with what the user typed
        var oldValue = rowView.Row[colName];
        var newText = (e.EditingElement as System.Windows.Controls.TextBox)?.Text;

        string oldText;
        if (oldValue == DBNull.Value || oldValue is null)
            oldText = "";
        else
            oldText = oldValue.ToString() ?? "";

        // If value didn't change, skip marking
        if (string.Equals(newText?.Trim(), oldText.Trim(), StringComparison.Ordinal)) return;

        var rowIndex = MainDataGrid.Items.IndexOf(e.Row.Item);
        vm.MarkRowModified(rowIndex);
        // Refresh row background after editing
        e.Row.Background = ModifiedBrush;
    }

    private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        // Show row number in header for easy row selection (click header to select entire row)
        e.Row.Header = (e.Row.GetIndex() + 1).ToString();

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
        if (e.PropertyDescriptor is System.ComponentModel.PropertyDescriptor pd)
        {
            var dataColumn = GetDataColumn(e.PropertyName);

            // Replace auto-generated column with template column for NULL highlighting
            var templateCol = new DataGridTemplateColumn
            {
                Header = BuildColumnHeader(e.PropertyName, dataColumn, pd.PropertyType),
                MinWidth = 60,
                SortMemberPath = e.PropertyName,
                IsReadOnly = dataColumn?.ReadOnly == true,
                Width = new DataGridLength(GetInitialColumnWidth(e.PropertyName, dataColumn, pd.PropertyType), DataGridLengthUnitType.Pixel)
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

            if (!templateCol.IsReadOnly)
            {
                // Edit template (edit mode)
                var editTemplate = new DataTemplate();
                var editFactory = new FrameworkElementFactory(typeof(TextBox));
                editFactory.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(e.PropertyName)
                {
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                    TargetNullValue = ""
                });
                editFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
                editTemplate.VisualTree = editFactory;
                templateCol.CellEditingTemplate = editTemplate;
            }

            e.Column = templateCol;
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var cell = FindParent<DataGridCell>(source);
        if (cell is null || cell.IsReadOnly) return;

        MainDataGrid.CurrentCell = new DataGridCellInfo(cell);
        MainDataGrid.SelectedItem = cell.DataContext;
        MainDataGrid.BeginEdit();
        e.Handled = true;
    }

    private async void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is not ViewModels.TableDataTabViewModel vm) return;
        if (MainDataGrid.CurrentCell.Item is not DataRowView rowView) return;

        e.Handled = true;

        MainDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MainDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var rowIndex = MainDataGrid.Items.IndexOf(rowView);
        if (rowIndex < 0) return;

        vm.MarkRowModified(rowIndex);
        await vm.ApplyRowChangeAsync(rowIndex);
    }

    private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.TableDataTabViewModel vm && sender is ComboBox combo && combo.SelectedItem is int size)
        {
            _ = vm.ChangePageSizeCommand.ExecuteAsync(size);
        }
    }

    private async void ApplyChanges_Click(object sender, RoutedEventArgs e)
    {
        // Commit any active DataGrid edit BEFORE running the Apply command
        MainDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MainDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (DataContext is ViewModels.TableDataTabViewModel vm && vm.ApplyChangesCommand.CanExecute(null))
        {
            await vm.ApplyChangesCommand.ExecuteAsync(null);
        }
    }

    private void SetNull_Click(object sender, RoutedEventArgs e)
    {
        // Commit any active edit first
        MainDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MainDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (MainDataGrid.CurrentCell.Item is DataRowView rowView && MainDataGrid.CurrentCell.Column is not null)
        {
            var colName = MainDataGrid.CurrentCell.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(colName)) return;
            if (!rowView.Row.Table.Columns.Contains(colName)) return;

            var column = rowView.Row.Table.Columns[colName];
            if (column.ReadOnly)
            {
                MessageBox.Show($"Column '{colName}' is read only.", "Read-only column", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            rowView.Row[colName] = DBNull.Value;
            if (DataContext is ViewModels.TableDataTabViewModel vm)
            {
                var rowIndex = MainDataGrid.Items.IndexOf(rowView);
                vm.MarkRowModified(rowIndex);
            }
        }
    }

    private DataColumn? GetDataColumn(string columnName)
    {
        if (MainDataGrid.Items.Count == 0) return null;
        if (MainDataGrid.Items[0] is not DataRowView firstRow) return null;
        return firstRow.Row.Table.Columns.Contains(columnName) ? firstRow.Row.Table.Columns[columnName] : null;
    }

    private static FrameworkElement BuildColumnHeader(string name, DataColumn? column, Type propertyType)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(2, 0, 2, 0)
        };

        panel.Children.Add(new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220
        });

        var metaRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 0) };

        var icon = new PackIcon
        {
            Kind = GetDataTypeIcon(propertyType),
            Width = 12,
            Height = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
        };
        DockPanel.SetDock(icon, Dock.Left);
        metaRow.Children.Add(icon);

        var sizeText = new TextBlock
        {
            Text = GetSizeLabel(column, propertyType),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(sizeText, Dock.Right);
        metaRow.Children.Add(sizeText);

        panel.Children.Add(metaRow);
        return panel;
    }

    private static PackIconKind GetDataTypeIcon(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return PackIconKind.AlphabeticalVariant;
        if (t == typeof(bool)) return PackIconKind.ToggleSwitchOutline;
        if (t == typeof(DateTime)) return PackIconKind.CalendarClock;
        if (t == typeof(Guid)) return PackIconKind.PoundBoxOutline;
        if (t == typeof(byte[])) return PackIconKind.FileDocumentOutline;
        if (t.IsEnum || t == typeof(short) || t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return PackIconKind.Numeric;
        return PackIconKind.TableColumn;
    }

    private static string GetSizeLabel(DataColumn? column, Type type)
    {
        if (column is not null && column.DataType == typeof(string))
            return column.MaxLength > 0 ? $"{column.MaxLength}" : "∞";

        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(short)) return "2B";
        if (t == typeof(int) || t == typeof(float)) return "4B";
        if (t == typeof(long) || t == typeof(double) || t == typeof(DateTime)) return "8B";
        if (t == typeof(decimal)) return "16B";
        if (t == typeof(bool)) return "1B";
        if (t == typeof(Guid)) return "16B";
        if (t == typeof(byte[])) return "BLOB";
        return "-";
    }

    private static double GetInitialColumnWidth(string name, DataColumn? column, Type type)
    {
        var width = 110d;

        if (column?.DataType == typeof(string))
        {
            if (column.MaxLength > 0)
                width = Math.Min(256d, Math.Max(110d, column.MaxLength * 7d));
            else
                width = 220d;
        }
        else if (type == typeof(DateTime) || type == typeof(DateTime?))
        {
            width = 170d;
        }
        else if (type == typeof(bool) || type == typeof(bool?))
        {
            width = 90d;
        }
        else if (name.Length > 16)
        {
            width = 180d;
        }

        return Math.Min(256d, Math.Max(80d, width));
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current != null)
        {
            if (current is T typed) return typed;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
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
