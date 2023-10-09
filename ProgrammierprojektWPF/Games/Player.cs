using System;
using System.Net.Sockets;
using System.Net;

namespace ProgrammierprojektWPF
{
    [Serializable]
    public class Player
    {
        private string name = "";
        public string Name
        {
            get
            {
                switch (type)
                {
                    case playerType.LocalHuman:
                        return connectionToServer?.username ?? name;
                    case playerType.RemoteHuman:
                        return connectionToClient?.username ?? name;
                    default:
                        return name;
                }
            }
        }
        public enum playerType { LocalHuman, RemoteHuman, LocalComputer, RemoteComputer };
        public playerType type;
        [NonSerialized] public ServerClientConnection connectionToClient = null;
        [NonSerialized] public Client connectionToServer = null;

        public Player(ServerClientConnection connectionToClient)
        {
            type = playerType.RemoteHuman;
            this.connectionToClient = connectionToClient;
            name = Name; //sets name through connection
        }
        public Player(Client connectionToServer)
        {
            type = playerType.LocalHuman;
            this.connectionToServer = connectionToServer;
            name = Name; //sets name through connection
        }
        public Player(playerType type, string name)
        {
            if (type == playerType.LocalComputer || type == playerType.RemoteComputer)
            { throw new ArgumentException("This constructor is intended to only be used for human players."); }
            this.type = type;
            this.name = name;
        }
        public Player(playerType type)
        {
            if (type == playerType.LocalHuman || type == playerType.RemoteHuman)
            { throw new ArgumentException("This constructor is intended to only be used for computer players."); }
            this.type = type;
            name = "Computer";
        }
    }

}