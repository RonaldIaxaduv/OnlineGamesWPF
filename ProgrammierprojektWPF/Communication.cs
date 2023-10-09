using System;
using System.Net.Sockets;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Communication
{
    
    public static class Commands
    {

        public enum ClientCommands
        {
            RequestCompareVersionNumber,
            SubmitUserName,
            SubmitPassword_ExistingUser,
            SubmitPassword_NewUser,
            ClientError,

            RequestOnlineUserList,
            RequestSendMessage,

            SubmitTurnData,

            RequestGame_ConnectFour,
            RequestGame_Chomp,
            SubmitRequestAccepted,
            SubmitRequestDenied,

            RequestConnectionVerification,
            AbortGame,

            SubmitClientInfo
        }
        public static uint getClientCommandUInt(ClientCommands cCom)
        {
            switch (cCom)
            {
                case ClientCommands.RequestCompareVersionNumber: return 0;
                case ClientCommands.SubmitUserName: return 1;
                case ClientCommands.SubmitPassword_ExistingUser: return 2;
                case ClientCommands.SubmitPassword_NewUser: return 3;
                case ClientCommands.ClientError: return 4;

                case ClientCommands.RequestOnlineUserList: return 5;
                case ClientCommands.RequestSendMessage: return 6;

                case ClientCommands.SubmitTurnData: return 7;

                case ClientCommands.RequestGame_ConnectFour: return 8;
                case ClientCommands.RequestGame_Chomp: return 9;
                case ClientCommands.SubmitRequestAccepted: return 10;
                case ClientCommands.SubmitRequestDenied: return 11;

                case ClientCommands.RequestConnectionVerification: return 12;
                case ClientCommands.AbortGame: return 13;

                case ClientCommands.SubmitClientInfo: return 14;

                default: throw new Exception("Unknown client command (" + cCom + ").");
            }
        }
        public static ClientCommands getClientCommand(uint cCom)
        {
            switch (cCom)
            {
                case 0: return ClientCommands.RequestCompareVersionNumber;
                case 1: return ClientCommands.SubmitUserName;
                case 2: return ClientCommands.SubmitPassword_ExistingUser;
                case 3: return ClientCommands.SubmitPassword_NewUser;
                case 4: return ClientCommands.ClientError;

                case 5: return ClientCommands.RequestOnlineUserList;
                case 6: return ClientCommands.RequestSendMessage;

                case 7: return ClientCommands.SubmitTurnData;

                case 8: return ClientCommands.RequestGame_ConnectFour;
                case 9: return ClientCommands.RequestGame_Chomp;
                case 10: return ClientCommands.SubmitRequestAccepted;
                case 11: return ClientCommands.SubmitRequestDenied;

                case 12: return ClientCommands.RequestConnectionVerification;
                case 13: return ClientCommands.AbortGame;

                case 14: return ClientCommands.SubmitClientInfo;

                default: throw new Exception("Unknown client command number (" + cCom + ").");
            }
        }


        public enum ServerCommands
        {
            ValidVersionNumber,
            InvalidVersionNumber,
            InvalidUserName,
            UserAlreadyOnline,
            UserNameFound,
            UserNameNotFound,
            PasswordIncorrect,
            LoginSuccessful,
            AccountCreatedSuccessfully,
            ServerError,

            MessageSent,
            MessageForwarded,
            ReceiveString,
            ReceiveList_String,
            OnlineUsersChanged,

            ReceiveTurnData,

            RequestAccepted,
            RequestDenied,
            RequestGameReply_ConnectFour,
            RequestGameReply_Chomp,

            ConnectionValid,
            GameAborted,

            RequestClientInfo,
            AlreadyIngame
        }
        public static uint getServerCommandUInt(ServerCommands sCom)
        {
            switch (sCom)
            {
                case ServerCommands.ValidVersionNumber: return 0;
                case ServerCommands.InvalidVersionNumber: return 1;
                case ServerCommands.InvalidUserName: return 2;
                case ServerCommands.UserAlreadyOnline: return 3;
                case ServerCommands.UserNameFound: return 4;
                case ServerCommands.UserNameNotFound: return 5;
                case ServerCommands.PasswordIncorrect: return 6;
                case ServerCommands.LoginSuccessful: return 7;
                case ServerCommands.AccountCreatedSuccessfully: return 8;
                case ServerCommands.ServerError: return 9;

                case ServerCommands.MessageSent: return 10;
                case ServerCommands.MessageForwarded: return 11;
                case ServerCommands.ReceiveString: return 12;
                case ServerCommands.ReceiveList_String: return 13;
                case ServerCommands.OnlineUsersChanged: return 14;

                case ServerCommands.ReceiveTurnData: return 15;

                case ServerCommands.RequestAccepted: return 16;
                case ServerCommands.RequestDenied: return 17;
                case ServerCommands.RequestGameReply_ConnectFour: return 18;
                case ServerCommands.RequestGameReply_Chomp: return 19;

                case ServerCommands.ConnectionValid: return 20;
                case ServerCommands.GameAborted: return 21;

                case ServerCommands.RequestClientInfo: return 22;
                case ServerCommands.AlreadyIngame: return 23;

                default: throw new Exception("Unknown server command (" + sCom + ").");
            }
        }
        public static ServerCommands getServerCommand(uint sCom)
        {
            switch (sCom)
            {
                case 0: return ServerCommands.ValidVersionNumber;
                case 1: return ServerCommands.InvalidVersionNumber;
                case 2: return ServerCommands.InvalidUserName;
                case 3: return ServerCommands.UserAlreadyOnline;
                case 4: return ServerCommands.UserNameFound;
                case 5: return ServerCommands.UserNameNotFound;
                case 6: return ServerCommands.PasswordIncorrect;
                case 7: return ServerCommands.LoginSuccessful;
                case 8: return ServerCommands.AccountCreatedSuccessfully;
                case 9: return ServerCommands.ServerError;

                case 10: return ServerCommands.MessageSent;
                case 11: return ServerCommands.MessageForwarded;
                case 12: return ServerCommands.ReceiveString;
                case 13: return ServerCommands.ReceiveList_String;
                case 14: return ServerCommands.OnlineUsersChanged;

                case 15: return ServerCommands.ReceiveTurnData;

                case 16: return ServerCommands.RequestAccepted;
                case 17: return ServerCommands.RequestDenied;
                case 18: return ServerCommands.RequestGameReply_ConnectFour;
                case 19: return ServerCommands.RequestGameReply_Chomp;

                case 20: return ServerCommands.ConnectionValid;
                case 21: return ServerCommands.GameAborted;

                case 22: return ServerCommands.RequestClientInfo;
                case 23: return ServerCommands.AlreadyIngame;

                default: throw new Exception("Unknown server command number (" + sCom + ").");
            }
        }
    }


    [Serializable()]
    public class ChatMessage
    {
        public string sender, msg, recipient;
        public bool global; //determines whether message is broadcast (to all online users) or only sent to one specific user

        public ChatMessage(string sender, string msg, string recipient)
        {
            this.sender = sender; this.msg = msg; this.recipient = recipient;
            global = false;
        }
        public ChatMessage(string sender, string msg) : this(sender, msg, "")
        { global = true; }
    }


    public static class Connection
    {
        public static bool isConnected(Socket socket)
        {
            //there is no direct method to check whether a client has disconnected, but this works (see https://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket )
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0); //socket isn't readable or has data available to be read
            }
            catch (SocketException) { return false; }
        }
    }

    public static class ListBoxUserItem
    {
        private delegate int AdderDelegate(object newItem);
        public static object generate(double FontSize, string username, bool online)
        {
            var gr = new Grid(); //this is going to contain the image and the textblock
            var cd = new ColumnDefinition();            
            gr.ColumnDefinitions.Add(cd);
            cd = new ColumnDefinition();
            gr.ColumnDefinitions.Add(cd);

            //add image showing whether the user is online or offline
            //see a mixture of https://wpf.2000things.com/tag/resources/ (explains how to put resource images into the executable file as a resource) and
            //https://social.msdn.microsoft.com/Forums/en-US/ca3e305d-7a1e-480d-917f-5ac3eb7f3c1f/wpf-image-from-resource?forum=wpf (which, of a billion pages, was the only one that (almost) gave a correct path for the uri...)
            Image img;
            if (online)
            { img = new Image() { Width = FontSize + 4, Height = FontSize + 4, Source = new BitmapImage(new Uri("pack://application:,,,/ProgrammierprojektWPF;component/Resources/Light_green.png")) }; } //green light
            else
            { img = new Image() { Width = FontSize + 4, Height = FontSize + 4, Source = new BitmapImage(new Uri("pack://application:,,,/ProgrammierprojektWPF;component/Resources/Light_red.png")) }; } //red light
            Grid.SetColumn(img, 0);
            gr.Children.Add(img);

            //add username (contained by textblock) formatted like in the chat listbox
            var tb = new TextBlock();
            tb.Inlines.Add(new Run(" " + username) { Foreground = Brushes.DarkCyan, FontWeight = FontWeights.UltraBlack });
            Grid.SetColumn(tb, 1);
            gr.Children.Add(tb);

            return gr;
        }
    }

    public static class ListBoxChatItem
    {
        private delegate int AdderDelegate(object newItem);
        public static object generate(string username, ChatMessage msg)
        {
            var tb = new TextBlock(); //this will contain the message as formatted text (formatting is done using Run to create inlines)

            //make sender bold and green (or red for server messages)
            Run rn;
            if (msg.sender.ToLower() == "server")
            { rn = new Run(msg.sender) { Foreground = Brushes.DarkRed, FontWeight = FontWeights.UltraBlack }; }
            else
            { rn = new Run(msg.sender) { Foreground = Brushes.DarkGreen, FontWeight = FontWeights.UltraBlack }; }
            tb.Inlines.Add(rn);

            //make recipient bold and cyan (not for global messages)
            if (!msg.global)
            {
                rn = new Run(" [to ") { Foreground = Brushes.DarkGray, FontWeight = FontWeights.DemiBold };
                tb.Inlines.Add(rn);
                rn = new Run(msg.recipient) { Foreground = Brushes.DarkCyan, FontWeight = FontWeights.UltraBlack };
                tb.Inlines.Add(rn);
                rn = new Run("]: ") { Foreground = Brushes.DarkGray, FontWeight = FontWeights.DemiBold };
                tb.Inlines.Add(rn);
            }
            else
            {
                rn = new Run(": ") { Foreground = Brushes.DarkGray, FontWeight = FontWeights.DemiBold };
                tb.Inlines.Add(rn);
            }

            //make message black and italic
            rn = new Run(msg.msg) { Foreground = Brushes.Black, FontWeight = FontWeights.Regular, FontStyle = FontStyles.Italic };
            tb.Inlines.Add(rn);

            return tb;
        }
    }

    public static class VersionNumber
    {
        public const string Major = "1";
        public const string Minor = "3";
        public const string Build = "0";
        public const string Revision = "0";

        public static string get()
        { return Major + "." + Minor + "." + Build + "." + Revision; }
    }
}