using System;
using System.Drawing;

namespace ProgrammierprojektWPF
{

    public class ConnectFourBoard : Board
    {
        public sbyte[,] squares;
        public class SquaresChangedEventArgs
        {
            public int X { get; private set; }
            public int Y { get; private set; }
            public int PlayerNumber { get; private set; }
            public SquaresChangedEventArgs(int x, int y, int playerNumber) { X = x; Y = y; PlayerNumber = playerNumber; }
        }
        public delegate void SquaresChangedEventHandler(object sender, SquaresChangedEventArgs e);
        public event SquaresChangedEventHandler SquaresChanged;

        public ConnectFourBoard(Size boardSize)
        {
            if (boardSize.Width < 4 && boardSize.Height < 4)
            { throw new ArgumentException("The board must be at least 4 units wide or tall."); }

            squares = new sbyte[boardSize.Width, boardSize.Height];
            for (int x = 0; x < boardSize.Width; x++)
            {
                for (int y = 0; y < boardSize.Height; y++)
                {
                    squares[x, y] = -1; //set all squares to be empty
                }
            }
        }

        public Point PlaceInColumn(int columnIndex, sbyte playerIndex)
        {
            //incorrect user input / no such column
            if (columnIndex >= squares.GetLength(0) || columnIndex < 0)
            { throw new ArgumentOutOfRangeException($"Invalid column index ({columnIndex} whereas the actual indices range from 0 to {squares.GetLength(0)}.)"); }
            else if (squares[columnIndex, 0] >= 0)
            { throw new ArgumentException($"The selected column (index: {columnIndex}) is already full."); }
            if (playerIndex < 0)
            { throw new ArgumentException($"Invalid player index (negative)."); }

            //going through column bottom to top
            for (int row = squares.GetLength(1) - 1; row >= 0 ; row--)
            {
                if(squares[columnIndex, row] < 0)
                {
                    squares[columnIndex, row] = playerIndex;
                    SquaresChanged?.Invoke(this, new ConnectFourBoard.SquaresChangedEventArgs(columnIndex, row, playerIndex));
                    return new Point(columnIndex, row);
                }
            }

            throw new Exception("The column could not be filled.");
        }
    }
}
