using System;
using System.Drawing;

namespace ProgrammierprojektWPF
{

    public class ChompBoard : Board
    {
        public bool[,] squares;
        public class SquaresChangedEventArgs
        {
            public int X { get; private set; }
            public int Y { get; private set; }
            public SquaresChangedEventArgs(int x, int y) { X = x; Y = y; }
        }
        public delegate void SquaresChangedEventHandler(object sender, SquaresChangedEventArgs e);
        public event SquaresChangedEventHandler SquaresChanged;

        public ChompBoard(Size boardSize)
        {
            if (boardSize.Width * boardSize.Height < 4)
            { throw new Exception("The specified size of the board is too small."); }

            this.size = boardSize;
            squares = new bool[size.Width, size.Height];
            for (int x = 0; x < size.Width; x++)
            {
                for (int y = 0; y < size.Height; y++)
                {
                    squares[x, y] = true; //set all squares to be active
                }
            }
        }

        public void snap(Point point)
        {
            if (point.X < 0 || point.Y < 0 || point.X > this.size.Width - 1 || point.Y > this.size.Height - 1)
            { throw new IndexOutOfRangeException("Invalid point on the board."); }

            if (!squares[point.X, point.Y])
            { throw new Exception("The selected square has already been removed."); }

            if (point == new Point(0, 0) && (squares[0, 1] || squares[1, 0])) //if the two squares next to the top-left one haven't been removed, there are always other targettable squares left
            { throw new Exception("The top-left square can only be targeted after all other squares have been removed."); }

            Console.WriteLine("Snapping board at " + point);

            for (int x = point.X; x < size.Width; x++)
            {
                for (int y = point.Y; y < size.Height; y++)
                {
                    if (squares[x, y])
                    { squares[x, y] = false; } //set all squares to be active
                    else
                    { continue; } //all squares behind one that has already been broken off are false -> skip to next line
                }
            }

            SquaresChanged?.Invoke(this, new SquaresChangedEventArgs(point.X, point.Y));
        }
    }

}