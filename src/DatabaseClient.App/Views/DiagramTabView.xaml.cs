using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DatabaseClient.Core.Models;
using Microsoft.Win32;

namespace DatabaseClient.App.Views;

public partial class DiagramTabView : UserControl
{
    private bool _isPanning;
    private Point _panStart;
    private bool _isDraggingTable;
    private DiagramTable? _draggedTable;
    private Point _dragOffset;

    public DiagramTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ViewModels.DiagramTabViewModel vm)
        {
            vm.ExportDiagramDelegate = ExportDiagramAsImage;
            vm.Tables.CollectionChanged += (_, _) => RedrawDiagram();
            vm.Relationships.CollectionChanged += (_, _) => RedrawDiagram();
        }
    }

    /// <summary>Redraws all table boxes and relationship lines on the canvas.</summary>
    private void RedrawDiagram()
    {
        if (DataContext is not ViewModels.DiagramTabViewModel vm) return;

        DiagramCanvas.Children.Clear();

        // Draw relationship lines first (behind tables)
        foreach (var rel in vm.Relationships)
        {
            var sourceTable = vm.Tables.FirstOrDefault(t => t.Name == rel.SourceTable);
            var targetTable = vm.Tables.FirstOrDefault(t => t.Name == rel.TargetTable);
            if (sourceTable is null || targetTable is null) continue;

            var line = new Line
            {
                X1 = sourceTable.X + sourceTable.Width / 2,
                Y1 = sourceTable.Y + sourceTable.Height / 2,
                X2 = targetTable.X + targetTable.Width / 2,
                Y2 = targetTable.Y + targetTable.Height / 2,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                StrokeThickness = 1.5,
                StrokeDashArray = [4, 2],
            };
            DiagramCanvas.Children.Add(line);

            // Cardinality label at midpoint
            var midX = (line.X1 + line.X2) / 2;
            var midY = (line.Y1 + line.Y2) / 2;
            var label = new TextBlock
            {
                Text = rel.Cardinality,
                FontSize = 10,
                Foreground = Brushes.Gray,
                Background = new SolidColorBrush(Color.FromArgb(200, 250, 250, 250)),
                Padding = new Thickness(2),
            };
            Canvas.SetLeft(label, midX - 10);
            Canvas.SetTop(label, midY - 8);
            DiagramCanvas.Children.Add(label);
        }

        // Draw tables
        foreach (var table in vm.Tables)
        {
            DrawTable(table);
        }
    }

    private void DrawTable(DiagramTable table)
    {
        var tableGroup = new Border
        {
            Width = table.Width,
            Background = Brushes.White,
            BorderBrush = table.IsSelected ? new SolidColorBrush(Color.FromRgb(33, 150, 243)) : Brushes.Gray,
            BorderThickness = table.IsSelected ? new Thickness(2) : new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 2,
                Opacity = 0.3,
                BlurRadius = 6,
            },
            Tag = table,
            Cursor = Cursors.Hand,
        };

        var stack = new StackPanel();

        // Header
        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(3, 3, 0, 0),
        };
        var headerText = new TextBlock
        {
            Text = table.Name,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            FontSize = 12,
        };
        header.Child = headerText;
        stack.Children.Add(header);

        // Columns
        foreach (var col in table.Columns)
        {
            var colPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(6, 2, 6, 2),
            };

            // PK/FK icon
            if (col.IsPrimaryKey)
            {
                colPanel.Children.Add(new TextBlock
                {
                    Text = "🔑",
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }
            else if (col.IsForeignKey)
            {
                colPanel.Children.Add(new TextBlock
                {
                    Text = "🔗",
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            // Column name
            colPanel.Children.Add(new TextBlock
            {
                Text = col.Name,
                FontSize = 11,
                FontWeight = col.IsPrimaryKey ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            });

            // Data type
            colPanel.Children.Add(new TextBlock
            {
                Text = $"  {col.DataType}",
                FontSize = 10,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
            });

            if (col.IsNullable)
            {
                colPanel.Children.Add(new TextBlock
                {
                    Text = " ?",
                    FontSize = 10,
                    Foreground = Brushes.Orange,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            stack.Children.Add(colPanel);
        }

        tableGroup.Child = stack;

        // Position on canvas
        Canvas.SetLeft(tableGroup, table.X);
        Canvas.SetTop(tableGroup, table.Y);

        // Mouse event handlers for dragging
        tableGroup.MouseLeftButtonDown += TableBox_MouseLeftButtonDown;
        tableGroup.MouseMove += TableBox_MouseMove;
        tableGroup.MouseLeftButtonUp += TableBox_MouseLeftButtonUp;

        DiagramCanvas.Children.Add(tableGroup);
    }

    #region Table Dragging

    private void TableBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not DiagramTable table) return;
        if (DataContext is not ViewModels.DiagramTabViewModel vm) return;

        // Select table
        foreach (var t in vm.Tables) t.IsSelected = false;
        table.IsSelected = true;
        vm.SelectedTable = table;

        // Start drag
        _isDraggingTable = true;
        _draggedTable = table;
        var pos = e.GetPosition(DiagramCanvas);
        _dragOffset = new Point(pos.X - table.X, pos.Y - table.Y);
        border.CaptureMouse();
        e.Handled = true;

        RedrawDiagram();
    }

    private void TableBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingTable || _draggedTable is null) return;
        if (sender is not Border border) return;

        var pos = e.GetPosition(DiagramCanvas);
        _draggedTable.X = Math.Max(0, pos.X - _dragOffset.X);
        _draggedTable.Y = Math.Max(0, pos.Y - _dragOffset.Y);

        Canvas.SetLeft(border, _draggedTable.X);
        Canvas.SetTop(border, _draggedTable.Y);

        // Redraw relationship lines (they depend on table positions)
        RedrawDiagram();
    }

    private void TableBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingTable = false;
        _draggedTable = null;
        if (sender is Border border)
            border.ReleaseMouseCapture();
        e.Handled = true;
    }

    #endregion

    #region Canvas Pan & Zoom

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not ViewModels.DiagramTabViewModel vm) return;

        double delta = e.Delta > 0 ? 0.1 : -0.1;
        vm.ZoomLevel = Math.Clamp(vm.ZoomLevel + delta, 0.3, 3.0);
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingTable) return;
        _isPanning = true;
        _panStart = e.GetPosition(this);
        ((UIElement)sender).CaptureMouse();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || _isDraggingTable) return;
        if (DataContext is not ViewModels.DiagramTabViewModel vm) return;

        var pos = e.GetPosition(this);
        vm.PanX += pos.X - _panStart.X;
        vm.PanY += pos.Y - _panStart.Y;
        _panStart = pos;
    }

    #endregion

    private async Task ExportDiagramAsImage(string defaultFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            FileName = defaultFileName,
            DefaultExt = ".png"
        };

        if (dlg.ShowDialog() != true) return;

        // Render the canvas to a bitmap
        var bounds = VisualTreeHelper.GetDescendantBounds(DiagramCanvas);
        if (bounds.IsEmpty) return;

        var renderBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)(bounds.Width + 100), (int)(bounds.Height + 100), 96, 96,
            PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.White, null, new Rect(new Size(bounds.Width + 100, bounds.Height + 100)));
            var brush = new VisualBrush(DiagramCanvas)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
            };
            ctx.DrawRectangle(brush, null, new Rect(new Point(50, 50), bounds.Size));
        }
        renderBitmap.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderBitmap));

        using var stream = File.OpenWrite(dlg.FileName);
        encoder.Save(stream);

        await Task.CompletedTask;
    }
}
