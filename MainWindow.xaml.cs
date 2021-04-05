using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ViDi2.Training;
using ViDi2.Training.UI;

namespace VPDL.Plugin.ConfusionMatrix
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window, IPlugin
    {
        //[STAThreadAttribute] //Clipboard 용 필요 속성
        List<string> classList = new List<string>();
        int tabNumber = 0; //0 Test, 1 Train, 2 All
        bool UseThreshold = true; //0 Use, 1 Not Use
        string resultClipboard = "";
        double[,] confusionMatrix;



        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
        }


        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        /// <summary>
        /// Context of other plugins, can be used to list other plugins.
        /// </summary>
        IPluginContext context;
        MenuItem pluginMenuItem;


        /// <summary>
        /// Gives a human readeable name of the plugin
        /// </summary>
        string IPlugin.Name { get { return "Confusion Matrix"; } }

        /// <summary>
        /// Gives a short description of the plugin
        /// </summary>
        string IPlugin.Description
        {
            get { return "Show Confusion Matrix, Only available in Green Tool"; }
        }

        /// <summary>
        /// Initilization method of the plugin. Called by the VisionPro Deep Learning Gui directly after the plugin is loaded.
        /// </summary>
        void IPlugin.Initialize(IPluginContext context)
        {
            this.context = context;

            var pluginContainerMenuItem =
                context.MainWindow.MainMenu.Items.OfType<MenuItem>().
                First(i => (string)i.Header == "Plugins");

            pluginMenuItem = new MenuItem()
            {
                Header = ((IPlugin)this).Name,
                IsEnabled = true,
                ToolTip = ((IPlugin)this).Description
            };

            pluginMenuItem.Click += (o, a) => { Run(); };

            pluginContainerMenuItem.Items.Add(pluginMenuItem);
        }

        /// <summary>
        /// Deinitialization method of the plugin. Called by the VisionPro Deep Learning Gui when exiting or when unloading the plugin.
        /// </summary>
        void IPlugin.DeInitialize()
        {
            var pluginContainerMenuItem =
                context.MainWindow.MainMenu.Items.OfType<MenuItem>().
                First(i => (string)i.Header == "Plugins");

            pluginContainerMenuItem.Items.Remove(pluginMenuItem);
        }

        /// <summary>
        /// Version of the plugin.
        /// </summary>
        int IPlugin.Version { get { return 1; } }



        private void Run()
        {
            try
            {
                IWorkspace workspace = context.MainWindow.WorkspaceBrowserViewModel.CurrentWorkspace;

                if (workspace == null)
                    MessageBox.Show("No current workspace");
                else
                {
                    this.Show();
                    //ShowConfusionMatrix();

                    //context.MainWindow.WorkspaceBrowserViewModel.WorkspaceSelected += WorkspaceBrowserViewModel_WorkspaceSelected;
                    //context.MainWindow.ToolChainViewModel.StreamSelected += ToolChainViewModel_StreamSelected;
                    context.MainWindow.ToolChainViewModel.ToolSelected += ToolChainViewModel_ToolSelected;
                    context.MainWindow.DatabaseExplorerViewModel.PropertyChanged += SampleViewerViewModel_PropertyChanged;

                }
            }
            catch
            { 
            
            }
        }

        private void WorkspaceBrowserViewModel_WorkspaceSelected(IWorkspace workspace)
        {
            context.MainWindow.DatabaseExplorerViewModel.PropertyChanged += SampleViewerViewModel_PropertyChanged;
        }
        private void ToolChainViewModel_StreamSelected(IStream stream)
        {
            if (context.MainWindow.DatabaseExplorerViewModel == null) return;
            context.MainWindow.DatabaseExplorerViewModel.PropertyChanged += SampleViewerViewModel_PropertyChanged;
        }
        private void ToolChainViewModel_ToolSelected(ITool tool)
        {
            if (context.MainWindow.DatabaseExplorerViewModel == null) return;
            context.MainWindow.DatabaseExplorerViewModel.PropertyChanged += SampleViewerViewModel_PropertyChanged;

            UpdateConfusionMatrix();
        }
        private void SampleViewerViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateConfusionMatrix();
        }

        private void UpdateConfusionMatrix()
        {
            if (IsException()) return;
            MakeClassList();
            MakeConfusionMatrix();
            ShowResultText();
        }

        private bool IsException()
        {
            var selectedView = context.MainWindow.DatabaseExplorerViewModel.SelectedView;
            if (selectedView == null) return true;
            var selectedTool = context.MainWindow.ToolChainViewModel.Tool;
            if (selectedTool == null) return true;
            var dataBase = selectedTool.Database;
            if (dataBase == null) return true;
            var marking = dataBase.GetMarking(selectedView);
            if (marking == null) return true;
            if (marking.ToolType == ViDi2.ToolType.GreenHighDetail || marking.ToolType == ViDi2.ToolType.Green) return false;

            return true;
        }

        private void MakeClassList()
        {
            var database = context.MainWindow.ToolChainViewModel.Tool.Database as IGreenDatabase;
            var labeledClass = database.Parameters.LabeledClass;

            classList.Clear();

            foreach (var classItem in labeledClass)
            {
                classList.Add(classItem.ToString());
            }
        }

        private void MakeConfusionMatrix()
        {
            int totalGrid = classList.Count + 1; // Class Label도 있기 때문에 1개 추가
            double[,] tempConfusionMatrix = new double[classList.Count, classList.Count];

            Grid thisGrid;
            switch (tabNumber)
            {
                case 0:
                    thisGrid = Test_Grid;
                    break;

                case 1:
                    thisGrid = Train_Grid;
                    break;

                case 2:
                    thisGrid = All_Grid;
                    break;

                default:
                    return;
            }

            thisGrid.Children.Clear();
            thisGrid.RowDefinitions.Clear();
            thisGrid.ColumnDefinitions.Clear();


            for (int gridNum = 0; gridNum < totalGrid; gridNum++)
            {
                ColumnDefinition tempCol = new ColumnDefinition();
                thisGrid.ColumnDefinitions.Add(tempCol);
                tempCol.Width = gridNum == 0 ? new GridLength(40) : new GridLength(1, GridUnitType.Star);

                RowDefinition tempRow = new RowDefinition();
                thisGrid.RowDefinitions.Add(tempRow);
                tempRow.Height = gridNum == 0 ? new GridLength(40) : new GridLength(1, GridUnitType.Star);
            }


            for (int col = 0; col < totalGrid; col++)
            {
                for (int row = 0; row < totalGrid; row++)
                {
                    if (row == 0 && col == 0)
                    {
                        continue;
                    }

                    if (row == 0 || col == 0)
                    {
                        TextBlock tempTextBlock = new TextBlock();
                        Grid.SetColumn(tempTextBlock, col);
                        Grid.SetRow(tempTextBlock, row);
                        tempTextBlock.Text = col == 0 ? classList[row - 1] : classList[col - 1];
                        tempTextBlock.FontWeight = FontWeights.Bold;
                        tempTextBlock.VerticalAlignment = VerticalAlignment.Center;
                        tempTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                        thisGrid.Children.Add(tempTextBlock);
                    }
                    else
                    {
                        string filter = MakeResultFilter(col, row);
                                                
                        IGreenTool tool = context.MainWindow.ToolChainViewModel.Tool as IGreenTool;
                        var database = tool.Database;
                        var tempList = database.List(filter);
                        int countView = tempList.Count;

                        tempConfusionMatrix[col - 1, row - 1] = countView;

                        Button tempButton = new Button();
                        tempButton.Content = countView;
                        tempButton.FontWeight = row == col ? FontWeights.Bold : FontWeights.Normal;
                        tempButton.Foreground = row > col ? Brushes.Blue : Brushes.Red;
                        if (row == col || countView == 0) tempButton.Foreground = Brushes.Black;
                        tempButton.Tag = filter;
                        tempButton.Click += new RoutedEventHandler(ConfusionMatrixValue_Btn_Click);
                        tempButton.Background = Brushes.White;
                        tempButton.BorderThickness = new Thickness(0);
                        Grid.SetColumn(tempButton, col);
                        Grid.SetRow(tempButton, row);
                        thisGrid.Children.Add(tempButton);
                    }
                }
            }

            confusionMatrix = tempConfusionMatrix;
        }

        private string MakeResultFilter(int Col, int Row)
        {
            string resultFilter = string.Empty;

            string predict = " and best_tag='" + classList[Col - 1] + "'";
            string actual = "tag![name='" + classList[Row - 1] + "']";
            string traintest = string.Empty;
            string threshold = UseThreshold == true ? "and score>threshold" : "";


            switch (tabNumber)
            {
                case 0:
                    traintest = " and not trained";
                    break;

                case 1:
                    traintest = " and trained";
                    break;

                case 2:
                    traintest = "";
                    break;

                default:
                    return string.Empty;
            }

            return resultFilter = actual + predict + traintest + threshold;
        }

        private void ConfusionMatrixValue_Btn_Click(object sender, RoutedEventArgs e)
        {
            Button thisButton = sender as Button;

            context.MainWindow.DatabaseExplorerViewModel.Refresh.Execute(context.MainWindow.DatabaseExplorerViewModel.Filter.Name = thisButton.Tag.ToString());
            context.MainWindow.DatabaseExplorerViewModel.Refresh.Execute(context.MainWindow.DatabaseExplorerViewModel.Filter.Value = thisButton.Tag.ToString());
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string tabItem = ((sender as TabControl).SelectedItem as TabItem).Header.ToString();

            switch (tabItem)
            {
                case "Test":
                    tabNumber = 0;
                    break;

                case "Train":
                    tabNumber = 1;
                    break;

                case "All":
                    tabNumber = 2;
                    break;

                default:
                    return;
            }

            UpdateConfusionMatrix();
        }

        private void Threshold_Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (UseThreshold)
            {
                Threshold_Toggle.Content = "Threshold Mode";
                UseThreshold = false;
            }
            else
            {
                Threshold_Toggle.Content = "Not Threshold Mode";
                UseThreshold = true;
            }

            UpdateConfusionMatrix();
        }

        private void ShowResultText()
        {
            //string precision = "";
            //string recall = "";

            //double acc = ((double)RealNum / (double)TotalNum) * 100;

            int sum = (int)confusionMatrix.Cast<double>().Sum();
            int correctSum = 0;

            for (int j = 0; j < confusionMatrix.GetLength(1); j++)
            {
                for (int i = 0; i < confusionMatrix.GetLength(0); i++)
                {
                    if (i == j)
                    {
                        correctSum += (int)confusionMatrix[i, j];
                    }
                }
            }

            double acc = (double)correctSum / (double)sum * 100;

            ResultText_TB.Text = "Total : " + sum.ToString() + "  /  Acc : " + string.Format("{0:0.00}", acc);
        }

        private void ResultText_Btn_Click(object sender, RoutedEventArgs e)
        {

            //string newline = "\n";
            //string tab = "\t";
            //string resultClipboard = tab + "Actual" + newline + "Predict" + "1" + newline + "Result" + tab + tab + "Check";

            string a = "";
            for (int j = 0; j < confusionMatrix.GetLength(1); j++)
            {
                for (int i = 0; i < confusionMatrix.GetLength(0); i++)
                {
                    a += confusionMatrix[i, j].ToString();
                    
                    if (i != confusionMatrix.GetLength(0) - 1)
                    {
                        a += "\t";
                    }
                }

                if (j != confusionMatrix.GetLength(1) - 1)
                {
                    a += "\n";
                }

            }

            Clipboard.SetText(a);
        }


    }
}
