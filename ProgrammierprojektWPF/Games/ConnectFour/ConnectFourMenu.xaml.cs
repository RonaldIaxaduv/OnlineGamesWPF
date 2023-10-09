using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ProgrammierprojektWPF
{
    /// <summary>
    /// Interaction logic for ConnectFourMenu.xaml
    /// </summary>
    public partial class ConnectFourMenu : Window
    {
        private Rectangle[,] fields;
        private GeometryGroup[] columnOverlayGeometries;
        private Path[] columnOverlays;

        private ConnectFour wrapper;
        private Brush[] playerColours = { Brushes.DarkRed, Brushes.DarkBlue };

        private bool active = false;
        public bool Active
        {
            get { return active; }
            set
            {
                active = value;
                if (value) //active -> normal shapes
                {
                    for (int x = 0; x < fields.GetLength(0); x++)
                    {
                        for (int y = 0; y < fields.GetLength(1); y++)
                        {
                            if (fields[x, y] != null)
                            { fields[x, y].Effect = null; }
                        }
                    }
                    for (int column = 0; column < columnOverlays.GetLength(0); column++)
                    {
                        columnOverlays[column].Effect = null;
                    }
                }
                else //inactive -> blurred shapes
                {
                    var blurEf = new System.Windows.Media.Effects.BlurEffect() { Radius = 5, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance, KernelType = System.Windows.Media.Effects.KernelType.Box };
                    for (int x = 0; x < fields.GetLength(0); x++)
                    {
                        for (int y = 0; y < fields.GetLength(1); y++)
                        {
                            if (fields[x, y] != null)
                            {
                                fields[x, y].Effect = blurEf;
                            }
                        }
                    }
                    for (int column = 0; column < fields.GetLength(0); column++)
                    {
                        columnOverlays[column].Effect = blurEf;
                    }                    
                }
                InvalidateVisual();
            }
        }

        public ConnectFourMenu(System.Drawing.Size size, ConnectFour wrapper)
        {
            InitializeComponent();
            initializeShapes(size);
            this.wrapper = wrapper;
            ((ConnectFourBoard)this.wrapper.board).SquaresChanged += UpdateFields;
        }

        private void initializeShapes(System.Drawing.Size size)
        {
            fields = new Rectangle[size.Width, size.Height];

            //define new columns and rows
            ConnectFourGrid.ColumnDefinitions.Clear();
            for (int vCol = 0; vCol <= fields.GetLength(0) + 1; vCol++)
            {
                var col = new ColumnDefinition();
                col.Width = new GridLength(1, GridUnitType.Star);
                ConnectFourGrid.ColumnDefinitions.Add(col);
            }
            ConnectFourGrid.RowDefinitions.Clear();
            for (int hRow = 0; hRow <= fields.GetLength(1) + 1; hRow++)
            {
                var row = new RowDefinition();
                row.Height = new GridLength(1, GridUnitType.Star);
                ConnectFourGrid.RowDefinitions.Add(row);
            }

            //draw overlay (geometry groups)
            columnOverlays = new Path[size.Width];
            columnOverlayGeometries = new GeometryGroup[size.Width];
            for (int column = 0; column < size.Width; column++)
            {
                columnOverlays[column] = new Path() { Stroke = Brushes.Black, StrokeThickness = 3, Fill = Brushes.ForestGreen };
                columnOverlayGeometries[column] = new GeometryGroup() { FillRule = FillRule.EvenOdd }; //FillRule.EvenOdd makes it so that the circles are cut out instead of being drawn over the rectangle

                var recGeo = new RectangleGeometry() { Rect = new Rect(0, 0, 1, size.Height) };
                columnOverlayGeometries[column].Children.Add(recGeo);

                for (int row = 0; row < size.Height; row++)
                {
                    var circGeo = new EllipseGeometry() { Center = new Point(0.5, row + 0.5), RadiusX = 0.4, RadiusY = 0.4 };
                    columnOverlayGeometries[column].Children.Add(circGeo);
                }

                //columnOverlayGeometries[column].Transform = new ScaleTransform() { ScaleX = Width / (fields.GetLength(0) + 2), ScaleY = Height / (fields.GetLength(1) + 2) };
                //geoGrp.Transform = columnTransformations[column];
                //^- this doesn't quite work yet but it looks promising. the columns aren't resized properly yet (not resized enough).
                //columnOverlayGeometries[column].RenderTransform = new ScaleTransform() { ScaleX = Width / (size.Width + 2), ScaleY = Height / (size.Height + 2) };
                //^- this seems to break stuff
                //columnOverlayGeometries[column].LayoutTransform = new ScaleTransform() { ScaleX = Width / (size.Width + 2), ScaleY = Height / (size.Height + 2) };
                //^- this also seems to break stuff
                columnOverlays[column].Data = columnOverlayGeometries[column];
                Grid.SetColumn(columnOverlays[column], column + 1);
                Grid.SetRow(columnOverlays[column], 1);
                Grid.SetRowSpan(columnOverlays[column], size.Height);
                Grid.SetZIndex(columnOverlays[column], 1); //makes it so that all columns are displayed in front of all pieces (which all have the default z index of 0)
                ConnectFourGrid.Children.Add(columnOverlays[column]);

                columnOverlays[column].MouseEnter += Column_MouseEnter;
                columnOverlays[column].MouseLeave += Column_MouseLeave;
                columnOverlays[column].MouseDown += Column_MouseDown;
            }
            setColumnTransforms();
        }

        private void Column_MouseEnter(object sender, MouseEventArgs e)
        {
            if (active)
            {
                int column = Grid.GetColumn(((Path)sender)) - 1;

                var cfBoard = ((ConnectFourBoard)wrapper.board).squares;
                for (int row = cfBoard.GetLength(1) - 1; row >= 0; row--)
                {
                    if (cfBoard[column, row] < 0)
                    {
                        fields[column, row] = new Rectangle() { Fill = Brushes.DarkViolet, Margin = new Thickness(2) };
                        Grid.SetColumn(fields[column, row], column + 1);
                        Grid.SetRow(fields[column, row], row + 1);
                        ConnectFourGrid.Children.Add(fields[column, row]);
                        break;
                    }
                }
            }
        }
        private void Column_MouseLeave(object sender, MouseEventArgs e)
        {
            for (int x = 0; x < fields.GetLength(0); x++)
            {
                for (int y = 0; y < fields.GetLength(1); y++)
                {
                    if (fields[x, y] != null && !playerColours.Contains(fields[x, y].Fill))
                    {
                        fields[x, y].Fill = Brushes.Transparent;
                        fields[x, y] = null;
                    }
                }
            }
        }
        private void Column_MouseDown(object sender, MouseEventArgs e)
        {
            if (active)
            {
                int column = Grid.GetColumn(((Path)sender)) - 1;
                if (((ConnectFourBoard)wrapper.board).squares[column, 0] >= 0)
                {
                    MessageBox.Show("This column is already full.", "Column Full", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                wrapper.myChoice = column;
                Console.WriteLine("myChoice set to {0}.", column);
                Active = false;
            }
        }

        private void UpdateFields(object sender, ConnectFourBoard.SquaresChangedEventArgs e)
        {
            if (fields != null)
            {
                if (fields[e.X, e.Y] != null)
                {
                    fields[e.X, e.Y].Effect = null;
                    fields[e.X, e.Y].Fill = Brushes.Transparent;
                    fields[e.X, e.Y] = null;
                }
                if (e.PlayerNumber >= 0)
                {
                    fields[e.X, e.Y] = new Rectangle() { Fill = playerColours[e.PlayerNumber], Margin = new Thickness(2) };
                    Grid.SetColumn(fields[e.X, e.Y], e.X + 1);
                    Grid.SetRow(fields[e.X, e.Y], e.Y + 1);
                    ConnectFourGrid.Children.Add(fields[e.X, e.Y]);
                }
                InvalidateVisual();
            }
        }
        private void setColumnTransforms()
        {
            //this method changes the overlay geometries' transforms depending on the grid's size (since the grid's size equals the window's client size - ActualWidth/ActualHeight of the window control don't yield the correct values for some reason)

            if (fields == null)
            { return; }

            UpdateLayout(); //ensures that all contents have been rendered -> size of grid has been updated
            for (int column = 0; column < fields.GetLength(0); column++)
            {
                columnOverlayGeometries[column].Transform = new ScaleTransform() { ScaleX = ConnectFourGrid.ActualWidth / (fields.GetLength(0) + 2), ScaleY = ConnectFourGrid.ActualHeight / (fields.GetLength(1) + 2) }; //ScaleY is still off. +2 makes it too tall, +3 too narrow for some reason
            }
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            setColumnTransforms();
            InvalidateVisual();
        }

        public void gameOver(Game.GameResult result, bool iWon)
        {
            switch (result)
            {
                case Game.GameResult.Aborted:
                    MessageBox.Show("A player has left or an error has occurred causing this game to be aborted. It counts as a draw.", "Connect Four", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case Game.GameResult.Draw:
                    MessageBox.Show("The game ends as a draw.", "Connect Four", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case Game.GameResult.PlayerWon:
                    if (iWon)
                    { MessageBox.Show("Congratulations, you have won the game!", "Connect Four", MessageBoxButton.OK, MessageBoxImage.Information); }
                    else //won == false
                    { MessageBox.Show("Unfortunately, you have lost the game.", "Connect Four", MessageBoxButton.OK, MessageBoxImage.Information); }
                    break;
                default:
                    break;
            }
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
