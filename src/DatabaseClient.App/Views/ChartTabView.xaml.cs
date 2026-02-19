using System.IO;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore.SkiaSharpView.WPF;
using Microsoft.Win32;
using SkiaSharp;

namespace DatabaseClient.App.Views;

public partial class ChartTabView : UserControl
{
    public ChartTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ViewModels.ChartTabViewModel vm)
        {
            vm.ExportChartDelegate = ExportChartAsImage;
        }
    }

    private async Task ExportChartAsImage(string defaultFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg|SVG Vector|*.svg",
            FileName = defaultFileName,
            DefaultExt = ".png"
        };

        if (dlg.ShowDialog() != true) return;

        await Task.Run(() =>
        {
            // Use SkiaSharp to render chart to image
            var info = new SKImageInfo(1920, 1080);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            using var image = surface.Snapshot();
            using var data = dlg.FilterIndex switch
            {
                2 => image.Encode(SKEncodedImageFormat.Jpeg, 95),
                _ => image.Encode(SKEncodedImageFormat.Png, 100)
            };

            using var stream = File.OpenWrite(dlg.FileName);
            data.SaveTo(stream);
        });
    }
}
