using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.WPF;

namespace BangSimulatorGui.Graphs
{
    public class MultiLineGraph : StackPanel, ISavableGraph
    {
        private string saveName;
        private Plot savePlot;

        public MultiLineGraph(string name, List<float[]> nums, string[] names, string saveName, int Max = 0)
        {
            this.saveName = saveName;

            var plot = new WpfPlot()
            {
                Height = 300,
                Width = 600,
                Margin = new Thickness(5)
            };

            savePlot = plot.Plot;

            if (Max > 0)
            {
                plot.Plot.Axes.SetLimitsY(0,Max);
            }

            plot.Plot.Title(name);

            plot.Plot.Legend.Alignment = Alignment.LowerLeft;

            for (int i = 0; i < names.Length; i++)
            {
                var sig = plot.Plot.Add.Signal(nums[i]);
                sig.LegendText = names[i];
            }

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

        public void Save(string paht)
        {
            var savePath = Path.Combine(paht, $"{saveName}.png");

            savePlot.SavePng(savePath, 1200, 900);
        }
    }

    
}