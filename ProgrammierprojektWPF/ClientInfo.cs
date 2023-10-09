using System;

namespace ProgrammierprojektWPF
{
    [Serializable]
    public class ClientInfo
    {
        public Client.IngameStatus ingameStatus = Client.IngameStatus.NotIngame;
        public string ingameOpponent = "";

        public ClientInfo(Client.IngameStatus ingameStatus, string ingameOpponent = "")
        {
            this.ingameStatus = ingameStatus;
            this.ingameOpponent = ingameOpponent;
        }

        public string displayIngameStatus()
        {
            switch (ingameStatus)
            {
                case Client.IngameStatus.NotIngame:
                    return "Not ingame.";
                case Client.IngameStatus.Chomp:
                    return $"Playing Chomp against {ingameOpponent}.";
                case Client.IngameStatus.ChompSolo:
                    return "Playing Chomp (solo).";
                case Client.IngameStatus.ConnectFour:
                    return $"Playing Connect Four against {ingameOpponent}.";
                case Client.IngameStatus.ConnectFourSolo:
                    return "Playing Connect Four (solo).";
                default:
                    return "";
            }
        }
    }
}
