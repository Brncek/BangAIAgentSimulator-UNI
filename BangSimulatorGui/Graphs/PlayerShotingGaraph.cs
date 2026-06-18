using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.WPF;

namespace BangSimulatorGui.Graphs
{
    public class PlayerShotingGaraph : StackPanel
    {
        public PlayerShotingGaraph(string name, int[] shoots, string[] columnNames, int maxCount)
        {
            var plot = new WpfPlot()
            {
                Height = 300,
                Width = 600,
                Margin = new Thickness(5)
            };

            var bars = shoots.Select((value, index) => new Bar
            {
                Position = index,
                Value = value
            });

            plot.Plot.Add.Bars(bars.ToList());

            plot.Plot.Title(name);

            plot.Plot.Axes.Bottom.TickGenerator =
                new ScottPlot.TickGenerators.NumericManual(
                    columnNames.Select((label, index) =>
                        new ScottPlot.Tick(index, label))
                    .ToArray());

            plot.Plot.Axes.SetLimitsY(0, maxCount);

            var border = new Border
            {
                Margin = new Thickness(10),
                Child = plot
            };

            this.Children.Add(border);

            Button saveBtn = new Button()
            {
                Margin = new Thickness(5)
            };
            saveBtn.Content = "Save";

            saveBtn.Click += (_, _) =>
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save Chart",
                    Filter = "PNG Image (*.png)|*.png",
                    DefaultExt = ".png",
                    AddExtension = true,
                    FileName = "chart.png"
                };

                if (dialog.ShowDialog() == true)
                {
                    string filePath = dialog.FileName;

                    plot.Plot.SavePng(filePath, 1200, 900);
                }
            };

            this.Children.Add(saveBtn);
        }
    }
}