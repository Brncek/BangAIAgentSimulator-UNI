using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace BangSimulatorGui;

public partial class RankingWindow : Window
{
    private Process? worker;
    private bool closed;

    public RankingWindow()
    {
        InitializeComponent();
        string python = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Python", "pythoncore-3.15-64", "python.exe");
        if (File.Exists(python))
            PythonExecutable.Text = python;

        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string agents = Path.Combine(folder.FullName, "agents");
            if (Directory.Exists(agents))
            {
                CheckpointFolder.Text = agents;
                break;
            }
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true)
            CheckpointFolder.Text = dialog.FolderName;
    }

    private void BrowsePython_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) == true)
            PythonExecutable.Text = dialog.FileName;
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        string folder = CheckpointFolder.Text.Trim();
        string python = PythonExecutable.Text.Trim();
        bool uncertainty = Method.SelectedIndex == 0;
        if (!Directory.Exists(folder) || python.Length == 0 ||
            !int.TryParse(GameLimit.Text, out int games) || games <= 0)
        {
            Status.Text = "Choose a checkpoint folder, a Python executable, and a positive game limit.";
            return;
        }

        double precision = 5;
        if (uncertainty && (!double.TryParse(Precision.Text, out precision) || !(precision > 0 && precision < 100)))
        {
            Status.Text = "Enter a precision between 0 and 100 percentage points.";
            return;
        }

        string executable = Path.Combine(AppContext.BaseDirectory, "BangSimulatorRanking.exe");
        if (!File.Exists(executable))
        {
            Status.Text = "The ranking worker is missing. Build the GUI project again.";
            return;
        }

        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[]
        {
            Path.GetFullPath(folder), "--json",
            "--method", (string)((ComboBoxItem)Method.SelectedItem).Tag,
            "--pool", (string)((ComboBoxItem)Pool.SelectedItem).Tag,
            "--games", games.ToString(CultureInfo.InvariantCulture),
            "--batch", Math.Min(games, 50).ToString(CultureInfo.InvariantCulture),
            "--precision", precision.ToString(CultureInfo.InvariantCulture),
            "--python", python
        })
            start.ArgumentList.Add(argument);

        SettingsPanel.IsEnabled = false;
        RunButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        Leaderboard.ItemsSource = null;
        Status.Text = "Starting ranking…";

        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the ranking worker.");
            worker = process;
            try
            {
                var stderr = process.StandardError.ReadToEndAsync();
                RankingProgress? latest = null;
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    latest = JsonSerializer.Deserialize<RankingProgress>(line)
                        ?? throw new InvalidDataException("The ranking worker returned an empty update.");
                    if (!closed)
                    {
                        Leaderboard.ItemsSource = latest.Rows;
                        Status.Text = Describe(latest);
                    }
                }

                await process.WaitForExitAsync();
                string diagnostics = await stderr;
                if (!closed)
                {
                    string result = process.ExitCode switch
                    {
                        130 => "Cancelled.",
                        0 when uncertainty && latest?.TargetReached == true => "Precision reached.",
                        0 when uncertainty => "Game limit reached before the precision target.",
                        0 => "Ranking complete.",
                        _ => diagnostics.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .LastOrDefault() ?? $"Ranking failed with exit code {process.ExitCode}."
                    };
                    Status.Text = latest is null ? result : $"{Describe(latest)}  {result}";
                }
            }
            finally
            {
                StopWorker(process);
                worker = null;
            }
        }
        catch (Exception error)
        {
            if (!closed)
                Status.Text = error.Message;
        }
        finally
        {
            SettingsPanel.IsEnabled = true;
            RunButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (worker is not { } process)
            return;

        CancelButton.IsEnabled = false;
        Status.Text = "Cancelling after the current match…";
        try
        {
            await process.StandardInput.WriteLineAsync("cancel");
            await process.StandardInput.FlushAsync();
        }
        catch (IOException)
        {
            // The worker can exit before it receives the cancellation request.
        }
        catch (InvalidOperationException)
        {
            // The run may already have disposed the process.
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        closed = true;
        if (worker is not null)
            StopWorker(worker);

        base.OnClosed(e);
    }

    private static void StopWorker(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The worker can exit between checking its state and stopping it.
        }
    }

    private static string Describe(RankingProgress progress) =>
        $"{progress.GamesPerCheckpoint:N0} games per checkpoint · {progress.Matches:N0} matches · " +
        $"{progress.Draws:N0} draws · {TimeSpan.FromSeconds(progress.ElapsedSeconds):hh\\:mm\\:ss}";

    private sealed record RankingProgress(
        int GamesPerCheckpoint, long Matches, long Draws, double ElapsedSeconds,
        bool TargetReached, RankingRow[] Rows);

    private sealed record RankingRow(
        string Role, int Rank, string Name, int Wins, int Games, int Draws, string Score, string Interval);
}
