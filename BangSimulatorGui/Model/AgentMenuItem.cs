using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BangSimulatorGui.Other;
using BangSimulatorLib.Agent;
using BangSimulatorLib.Game;
using BangSimulatorLib.Statistics;
using Microsoft.Win32;
using ScottPlot.Colormaps;

namespace BangSimulatorGui.Model
{
    public class AgentMenuItem : StackPanel
    {
        private string name;

        private ComboBox agentTypeBox = new();
        private ComboBox comboboxRole = new();

        private StackPanel stepWaitPanel = new();
        private CheckBox stepWaitCheckBox = new();

        private StackPanel pythonIdPanel = new();
        private TextBox pythonIdBox = new();

        private StackPanel savePathPanel = new();
        private TextBox savePathBox = new();

        private StackPanel loadPathPanel = new();
        private TextBox loadPathBox = new();

        private StackPanel evalPanel = new();
        private CheckBox evalModCheckBox = new();

        private bool agentTypeAdded = false;

        public AgentMenuItem(string name)
        {
            this.HorizontalAlignment = HorizontalAlignment.Left;

            this.name = name;

            this.Margin = new Thickness(5, 5, 5, 5);

            var nameBox = new TextBlock();
            nameBox.Text = name;
            nameBox.FontSize = 14;
            nameBox.Margin = new Thickness(2);

            agentTypeBox.HorizontalAlignment = HorizontalAlignment.Left;
            agentTypeBox.ItemsSource = Enum.GetValues(typeof(AgentType));
            agentTypeBox.SelectedIndex = 0;
            agentTypeBox.Margin = new Thickness(2);
            agentTypeBox.Width = 100;


            StackPanel[] panels =
            {
                stepWaitPanel, pythonIdPanel, savePathPanel, loadPathPanel, evalPanel
            };

            foreach(var panel in panels)
            {
                panel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                panel.Margin = new Thickness(2);
            }

            var stepWaitLabel = new TextBlock();
            stepWaitLabel.Width = 75;
            stepWaitLabel.Text = "Use step";


            stepWaitPanel.Children.Add(stepWaitLabel);
            stepWaitPanel.Children.Add(stepWaitCheckBox);

            comboboxRole.HorizontalAlignment = HorizontalAlignment.Left;
            comboboxRole.ItemsSource = Enum.GetValues(typeof(AgentRole));
            comboboxRole.SelectedIndex = 4;
            comboboxRole.Margin = new Thickness(2);
            comboboxRole.Width = 100;

            comboboxRole.LostFocus += (x, y) =>
            {
                LostFocusOV();
            };

            agentTypeBox.LostFocus += (x, y) =>
            {
                AgentTypeLostFocus();
            };

            this.Children.Add(nameBox);
            this.Children.Add(comboboxRole);

            this.Children.Add(agentTypeBox);
            this.Children.Add(stepWaitPanel);

            agentTypeBox.Visibility = Visibility.Collapsed;
            stepWaitPanel.Visibility = Visibility.Collapsed;

            //----------------------------------------------------------------

            var pyIdLabel = new TextBlock();
            pyIdLabel.Text = "Py agent ID";
            pyIdLabel.Width = 75;
            
            pythonIdBox.Width = 100;

            pythonIdBox.LostFocus += (x, y) =>
            {
                if (int.TryParse(pythonIdBox.Text, out int i))
                {
                    if (i < 0)
                    {
                        pythonIdBox.Text = string.Empty;
                    }
                }
                else
                {
                    pythonIdBox.Text = string.Empty;
                }
            };

            pythonIdPanel.Children.Add(pyIdLabel);
            pythonIdPanel.Children.Add(pythonIdBox);

            pythonIdPanel.Visibility = Visibility.Collapsed;
            this.Children.Add(pythonIdPanel);

            //----------------------------------------------------------------

            var saveButton = new Button();
            saveButton.Click += (x, y) =>
            {
                SaveFileDialog dialog = new SaveFileDialog();

                if (dialog.ShowDialog()!.Value)
                {
                    savePathBox.Text = dialog.FileName;
                }
                else
                {
                    savePathBox.Text = string.Empty;
                }
            };

            var saveLabel = new TextBlock();
            saveLabel.Text = "Save path";
            saveLabel.Width = 75;

            saveButton.Content = "B";
            saveButton.Width = 20;

            savePathBox.Width = 100;

            savePathPanel.Children.Add(saveLabel);
            savePathPanel.Children.Add(savePathBox);
            savePathPanel.Children.Add(saveButton);

            savePathPanel.Visibility = Visibility.Collapsed;
            this.Children.Add(savePathPanel);

            //---------------------------------------------------------------

            var loadButton = new Button();
            loadButton.Click += (x, y) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();

                if (dialog.ShowDialog()!.Value)
                {
                    loadPathBox.Text = dialog.FileName;
                }
                else
                {
                    loadPathBox.Text = string.Empty;
                }
            };

            loadButton.Content = "B";
            loadButton.Width = 20;

            var loadLabel = new TextBlock();
            loadLabel.Text = "Load path";
            loadLabel.Width = 75;

            loadPathBox.Width = 100;

            loadPathPanel.Children.Add(loadLabel);
            loadPathPanel.Children.Add(loadPathBox);
            loadPathPanel.Children.Add(loadButton);

            loadPathPanel.Visibility = Visibility.Collapsed;
            this.Children.Add(loadPathPanel);

            //---------------------------------------------------------------

            var evalLabel = new TextBlock();
            evalLabel.Text = "Eval mod";
            evalLabel.Width = 75;

            evalPanel.Children.Add(evalLabel);
            evalPanel.Children.Add(evalModCheckBox);

            evalPanel.Visibility = Visibility.Collapsed;
            this.Children.Add(evalPanel);
        }

        public void PreselectRole(int index)
        {
            comboboxRole.SelectedIndex = index;
            LostFocusOV();
        }

        public string? GetLabel()
        {
            if (comboboxRole.SelectedIndex == 4) return null;

            PlayerRole role = (PlayerRole)(int)comboboxRole.SelectedItem;
            var agentType = (AgentType)agentTypeBox.SelectedItem;
            return $"({role.ToString()}/{agentType.ToString()})";
        }

        public Player? GetSelectedPlayer(bool profileAgent, bool stepping)
        {
            if (comboboxRole.SelectedIndex == 4) return null;

            PlayerRole role = (PlayerRole)(int)comboboxRole.SelectedItem;

            IAgent agent;

            if (!int.TryParse(pythonIdBox.Text, out int id) || id < 0)
            {
                id = 0;
            }

            switch((AgentType)agentTypeBox.SelectedItem)
            {
                case AgentType.Scripted: 
                    agent = new ScriptedAgent(); break;
                case AgentType.Python:
                    agent = new PythonAgent(id); break;
                case AgentType.NetDQN:
                    agent = new DQNAgentNet(); break;
                default: agent = new RandomAgent(); break;
            }

            agent.SetEval(evalModCheckBox.IsChecked!.Value);
            
            if (loadPathBox.Text != string.Empty)
            {
                agent.Load(loadPathBox.Text);
            }

            //TODO:: save

            if (profileAgent)
            {
                agent = new AgentProfiller(agent);
            }

            if (stepWaitCheckBox.IsChecked!.Value && stepping)
            {
                agent = new StepAgent(agent);
            }

            return new Player(role, agent);
        }

        private void LostFocusOV()
        {
            if (comboboxRole.SelectedIndex == 4 && agentTypeAdded == true)
            {
                agentTypeBox.Visibility = Visibility.Collapsed;
                stepWaitPanel.Visibility = Visibility.Collapsed;
                pythonIdPanel.Visibility = Visibility.Collapsed;
                savePathPanel.Visibility = Visibility.Collapsed;
                loadPathPanel.Visibility = Visibility.Collapsed;
                evalPanel.Visibility = Visibility.Collapsed;

                agentTypeAdded = false;
            }
            else if (comboboxRole.SelectedIndex != 4 && agentTypeAdded == false)
            {

                agentTypeBox.Visibility = Visibility.Visible;
                stepWaitPanel.Visibility = Visibility.Visible;
                agentTypeAdded = true;

                AgentTypeLostFocus();
            }
        }

        private void AgentTypeLostFocus()
        {
            var selected = (AgentType)agentTypeBox.SelectedItem;

            if (selected == AgentType.Python)
            {
                pythonIdPanel.Visibility = Visibility.Visible;
            }
            else
            {
                pythonIdPanel.Visibility = Visibility.Collapsed;
            }

            if (selected == AgentType.Python || selected == AgentType.NetDQN)
            {
                savePathPanel.Visibility = Visibility.Visible;
                loadPathPanel.Visibility = Visibility.Visible;
                evalPanel.Visibility = Visibility.Visible;
            }
            else
            {
                savePathPanel.Visibility = Visibility.Collapsed;
                loadPathPanel.Visibility = Visibility.Collapsed;
                evalPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}