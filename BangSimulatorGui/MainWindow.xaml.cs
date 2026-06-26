using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection.Emit;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
            int roundsCount = 0;
            int avgLen = 0;

            if (!LoadDataToRun(ref players, ref roundsCount, ref avgLen, false)) { return; }

            Start_BT.Visibility = Visibility.Collapsed;
            Stepping_BT.Visibility = Visibility.Collapsed;
            Stop_BT.Visibility = Visibility.Visible;
            Continue_BT.Visibility = Visibility.Visible;
            Continue_BT.IsEnabled = false;
            agentSettings.ForEach(a => a.DisableEdit(false,false) );

            SetProgress(0, roundsCount);

            lastGame = new GameEngine(players.ToArray()!);
            lastResults = [];

            RunGames(roundsCount, avgLen);
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            List<Player?> players = [];
            int roundsCount = 0;
            int avgLen = 0;

            if (!LoadDataToRun(ref players, ref roundsCount, ref avgLen, false)) { return; }

            inWait = false;
            Stop_BT.IsEnabled = true;
            Continue_BT.IsEnabled = false;
            agentSettings.ForEach(a => a.DisableEdit(false, false));

            var agents = lastGame!.Players.Select(p => p.Agent).ToArray();
            var evals = agentSettings.Select(a => a.IsEval()).ToArray();
            var savePaths = agentSettings.Select(a => a.SaveLocation()).ToArray();

            for (int i  = 0; i < agents.Count(); i++)
            {
                agents[i].SetEval(evals[i]);
                agents[i].SetAutoSavePath(savePaths[i]);
            }

            SetProgress(lastResults.Count, roundsCount + lastResults.Count);

            RunGames(roundsCount, avgLen);
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

        private bool LoadDataToRun(ref List<Player?> players, ref int roundsCount, ref int avgLen, bool stepping)
        {
            profileIngMog = ProfileSimulatorCheck.IsChecked!.Value;

            Action<string> terminalPrint = (s) =>
            {
                WriteLnToTerminal(s);
            };

            players = agentSettings.Select(m => m.GetSelectedPlayer(profileIngMog, stepping, terminalPrint)).Where(s => s != null).ToList();

            if (players.Count <= 2)
            {
                MessageBox.Show("Minimum player count is 3", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(RoundsCountBox.Text, out roundsCount))
            {
                MessageBox.Show("Round count has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (roundsCount <= 0)
            {
                MessageBox.Show("Round count has to be bigger than 0", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }


            if (!int.TryParse(MemSizeBox.Text, out var memSize))
            {
                MessageBox.Show("Memory size has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (memSize <= 4)
            {
                MessageBox.Show("Memory size has to be bigger than 4", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            Deck.SetDeckMemory(memSize);

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

            GlobalRnd.SetSeed(rndSeed);

            if (!int.TryParse(AVGLenBox.Text, out avgLen))
            {
                MessageBox.Show("AVG len has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (avgLen <= 0)
            {
                MessageBox.Show("AVG len has to be bigger than 0", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void RunGames(int roundsCount, int avgLen)
        {
            stopSim = false;

            int runnedBeforeCount = lastResults.Count;

            simThread = new Thread(() =>
            {
                var stopWatch = new Stopwatch();


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
                }

                stopWatch.Stop();

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
    }
}

    

