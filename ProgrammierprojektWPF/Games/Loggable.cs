using System;
using System.Drawing;
using System.Collections.Generic;

namespace ProgrammierprojektWPF
{

    public interface Loggable
    {
        Stack<TurnData> TurnStack { get; set; }

        void addTurn(Player player, Point coords);
        void removeTurn();
    }

    public struct TurnData
    {
        public Player player;
        public Point coords;

        public TurnData(Player player, Point coords)
        {
            this.player = player;
            this.coords = coords;
        }
    }

    [Serializable]
    public struct TurnDataPortable
    {
        public string recipient; //name of the user this data should be forwarded to

        public string playerName;
        public Point coords;

        public TurnDataPortable(string recipient, string playerName, Point coords)
        {
            this.recipient = recipient;
            this.playerName = playerName;
            this.coords = coords;
        }
    }
}