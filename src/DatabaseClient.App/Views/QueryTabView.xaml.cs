using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DatabaseClient.App.Services;
using DatabaseClient.App.ViewModels;
using DatabaseClient.Core.Services;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace DatabaseClient.App.Views;

/// <summary>
/// Code-behind for QueryTabView. Bridges AvalonEdit (non-bindable) with the ViewModel.
/// Also handles NULL highlighting in the DataGrid and SQL autocompletion.
/// </summary>
public partial class QueryTabView : UserControl
{
    private readonly SqlCompletionProvider _completionProvider = new();
    private CompletionWindow? _completionWindow;
    public QueryTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Ctrl+Enter to execute
        SqlEditor.InputBindings.Add(new KeyBinding(
            new RelayCommandBridge(() =>
            {
                if (DataContext is QueryTabViewModel vm && vm.ExecuteQueryCommand.CanExecute(null))
                {
                    vm.SelectedText = SqlEditor.SelectedText;
                    vm.ExecuteQueryCommand.Execute(null);
                }
            }),
            Key.Enter, ModifierKeys.Control));

        // F5 to execute (alternative)
        SqlEditor.InputBindings.Add(new KeyBinding(
            new RelayCommandBridge(() =>
            {
                if (DataContext is QueryTabViewModel vm && vm.ExecuteQueryCommand.CanExecute(null))
                {
                    vm.SelectedText = SqlEditor.SelectedText;
                    vm.ExecuteQueryCommand.Execute(null);
                }
            }),
            Key.F5, ModifierKeys.None));

        // Ctrl+Space for manual autocompletion
        SqlEditor.InputBindings.Add(new KeyBinding(
            new RelayCommandBridge(ShowCompletionWindow),
            Key.Space, ModifierKeys.Control));

        // Auto-trigger completion on text input
        SqlEditor.TextArea.TextEntered += OnTextEntered;
    }

    /// <summary>Sets the schema cache for autocompletion.</summary>
    public void SetSchemaCache(SchemaCache cache) => _completionProvider.SetSchemaCache(cache);

    private void ShowCompletionWindow()
    {
        if (_completionWindow is not null) return;

        var completions = _completionProvider.GetCompletions(SqlEditor.Document, SqlEditor.CaretOffset);
        if (completions.Count == 0) return;

        _completionWindow = new CompletionWindow(SqlEditor.TextArea);
        foreach (var item in completions)
            _completionWindow.CompletionList.CompletionData.Add(item);

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    private void OnTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // Auto-trigger completion after typing a dot (table.column) or a letter after space
        if (e.Text == ".")
        {
            ShowCompletionWindow();
        }
        else if (e.Text.Length == 1 && char.IsLetter(e.Text[0]) && _completionWindow is null)
        {
            // Check if we just started a new word (previous char is space, newline, or start)
            var offset = SqlEditor.CaretOffset;
            if (offset >= 2)
            {
                var prevChar = SqlEditor.Document.GetCharAt(offset - 2);
                if (prevChar is ' ' or '\t' or '\n' or '\r' or '(' or ',' or ';')
                {
                    ShowCompletionWindow();
                }
            }
            else if (offset == 1)
            {
                ShowCompletionWindow();
            }
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is QueryTabViewModel oldVm)
        {
            SqlEditor.TextChanged -= OnSqlEditorTextChanged;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            oldVm.SchemaCacheRefreshed -= OnSchemaCacheRefreshed;
        }

        if (e.NewValue is QueryTabViewModel newVm)
        {
            SqlEditor.Text = newVm.SqlText;
            SqlEditor.TextChanged += OnSqlEditorTextChanged;
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            newVm.SchemaCacheRefreshed += OnSchemaCacheRefreshed;
            
            // Set initial schema cache
            SetSchemaCache(newVm.SchemaCache);

            // Wire parameter dialog delegate
            newVm.ShowParameterDialog = parameters =>
            {
                var dialog = new ParameterDialog(parameters);
                dialog.Owner = Window.GetWindow(this);
                return dialog.ShowDialog() == true ? dialog.ParameterValues : null;
            };
            
            // Sync SelectedText before every execution
            newVm.ExecuteQueryCommand.CanExecuteChanged += (_, _) =>
            {
                newVm.SelectedText = SqlEditor.SelectedText;
            };
        }
    }

    private void OnSchemaCacheRefreshed(object? sender, EventArgs e)
    {
        if (DataContext is QueryTabViewModel vm)
            SetSchemaCache(vm.SchemaCache);
    }

    private void OnSqlEditorTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is QueryTabViewModel vm)
        {
            vm.SqlText = SqlEditor.Text;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryTabViewModel.SqlText) && DataContext is QueryTabViewModel vm)
        {
            if (SqlEditor.Text != vm.SqlText)
                SqlEditor.Text = vm.SqlText;
        }
    }

    /// <summary>
    /// Applies NULL cell styling when auto-generating columns.
    /// </summary>
    private void ResultsGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // Apply a custom cell style that highlights NULL values
        if (e.Column is DataGridTextColumn textColumn)
        {
            var style = new Style(typeof(DataGridCell));

            // Trigger: when cell value is DBNull, show "NULL" in gray italic
            var nullTrigger = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding(e.PropertyName)
                {
                    TargetNullValue = DBNull.Value
                },
                Value = string.Empty // DBNull converts to empty string in display
            };
            // We'll handle this via the CellTemplate approach instead
            style.Setters.Add(new Setter(DataGridCell.ToolTipProperty, e.PropertyName));
            textColumn.CellStyle = style;

            // Replace with a template column for NULL display
            var templateColumn = new DataGridTemplateColumn
            {
                Header = e.Column.Header,
                SortMemberPath = e.PropertyName,
                CanUserSort = true,
                Width = e.Column.Width
            };

            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(e.PropertyName)
            {
                TargetNullValue = "NULL",
                FallbackValue = "NULL"
            });

            // Style the TextBlock for NULL values
            var nullStyleTrigger = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding(e.PropertyName),
                Value = null
            };
            nullStyleTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Gray));
            nullStyleTrigger.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));

            var triggerStyle = new Style(typeof(TextBlock));
            triggerStyle.Triggers.Add(nullStyleTrigger);
            factory.SetValue(FrameworkElement.StyleProperty, triggerStyle);

            factory.SetValue(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2));
            factory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            template.VisualTree = factory;
            templateColumn.CellTemplate = template;

            e.Column = templateColumn;
        }
    }

    /// <summary>
    /// Minimal ICommand wrapper for key bindings in the editor.
    /// </summary>
    private class RelayCommandBridge(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }

    /// <summary>
    /// Navigates the SQL editor to the error's line when double-clicking an error entry.
    /// </summary>
    private void ErrorGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is QueryErrorEntry entry && entry.Line.HasValue)
        {
            var lineNumber = entry.Line.Value;
            if (lineNumber > 0 && lineNumber <= SqlEditor.Document.LineCount)
            {
                var line = SqlEditor.Document.GetLineByNumber(lineNumber);
                SqlEditor.CaretOffset = line.Offset;
                SqlEditor.ScrollToLine(lineNumber);
                SqlEditor.TextArea.Focus();

                // Select the entire line for visibility
                SqlEditor.Select(line.Offset, line.Length);
            }
        }
    }
}
