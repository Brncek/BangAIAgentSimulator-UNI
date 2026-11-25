using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection.Emit;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using BangSimulatorGui.Graphs;
using BangSimulatorGui.Model;
using BangSimulatorLib.Game;
using BangSimulatorLib.Statistics;
using Microsoft.Win32;
using ScottPlot;

namespace BangSimulatorGui
{
    public partial class MainWindow : Window
    {
        private List<AgentMenuItem> agentSettings = [];

        private LinkedList<string> terminal = [];
        private int maxTerminalLines = 1000;

        private Thread? simThread;
        private GameEngine? lastGame;
        private List<GameResoult> lastResults = [];

        private bool stopSim = false;
        private Mutex stopSimMutex = new Mutex();

        private bool profileIngMog = false;
        private bool inWait = false;

        private Mutex terminalMutex = new();

        private List<ISavableGraph> savableGraphs = [];
        private RankingWindow? rankingWindow;

        private SimulatorSettings settings = new();

        public MainWindow()
        {
            InitializeComponent();
            InitAgentMenu();
            SetProgress(0, 0);
        }

        private void InitAgentMenu()
        {
            List<int> selected = [1, 2, 2, 3, 0];


            for (int i = 0; i < 7; i++)
            {
                var agentMenu = new AgentMenuItem($"Agent {i + 1}");
                agentSettings.Add( agentMenu );
                PlayersSettings.Children.Add(agentMenu);
            
                var separator =  new Separator();
                separator.Margin = new Thickness(2);

                if (selected.Count > 0)
                {
                    var index = GlobalRnd.Rnd.Next(selected.Count);
                    agentMenu.PreselectRole(selected[index]);
                    selected.RemoveAt(index);
                }

                PlayersSettings.Children.Add(separator);
            }
        }

        private void Ranking_Click(object sender, RoutedEventArgs e)
        {
            if (rankingWindow is not null)
            {
                if (rankingWindow.WindowState == WindowState.Minimized)
                    rankingWindow.WindowState = WindowState.Normal;

                rankingWindow.Activate();
                return;
            }

            rankingWindow = new RankingWindow { Owner = this };
            rankingWindow.Closed += (_, _) => rankingWindow = null;
            rankingWindow.Show();
        }

        private void Step_Click(object sender, RoutedEventArgs e)
        {
            //TODO: Step_Click
        }

        private void Stepping_CLick(object sender, RoutedEventArgs e)
        {
            //TODO: Stepping_CLick
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            List<Player?> players = [];

            if (!LoadDataFromFormular() || !LoadPlayers(ref players, false)) { return; }

            Start_BT.Visibility = Visibility.Collapsed;
            Stepping_BT.Visibility = Visibility.Collapsed;
            Stop_BT.Visibility = Visibility.Visible;
            Continue_BT.Visibility = Visibility.Visible;
            Continue_BT.IsEnabled = false;
            agentSettings.ForEach(a => a.DisableEdit(false,false) );

            SetProgress(0, settings.Rounds);

            lastGame = new GameEngine(players.ToArray()!);
            lastResults = [];

            var agentsSetInfo = GetPlayerSettings();

            Action saveA = () =>
            {
                for(int i = 0; i < players.Count(); i++)
                {
                    players[i]!.Agent.SetAutoSavePath(agentsSetInfo[i].SavePath);
                }
            };

            RunGames(settings.Rounds, settings.AvgLen, saveA);
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {

            if (!LoadDataFromFormular()) { return; }

            inWait = false;
            Stop_BT.IsEnabled = true;
            Continue_BT.IsEnabled = false;
            agentSettings.ForEach(a => a.DisableEdit(false, false));

            var agents = lastGame!.Players.Select(p => p.Agent).ToArray();
            var agentsSetInfo = GetPlayerSettings();

            for (int i  = 0; i < agents.Count(); i++)
            {
                agents[i].SetEval(agentsSetInfo[i].EvalMode);
                agents[i].SetAutoSavePath(agentsSetInfo[i].SavePath);
            }

            SetProgress(lastResults.Count, settings.Rounds + lastResults.Count);

            RunGames(settings.Rounds, settings.AvgLen);
        }


        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (inWait)
            {
                Reset();
                return;
            }

            stopSimMutex.WaitOne();

            stopSim = true;

            stopSimMutex.ReleaseMutex();

            Stop_BT.IsEnabled = false;
        }

        private void DoneActions()
        {
            inWait = true;

            Stop_BT.IsEnabled = true;
            Continue_BT.IsEnabled = true;

            agentSettings.ForEach(a => a.DisableEdit(false, true));
        }

        private void Reset()
        {
            lastResults = [];
            lastGame = null;

            stopSim = false;
            inWait = false;

            Stop_BT.IsEnabled = true;
            Continue_BT.IsEnabled = true;
            
            Stepping_BT.Visibility = Visibility.Visible;
            Start_BT.Visibility = Visibility.Visible;

            Step_BT.Visibility = Visibility.Collapsed;
            Continue_BT.Visibility = Visibility.Collapsed;
            Stop_BT.Visibility = Visibility.Collapsed;

            agentSettings.ForEach(a => a.DisableEdit(true, true));
        }

        private void SetProgress(int done, int total)
        {
            var val = 0.0;

            if (done >  0)
            {
                val = done / (double)total * 100;
            }

            App.Current.Dispatcher.Invoke(new Action(() =>
            {
                ProgressText.Text = $"Progress: {done}/{total}";
                ProgressBar.Value = val;
            }));
        }

        private void WriteLnToTerminal(string text)
        {

            terminalMutex.WaitOne();

            if (maxTerminalLines < terminal.Count)
            {
                terminal.RemoveLast();
            }

            terminal.AddFirst($"> {text}");


            StringBuilder stringBuilder = new StringBuilder();

            foreach (var line  in terminal)
            {
                stringBuilder.AppendLine(line);
            }

            var guiText = stringBuilder.ToString();

            terminalMutex.ReleaseMutex();

            App.Current.Dispatcher.Invoke(new Action(() =>
            {

                TerminalBlock.Text = guiText;

            }));
        }

        private bool LoadDataFromFormular()
        {
            if (!int.TryParse(MemSizeBox.Text, out var memSize))
            {
                MessageBox.Show("Memory size has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (memSize <= 0)
            {
                memSize = 0;
                MemSizeBox.Text = "0";
            }

            settings.MemSize = memSize;

            bool useMemory = memSize > 0;


            if (!int.TryParse(MemLimitBox.Text, out var memLimit))
            {
                MessageBox.Show("Memory limit has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (memLimit <= 0)
            {
                memLimit = 0;
                MemLimitBox.Text = "0";
            }

            settings.MemLimit = memLimit;

            if (memLimit < memSize)
            {
                MessageBox.Show("Memory limit has to bigger or equal as memory size", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(RoundsCountBox.Text, out var roundsCount))
            {
                MessageBox.Show("Round count has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (roundsCount <= 0)
            {
                MessageBox.Show("Round count has to be bigger than 0", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            settings.Rounds = roundsCount;

            Deck.SetDeckMemory(memSize, memLimit);

            if (!int.TryParse(RndSeedBox.Text, out var rndSeed))
            {
                MessageBox.Show("Seed has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (rndSeed < -1)
            {
                MessageBox.Show("Seed has to be bigger or equal -1", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            settings.RndSeed = rndSeed;

            GlobalRnd.SetSeed(rndSeed);

            if (!int.TryParse(AVGLenBox.Text, out var avgLen))
            {
                MessageBox.Show("AVG len has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (avgLen <= 0)
            {
                MessageBox.Show("AVG len has to be bigger than 0", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            settings.AvgLen = avgLen;

            return true;
        }

        private bool LoadPlayers(ref List<Player?> players, bool stepping)
        {
            profileIngMog = ProfileSimulatorCheck.IsChecked!.Value;

            Action<string> terminalPrint = (s) =>
            {
                WriteLnToTerminal(s);
            };

            try
            {
                players = agentSettings.Select(m => m.GetSelectedPlayer(profileIngMog, stepping, terminalPrint)).Where(s => s != null).ToList();
            }
            catch (Exception e)
            {
                WriteLnToTerminal(e.ToString());
            }

            if (players.Count <= 2)
            {
                MessageBox.Show("Minimum player count is 3", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private List<SimulatorSettingsPlayer> GetPlayerSettings()
        {
            return agentSettings.Select(a => a.GetSettings()).ToList();
        }

        private void RunGames(int roundsCount, int avgLen, Action? SetSaveAgents = null)
        {
            stopSim = false;

            int runnedBeforeCount = lastResults.Count;

            simThread = new Thread(() =>
            {
                try
                {

                    var stopWatch = new Stopwatch();


                    try
                    {
                        stopWatch.Start();

                        for (int i = 0; i < roundsCount; i++)
                        {
                            lastResults.Add(lastGame!.Play());

                            SetProgress(i + 1 + runnedBeforeCount, roundsCount + runnedBeforeCount);

                            stopSimMutex.WaitOne();
                            if (stopSim)
                            {
                                i = roundsCount;
                            }
                            stopSimMutex.ReleaseMutex();

                            if (i == 0)
                            {
                                SetSaveAgents?.Invoke();
                            }
                        }

                        stopWatch.Stop();
                    }
                    catch (Exception e)
                    {
                        WriteLnToTerminal(e.ToString());

                        App.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            DoneActions();
                            WriteLnToTerminal("=-=-=-=-=-=-=-=-=-=-=-=-=");
                        }));

                        return;
                    }

                    long elapsedTicks = stopWatch.ElapsedTicks;

                    var winResoults = StatisticsEngine.WinsEval(lastResults);


                    WriteLnToTerminal($"Sheriff wins: {winResoults.SherifWins:F2}%");
                    WriteLnToTerminal($"Bandits wins: {winResoults.BanditWins:F2}%");
                    WriteLnToTerminal($"Renegade wins: {winResoults.RenegadeWins:F2}%");

                    var timespan = new TimeSpan(elapsedTicks);

                    WriteLnToTerminal($"Time elapsed {timespan.ToString(@"hh\:mm\:ss")}");

                    if (profileIngMog)
                    {
                        long tickSum = 0;

                        foreach (var p in lastGame!.Players)
                        {
                            if (p.Agent is AgentProfiller profiler)
                            {
                                tickSum += profiler.GetProfiledTicks();
                            }
                        }

                        var totalAgentTime = new TimeSpan(tickSum);

                        WriteLnToTerminal($"Agent Time elapsed {totalAgentTime.ToString(@"hh\:mm\:ss")}");

                        double agentPercentageOfWhole = tickSum / (double)elapsedTicks * 100;

                        WriteLnToTerminal($"Agent % of the time {agentPercentageOfWhole:F3}");
                    }

                    App.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        StatisticInfo(lastResults, avgLen);
                        DoneActions();

                        WriteLnToTerminal("=-=-=-=-=-=-=-=-=-=-=-=-=");

                    }));
                }
                catch (Exception e)
                {
                    App.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        WriteLnToTerminal(e.ToString());

                        WriteLnToTerminal("=-=-=-=-=-=-=-=-=-=-=-=-=");

                    }));
                    
                }
                

            });

            simThread.Start();
        }

        private void StatisticInfo(List<GameResoult> resoults, int AVGlen)
        {
            StatisticsPanel.Children.Clear();
            savableGraphs.Clear();

            StackPanel bangsCounts = new StackPanel()
            {
                Margin = new Thickness(5),
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };

            var playerLabels = agentSettings.Select(s => s.GetLabel()).Where(s => s is not null).ToArray();

            var last = resoults[resoults.Count - 1];

            int maxBangCount = 0;

            for (int i = 0; i < last.PlayerToPlayerBang.GetLength(0); i++)
            {
                for (int j = 0; j < last.PlayerToPlayerBang.GetLength(0); j++)
                {
                    if (last.PlayerToPlayerBang[i, j] > maxBangCount)
                    {
                        maxBangCount = last.PlayerToPlayerBang[i, j];
                    }
                }
            }

            for (int i = 0; i < last.PlayerToPlayerBang.GetLength(0); i++)
            {
                List<int> bangCounts = [];
                List<string> names = [];

                for (int j = 0; j < last.PlayerToPlayerBang.GetLength(0); j++)
                {
                    if (i != j)
                    {
                        bangCounts.Add(last.PlayerToPlayerBang[i, j]);
                        names.Add($"P{j + 1} {playerLabels[j]}");
                    }
                }

                PlayerShotingGaraph bangGraph = new PlayerShotingGaraph($"PLAYER{i + 1} {playerLabels[i]} BANGS", bangCounts.ToArray(), 
                    names.ToArray(), maxBangCount, $"PLAYER{i + 1} BANGS");

                savableGraphs.Add(bangGraph);

                bangsCounts.Children.Add(bangGraph);
            }

            StatisticsPanel.Children.Add(bangsCounts);

            StackPanel otherStats = new StackPanel()
            {
                Margin = new Thickness(5),
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };


            var turnsLengths = StatisticsEngine.AverageTurns(resoults, AVGlen); 

            var turnsGraph = new LineGraph("Average turns count", turnsLengths, "AVG turns");
            savableGraphs.Add(turnsGraph);

            otherStats.Children.Add(turnsGraph);


            var lifeGraphLabels = new List<string>();

            for (int i = 0; i < playerLabels.Length; i++)
            {
                lifeGraphLabels.Add($"P{i+1} {playerLabels[i]}");
            }

            var lastLifesGraph = new MultiLineGraph("Last round life progress", StatisticsEngine.PlayersLifesProgress(last), 
                lifeGraphLabels.ToArray(), "Lifes");

            savableGraphs.Add(lastLifesGraph);

            otherStats.Children.Add(lastLifesGraph);

            string[] roles =
            {
                "Sherif", "Bandit", "Renegade"
            };

            var winRatesAvg = StatisticsEngine.WinRatesAVGs(resoults, AVGlen);

            var winTareAVGGraph = new MultiLineGraph("Win rate AVG", winRatesAvg, roles, "WinRateAvgs");
            savableGraphs.Add(winTareAVGGraph);

            otherStats.Children.Add(winTareAVGGraph);

            WriteLnToTerminal($"Sheriff last AVG wins: {winRatesAvg[0][winRatesAvg[0].Length -1]:F2}%");
            WriteLnToTerminal($"Bandits last AVG wins: {winRatesAvg[1][winRatesAvg[0].Length - 1]:F2}%");
            WriteLnToTerminal($"Renegade last AVG wins: {winRatesAvg[2][winRatesAvg[0].Length - 1]:F2}%");

            StatisticsPanel.Children.Add(otherStats);

            StackPanel rewardStats = new StackPanel()
            {
                Margin = new Thickness(5),
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };

            var agents = lastGame!.Players.Select(p => p.Agent).ToArray();

            List<List<float>?> rewards = agents.Select(a => a.HasReward() ? a.GetRewards() : null).ToList();

            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i] != null && rewards[i]!.Count > 2)
                {
                    float[] avgs = StatisticsEngine.RewardsAVG(rewards[i]!, AVGlen);
                    
                    var rewardGraph = new LineGraph($"PLAYER{ i + 1 } {playerLabels[i]!} AVG REWARDS", avgs, $"PLAYER{i + 1} AVG-R");
                    rewardStats.Children.Add(rewardGraph);
                    savableGraphs.Add(rewardGraph);
                }
            }


            StatisticsPanel.Children.Add(rewardStats);

            //TODO: last deck memory
        }

        private void Save_termina_BT(object sender, RoutedEventArgs e)
        {

            StringBuilder stringBuilder = new StringBuilder();

            foreach (var line in terminal)
            {
                stringBuilder.AppendLine(line);
            }


            var dialog = new SaveFileDialog
            {
                Title = "Save Chart",
                Filter = "LOG file (*.log)|*.log",
                DefaultExt = ".log",
                AddExtension = true,
                FileName = "terminal.log"
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, stringBuilder.ToString());
            }
        }

        private void Save_All_Graphs(object sender, RoutedEventArgs e)
        {
            if (savableGraphs.Count > 0) 
            {

                var dialog = new OpenFolderDialog();
                dialog.Title = "Select the output folder";

                if (dialog.ShowDialog()!.Value) 
                {
                    string folderPath = dialog.FolderName;

                    savableGraphs.ForEach(s => { s.Save(folderPath); });
                }

            }
        }

        private void Clear_Terminal_BT(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you shore ?", "WARNING", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }


            terminalMutex.WaitOne();

            terminal.Clear();
            terminal.AddFirst($">");

            TerminalBlock.Text = terminal.First();

            terminalMutex.ReleaseMutex();
        }

        private void Expoert_data_BT(object sender, RoutedEventArgs e)
        {
            if (lastResults is not null && lastResults.Count() > 0)
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save Data",
                    Filter = "JSON file (*.json)|*.json",
                    DefaultExt = ".json",
                    AddExtension = true,
                    FileName = "data.json"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var serializableRes = lastResults.Select(r => new GameResoultSerializable(r)).ToList();

                var last_json = JsonSerializer.Serialize(serializableRes, new JsonSerializerOptions
                {
                    WriteIndented = true
                });


                File.WriteAllText(dialog.FileName, last_json);
                
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "",
                Filter = "JSON file (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = "simSettings.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            LoadDataFromFormular();

            settings.Players = GetPlayerSettings();

            var jtext = JsonSerializer.Serialize(settings, options: new()
            {
                WriteIndented = true
            });

            settings.Players = [];

            File.WriteAllText(dialog.FileName, jtext);
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "",
                Filter = "JSON file (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var text = File.ReadAllText(dialog.FileName);
                var settingsLoaded = JsonSerializer.Deserialize<SimulatorSettings>(text);
                
                if (settingsLoaded is null || settingsLoaded.Players.Count() == 0)
                {
                    MessageBox.Show("Unable to deserialize file", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                settings = settingsLoaded;

                RoundsCountBox.Text = $"{settings.Rounds}";
                MemSizeBox.Text = $"{settings.MemSize}";
                MemLimitBox.Text = $"{settings.MemLimit}";
                RndSeedBox.Text = $"{settings.RndSeed}";
                AVGLenBox.Text = $"{settings.AvgLen}";

                for(int i = 0; i < agentSettings.Count(); i++)
                {
                    agentSettings[i].LoadSettings(settings.Players[i]);
                }

                settings.Players = [];
            }
            catch
            {
                MessageBox.Show("Unable to load file", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

    

