using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace ProgrammierprojektWPF
{

    [Serializable]
    public class ChompSpecificationsPortable
    {
        private Size boardSize;
        public Size BoardSize
        {
            get { return boardSize; }
            private set
            {
                if (value.Width * value.Height <= 4) throw new ArgumentException("Chomp boards need to consist of at least 4 fields.");
                boardSize = value;
            }
        }

        private string[] playerNames;
        public string[] PlayerNames
        {
            get { return playerNames; }
            private set
            {
                if (value.Length != 2) throw new ArgumentException("Chomp can only be played by 2 players.");
                if (value[0] == value[1]) throw new ArgumentException("The names of the two players cannot be equal (players cannot play against themselves).");
                playerNames = value;
            }
        }

        private int first;
        public int First
        {
            get { return first; }
            private set
            {
                if (value != 0 && value != 1) throw new ArgumentException("Only the first (0) or second (1) player can be chosen as the value of First.");
                first = value;
            }
        }

        public ChompSpecificationsPortable(Size boardSize, string[] playerNames, int first)
        {
            BoardSize = boardSize;
            PlayerNames = playerNames;
            First = first;
        }
    }

    public class ChompSpecifications
    {
        private Game.GameLocation gameLocation;
        public Game.GameLocation GameLocation
        {
            get { return gameLocation; }
            private set
            {
                switch (value)
                {
                    case Game.GameLocation.Server:
                        if (Players.Length > 0)
                        {
                            foreach (Player player in Players)
                            {
                                if (player.type == Player.playerType.LocalHuman)
                                { throw new ArgumentException("Local non-computer users are not supported for Chomp servers."); }
                                else if ((player.type == Player.playerType.RemoteHuman || player.type == Player.playerType.RemoteComputer) && player.connectionToClient == null)
                                { throw new ArgumentException("All remote users must own a connection to a client."); }
                            }
                        }
                        if (serverWrapper == null)
                        { throw new ArgumentException("Chomp server require a server wrapper."); }
                        break;

                    case Game.GameLocation.Client:
                        if (Players.Length > 0)
                        {
                            foreach (Player player in Players)
                            {
                                /*if ((player.type == Player.playerType.RemoteHuman || player.type == Player.playerType.RemoteComputer) && clientWrapper == null)
                                { throw new ArgumentException("A connection to the server must be added to support remote users."); }
                                else*/ if (player.type == Player.playerType.LocalHuman && player.connectionToServer == null)
                                { throw new ArgumentException("All local users need a connection to the client."); }
                            }
                        }
                        if (clientWrapper == null)
                        { throw new ArgumentException("Chomp clients require a client wrapper."); }
                        break;

                    case Game.GameLocation.Local:
                        if (Players.Length > 0)
                        {
                            foreach (Player player in Players)
                            {
                                if (player.type == Player.playerType.RemoteComputer || player.type == Player.playerType.RemoteHuman)
                                { throw new ArgumentException("Remote users are not supported for local Chomp games."); }
                            }
                        }
                        break;

                    default:
                        break;
                }
                gameLocation = value;
            }
        }
        public Client clientWrapper = null; //needed for GameLocation == Client
        public Server serverWrapper = null; //needed for GameLocation == Server

        private Size boardSize;
        public Size BoardSize
        {
            get { return boardSize; }
            private set
            {
                if (value.Width * value.Height <= 4) throw new ArgumentException("Chomp boards need to consist of at least 4 fields.");
                boardSize = value;
            }
        }

        private Player[] players;
        public Player[] Players
        {
            get { return players; }
            private set
            {
                if (value.Length != 2) throw new ArgumentException("Chomp can only be played by 2 players.");
                if (value[0] == value[1]) throw new ArgumentException("The names of the two players cannot be equal (players cannot play against themselves).");
                players = value;
            }
        }

        private int first = -1;
        public int First
        {
            get { return first; }
            private set
            {
                if (value != 0 && value != 1) throw new ArgumentException("Only the first (0) or second (1) player can be chosen as the value of First.");
                first = value;
            }
        }

        public ChompSpecifications(Game.GameLocation gameLocation, Size boardSize, Player[] players, int first, Client clientWrapper = null, Server serverWrapper = null)
        {
            BoardSize = boardSize;
            Players = players;
            First = first;
            this.clientWrapper = clientWrapper;
            this.serverWrapper = serverWrapper;
            GameLocation = gameLocation; //needs to come last because of exception handling
        }
        public ChompSpecifications(ChompSpecificationsPortable pSpecs, Game.GameLocation gameLocation, Player[] players, Client clientWrapper = null, Server serverWrapper = null)
        {
            BoardSize = pSpecs.BoardSize;
            Players = players;
            First = pSpecs.First;
            this.clientWrapper = clientWrapper;
            this.serverWrapper = serverWrapper;
            GameLocation = gameLocation; //needs to come last because of exception handling
        }

        public override string ToString()
        {
            return GameLocation.ToString() +
                ":\nSize: " +
                BoardSize.ToString() +
                "\nPlayers: " + (Players.Length == 2 ? Players[0].type.ToString() + ": " + Players[0].Name + " ; " + Players[1].type.ToString() + ": " + Players[1].Name : "none") +
                "\nFirst: " + First.ToString() +
                "\nClient wrapper: " + (clientWrapper != null ? "yes" : "no") +
                "\nServer wrapper: " + (serverWrapper != null ? "yes" : "no");
        }
    }

}
