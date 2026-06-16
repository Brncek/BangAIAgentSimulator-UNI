using System.Diagnostics;
using System.Reflection.Emit;
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
using BangSimulatorLib.Agent;
using BangSimulatorLib.Game;
using BangSimulatorLib.Statistics;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.WPF;

namespace BangSimulatorGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<AgentMenuItem> agentSettings = [];

        private LinkedList<string> terminal = [];
        private int maxTerminalLines = 1000;

        private Thread? simThread;
        private GameEngine? lastGame;

        private bool stopSim = false;
        private Mutex stopSimMutex = new Mutex();

        private bool profileIngMog = false;

        public MainWindow()
        {
            InitializeComponent();
            InitAgentMenu();
            SetProgress(0, 0);
        }

        private void InitAgentMenu()
        {
            for (int i = 0; i < 7; i++)
            {
                var agentMenu = new AgentMenuItem($"Agent {i + 1}");
                agentSettings.Add( agentMenu );
                PlayersSettings.Children.Add(agentMenu);
            
                var separator =  new Separator();
                separator.Margin = new Thickness(2);

                PlayersSettings.Children.Add(separator);
            }
        }

        private void Step_Click(object sender, RoutedEventArgs e)
        {
            //TODO:
        }

        private void Stepping_CLick(object sender, RoutedEventArgs e)
        {
            //TODO:
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            profileIngMog = ProfileSimulatorCheck.IsChecked!.Value;

            var players = agentSettings.Select(m => m.GetSelectedPlayer(profileIngMog)).Where(s => s != null).ToList();

            if (players.Count <= 2)
            {
                MessageBox.Show("Minimum player count is 3", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(RoundsCountBox.Text, out var roundsCount))
            {
                MessageBox.Show("Round count has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (roundsCount <= 0)
            {
                MessageBox.Show("Round count has to be bigger than 0", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            if (!int.TryParse(MemSizeBox.Text, out var memSize))
            {
                MessageBox.Show("Memory size has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (memSize <= 4)
            {
                MessageBox.Show("Memory size has to be bigger than 4", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Deck.SetDeckMemory(memSize);

            if (!int.TryParse(RndSeedBox.Text, out var rndSeed))
            {
                MessageBox.Show("Seed has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (rndSeed < -1)
            {
                MessageBox.Show("Seed has to be bigger or equal -1", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GlobalRnd.SetSeed(rndSeed);

            if (!int.TryParse(AVGLenBox.Text, out var avgLen))
            {
                MessageBox.Show("AVG len has to be number", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (avgLen <= 0)
            {
                MessageBox.Show("AVG len has to be bigger than 0", "WARNING", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Start_BT.IsEnabled = false;
            Stop_BT.IsEnabled = true;
            Stepping_BT.IsEnabled = false;
            PlayersSettings.IsEnabled = false;

            

            SetProgress(0, roundsCount);

            stopSim = false;

            simThread = new Thread(() =>
            {
                var stopWatch = new Stopwatch();

                List<GameResoult> resoults = [];
                lastGame = new GameEngine(players.ToArray()!);

                stopWatch.Start();

                for (int i = 0; i < roundsCount; i++)
                {
                    resoults.Add(lastGame.Play());

                    SetProgress(i + 1, roundsCount);

                    stopSimMutex.WaitOne();
                    if (stopSim)
                    {
                        i = roundsCount;
                    }
                    stopSimMutex.ReleaseMutex();
                }

                stopWatch.Stop();

                long elapsedTicks = stopWatch.ElapsedTicks;

                var winResoults = StatisticsEngine.WinsEval(resoults);


                WriteLnToTerminal($"Sheriff wins: {winResoults.SherifWins:F2}%");
                WriteLnToTerminal($"Bandits wins: {winResoults.BanditWins:F2}%");
                WriteLnToTerminal($"Renegade wins: {winResoults.RenegadeWins:F2}%");

                var timespan = new TimeSpan(elapsedTicks);

                WriteLnToTerminal($"Time elapsed {timespan.ToString(@"hh\:mm\:ss")}"); 

                if (profileIngMog)
                {
                    long tickSum = 0;

                    foreach (var p in lastGame.Players)
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
                    DrawStatisticInfo(resoults, avgLen);
                    StopActions();

                    WriteLnToTerminal("=-=-=-=-=-=-=-=-=-=-=-=-=");

                }));
            });

            simThread.Start();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            stopSimMutex.WaitOne();

            stopSim = true;

            stopSimMutex.ReleaseMutex();

            Stop_BT.IsEnabled = false;
        }

        private void StopActions()
        {
            Stop_BT.IsEnabled = false;
            Stepping_BT.IsEnabled = true;
            Start_BT.IsEnabled = true;
            Step_BT.IsEnabled = false;
            PlayersSettings.IsEnabled = true;
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

            App.Current.Dispatcher.Invoke(new Action(() =>
            {
                TerminalBlock.Text = stringBuilder.ToString();

            }));
        }

        private void DrawStatisticInfo(List<GameResoult> resoults, int AVGlen)
        {
            StatisticsPanel.Children.Clear();

            StackPanel bangsCounts = new StackPanel()
            {
                Margin = new Thickness(5),
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };

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
                        names.Add($"P{j + 1}");
                    }
                }

                PlayerShotingGaraph bangGraph = new PlayerShotingGaraph($"PLAYER{i + 1} BANGS", bangCounts.ToArray(), names.ToArray(), maxBangCount);
                bangsCounts.Children.Add(bangGraph);
            }

            StatisticsPanel.Children.Add(bangsCounts);

            StackPanel otherStats = new StackPanel()
            {
                Margin = new Thickness(5),
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };


            var turnsLengths = StatisticsEngine.AverageTurns(resoults, AVGlen); 

            var turnsGraph = new LineGraph("Average turns count", turnsLengths);

            otherStats.Children.Add(turnsGraph);

            var lastLifesGraph = new MultiLineGraph("Last round life progress", StatisticsEngine.PlayersLifesProgress(last), 
                Enumerable.Range(1, last.PlayerToPlayerBang.GetLength(0)).Select(n => $"P{n}").ToArray());  

            otherStats.Children.Add(lastLifesGraph);

            StatisticsPanel.Children.Add(otherStats);

            //TODO: last deck memory
        }
    }

    public class MultiLineGraph : StackPanel
    {
        public MultiLineGraph(string name, List<float[]> nums, string[] names)
        {
            var plot = new WpfPlot()
            {
                Height = 300,
                Width = 600,
                Margin = new Thickness(5)
            };

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
    }

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

    public class AgentMenuItem : StackPanel
    {
        private string name;

        private ComboBox agentTypeBox;
        private ComboBox comboboxRole;

        private StackPanel stepWaitPanel;
        private CheckBox stepWaitCheckBox;


        private bool agentTypeAdded = false;

        public AgentMenuItem(string name)
        {
            this.name = name;

            this.Margin = new Thickness(5, 5, 5, 5);

            var nameBox = new TextBlock();
            nameBox.Text = name;
            nameBox.FontSize = 14;
            nameBox.Margin = new Thickness(2);

            agentTypeBox = new ComboBox();
            agentTypeBox.ItemsSource = Enum.GetValues(typeof(AgentType));
            agentTypeBox.SelectedIndex = 0;
            agentTypeBox.Margin = new Thickness(2);
            agentTypeBox.Width = 100;

            stepWaitPanel = new StackPanel();
            stepWaitPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
            stepWaitPanel.Margin = new Thickness(2);

            var stepWaitLabel = new TextBlock();
            stepWaitLabel.Text = "Use step";
            stepWaitLabel.Margin = new Thickness(2,0,2,0);

            stepWaitCheckBox = new CheckBox();
            stepWaitCheckBox.Margin = new Thickness(2, 0, 2, 0);

            stepWaitPanel.Children.Add(stepWaitLabel);
            stepWaitPanel.Children.Add(stepWaitCheckBox);

            comboboxRole = new ComboBox();
            comboboxRole.ItemsSource = Enum.GetValues(typeof(AgentRole));
            comboboxRole.SelectedIndex = 4;
            comboboxRole.Margin = new Thickness(2);
            comboboxRole.Width = 100;

            comboboxRole.LostFocus += (x, y) =>
            {
                if (comboboxRole.SelectedIndex == 4 && agentTypeAdded == true)
                {
                    this.Children.Remove(agentTypeBox);
                    this.Children.Remove(stepWaitPanel);
                    agentTypeAdded = false;
                }
                else if (comboboxRole.SelectedIndex != 4 && agentTypeAdded == false)
                {
                    this.Children.Add(agentTypeBox);
                    this.Children.Add(stepWaitPanel);
                    agentTypeAdded = true;
                }
            };


            this.Children.Add(nameBox);
            this.Children.Add(comboboxRole);
            
        }

        public Player? GetSelectedPlayer(bool profileAgent)
        {
            if (comboboxRole.SelectedIndex == 4) return null;

            PlayerRole role = (PlayerRole)(int)comboboxRole.SelectedItem;

            IAgent agent;

            switch((AgentType)agentTypeBox.SelectedItem)
            {
                case AgentType.Scripted: 
                    agent = new ScriptedAgent(); break;
                case AgentType.Python:
                    agent = new PythonAgent(); break;
                case AgentType.NetDQN:
                    agent = new DQNAgentNet(); break;
                default: agent = new RandomAgent(); break;
            }

            //TODO: STEP AGENT

            if (profileAgent)
            {
                agent = new AgentProfiller(agent);
            }

            return new Player(role, agent);
        }
    }

    public class GuiPlayerInfo
    {
        //TODO::
    }

    public class GuiDeckMemory
    {
        //TODO::
    }

    public enum AgentType
    {
        Random,
        Scripted,
        Python,
        NetDQN
    }

    public enum AgentRole
    {
        NONE = -1,
        Sheriff = 0,
        Deputy = 1,
        Bandit = 2,
        Renegade = 3, 
    }
}