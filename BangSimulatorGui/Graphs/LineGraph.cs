using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScottPlot.WPF;

namespace BangSimulatorGui.Graphs
{
    public class LineGraph : StackPanel
    {
        public LineGraph(string name, float[] nums)
        {
            var plot = new WpfPlot()
            {
                Height = 300,
                Width = 600,
                Margin = new Thickness(5)
            };

            plot.Plot.Title(name);

            plot.Plot.Add.Signal(nums);

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