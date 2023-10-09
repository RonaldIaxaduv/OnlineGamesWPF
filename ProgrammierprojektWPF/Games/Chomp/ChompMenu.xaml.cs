using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace ProgrammierprojektWPF
{
    /// <summary>
    /// Interaction logic for Chomp.xaml
    /// </summary>
    public partial class ChompMenu : Window
    {
        private Rectangle[,] fields;
        private Chomp wrapper;

        private bool active = false;
        public bool Active
        {
            get { return active; }
            set
            {
                active = value;
                if (value) //active -> normal rectangles
                {

                    for (int x = 0; x < fields.GetLength(0); x++)
                    {
                        for (int y = 0; y < fields.GetLength(1); y++)
                        {
                            if (fields[x,y] != null)
                            { fields[x, y].Effect = null; }
                        }
                    }
                }
                else //inactive -> blurred rectangles
                {
                    var blurEf = new System.Windows.Media.Effects.BlurEffect() { Radius = 5, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance, KernelType = System.Windows.Media.Effects.KernelType.Box };
                    for (int x = 0; x < fields.GetLength(0); x++)
                    {
                        for (int y = 0; y < fields.GetLength(1); y++)
                        {
                            if (fields[x, y] != null)
                            {
                                fields[x, y].Effect = blurEf;
                                fields[x, y].Fill = Brushes.ForestGreen;
                            }
                        }
                    }
                }
                InvalidateVisual();
            }
        }

        public ChompMenu(System.Drawing.Size size, Chomp wrapper)
        {
            InitializeComponent();
            initializeRectangles(size);
            this.wrapper = wrapper;
            ((ChompBoard)this.wrapper.board).SquaresChanged += UpdateFields;
        }

        private void initializeRectangles(System.Drawing.Size size)
        {
            fields = new Rectangle[size.Width, size.Height];

            //define new columns and rows
            ChompGrid.ColumnDefinitions.Clear();
            for (int vCol = 0; vCol <= fields.GetLength(0) + 1; vCol++)
            {
                var col = new ColumnDefinition();
                col.Width = new GridLength(1, GridUnitType.Star);
                ChompGrid.ColumnDefinitions.Add(col);
            }
            ChompGrid.RowDefinitions.Clear();
            for (int hRow = 0; hRow <= fields.GetLength(1) + 1; hRow++)
            {
                var row = new RowDefinition();
                row.Height = new GridLength(1, GridUnitType.Star);
                ChompGrid.RowDefinitions.Add(row);
            }

            for (int x = 1; x <= fields.GetLength(0); x++)
            {
                for (int y = 1; y <= fields.GetLength(1); y++)
                {
                    var rec = new Rectangle() { Fill = Brushes.ForestGreen, Margin = new Thickness(1) };
                    rec.MouseEnter += ChompField_MouseEnter;
                    rec.MouseLeave += ChompField_MouseLeave;
                    rec.MouseDown += ChompField_MouseDown;
                    Grid.SetColumn(rec, x);
                    Grid.SetRow(rec, y);
                    ChompGrid.Children.Add(rec);
                    fields[x - 1, y - 1] = rec;
                }
            }
        }

        private void ChompField_MouseEnter(object sender, MouseEventArgs e)
        {
            if (active)
            {
                try
                {
                    int thisX = Grid.GetColumn(((Rectangle)sender));
                    int thisY = Grid.GetRow(((Rectangle)sender));
                    //Console.WriteLine("First substring: {0}", ((Rectangle)sender).Name.Substring(1, ((Rectangle)sender).Name.IndexOf("_") + 1));
                    //int thisX = int.Parse(((Rectangle)sender).Name.Substring(1, ((Rectangle)sender).Name.IndexOf("_") + 1));
                    //Console.WriteLine("Second substring: {0}", ((Rectangle)sender).Name.Substring(((Rectangle)sender).Name.LastIndexOf("_") + 1));
                    //int thisY = int.Parse(((Rectangle)sender).Name.Substring(((Rectangle)sender).Name.LastIndexOf("_") + 1));
                    //Console.WriteLine("Let's apply this...");

                    Brush previewColour = (thisX == 1 && thisY == 1 && (fields[0, 1] != null || fields[1, 0] != null)) ? Brushes.DarkRed : Brushes.DarkCyan;
                    for (int x = 0; x < fields.GetLength(0); x++)
                    {
                        for (int y = 0; y < fields.GetLength(1); y++)
                        {
                            if (fields[x, y] != null)
                            {
                                if (x < thisX - 1 || y < thisY - 1)
                                {
                                    fields[x, y].Fill = Brushes.ForestGreen;
                                }
                                else
                                {
                                    fields[x, y].Fill = previewColour;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not determine field indices from {0}. Reason: {1}", ((Rectangle)sender).Name, ex.Message);
                }
            }
        }
        private void ChompField_MouseLeave(object sender, MouseEventArgs e)
        {
            for (int x = 0; x < fields.GetLength(0); x++)
            {
                for (int y = 0; y < fields.GetLength(1); y++)
                {
                    if (fields[x, y] != null)
                    { fields[x, y].Fill = Brushes.ForestGreen; }                    
                }
            }
        }
        private void ChompField_MouseDown(object sender, MouseEventArgs e)
        {
            if (active)
            {
                int thisX = Grid.GetColumn(((Rectangle)sender));
                int thisY = Grid.GetRow(((Rectangle)sender));
                if (thisX == 1 && thisY == 1 && (fields[0, 1] != null || fields[1, 0] != null))
                {
                    MessageBox.Show("You may not remove this field while other fields remain.", "Last Field", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                wrapper.myChoice = new System.Drawing.Point(thisX - 1, thisY - 1);
                Active = false;
            }
        }

        private void UpdateFields(object sender, ChompBoard.SquaresChangedEventArgs e)
        {
            //this might update the fields incorrectly (compare to ChompBoard.Snap) -> testing needed!
            for (int y = e.Y; y < fields.GetLength(1); y++)
            {
                for (int x = e.X; x < fields.GetLength(0); x++)
                {
                    if (fields[x, y] != null)
                    {
                        fields[x, y].Effect = null;
                        fields[x, y].Fill = Brushes.Transparent;
                        fields[x, y] = null;
                    }
                    else
                    { break; }
                }
            }
            InvalidateVisual();
        }

        public void gameOver(Game.GameResult result, bool iWon)
        {
            switch (result)
            {
                case Game.GameResult.Aborted:
                    MessageBox.Show("A player has left or an error has occurred causing this game to be aborted. It counts as a draw.", "Chomp", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case Game.GameResult.Draw:
                    MessageBox.Show("The game ends as a draw.", "Chomp", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case Game.GameResult.PlayerWon:
                    if (iWon)
                    { MessageBox.Show("Congratulations, you have won the game!", "Chomp", MessageBoxButton.OK, MessageBoxImage.Information); }
                    else //won == false
                    { MessageBox.Show("Unfortunately, you have lost the game.", "Chomp", MessageBoxButton.OK, MessageBoxImage.Information); }
                    break;
                default:
                    break;
            }
            //if (won == null)
            //{ MessageBox.Show("A player has left or an error has occurred causing this game to be aborted. It counts as a draw.", "Chomp", MessageBoxButton.OK, MessageBoxImage.Information); }
            //else
            //{
            //    if (won == true)
            //    { MessageBox.Show("Congratulations, you have won the game!", "Chomp", MessageBoxButton.OK, MessageBoxImage.Information); }
            //    else //won == false
            //    { MessageBox.Show("Unfortunately, you have lost the game.", "Chomp", MessageBoxButton.OK, MessageBoxImage.Information); }
            //}
            Close();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            wrapper.myWindow = null; //signals code in wrapper to stop working
            wrapper = null;
            fields = null;
        }
    }
}
