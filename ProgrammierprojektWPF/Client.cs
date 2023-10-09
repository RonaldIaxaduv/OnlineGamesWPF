using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using Communication;

namespace ProgrammierprojektWPF
{
    public sealed class Client : IDisposable
    {
        public Socket sender;
        private NetworkStream netS; private BufferedStream bufS;
        private const int bufferSize = 1000;

        public List<Commands.ServerCommands> serverMsgs = new List<Commands.ServerCommands>();
        public object receivedData;

        public string username = "";

        public enum IngameStatus
        {
            NotIngame,
            ConnectFour,
            ConnectFourSolo,
            Chomp,
            ChompSolo
        }
        private IngameStatus ingameInf = IngameStatus.NotIngame;
        public string opponent = "";
        public IngameStatus IngameInf
        {
            get { return ingameInf; }
            set
            {
                ingameInf = value;
                Console.WriteLine("New ingame status: {0}", value);
            }
        }
        public ClientMenu myWindow;

        public Client()
        {
            myWindow = new ClientMenu(this);
        }


        //basic behaviour
        public async Task startClient()
        {
            await Task.Delay(100); //wait so that MainWindow can close

            //get IP address and endpoint
            IPAddress ipAddress;
            IPEndPoint remoteEP;
            if (!setupEndpoint(out ipAddress, out remoteEP))
            {
                Dispose();
                return;
            }

            sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            await Task.WhenAny(connect(remoteEP), Task.Delay(10000));
            if (!sender.Connected) //server takes more than 10 seconds to respond
            {
                MessageBox.Show("The server takes too long to respond. Please check your internet connection and firewall settings, as well as the status of the server.", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                Dispose();
                return;
            }

            //prepare remaining tools needed for execution of client
            netS = new NetworkStream(sender);
            bufS = new BufferedStream(netS, bufferSize);

            using (Task listenTask = listen())
            {
                if (!(await requestCompareVersionNumber()))
                {
                    MessageBox.Show("The version number of this client and the server don't match. Please check that both of you use the latest version.", "Invalid Version Number", MessageBoxButton.OK, MessageBoxImage.Error);
                    Dispose();
                    return;
                }

                //login or create new account (closes window if canceled)
                if (!(await login()))
                {
                    Dispose();
                    return;
                }

                await Task.WhenAny(listenTask, ShowAsync()); //this ends if the connection is severed in any way (ClientMenu closed, internet connection failure, ...)
            }

            Dispose();
        }
        private bool setupEndpoint(out IPAddress ipAddress, out IPEndPoint remoteEP)
        {
            ipAddress = null; remoteEP = null;

            IPWindow windIP = new IPWindow();
            if (windIP.ShowDialog() == true) //only happens when the confirm button is pressed
            {
                if (windIP.tbIP.Text == "" || windIP.tbPort.Text == "")
                {
                    MessageBox.Show("The entered endpoint was incomplete.", "Incomplete Endpoint", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    try
                    {
                        ipAddress = IPAddress.Parse(windIP.tbIP.Text);
                        remoteEP = new IPEndPoint(ipAddress, int.Parse(windIP.tbPort.Text));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error has occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            return false;
        }
        private async Task connect(IPEndPoint remoteEP)
        {
            sender.Connect(remoteEP);
        }
        private async Task ShowAsync()
        {
            myWindow.ShowDialog();
        }

        private async Task listen()
        {
            /*this method can receive ANY type of data through the stream. it is based on: https://stackoverflow.com/questions/2316397/sending-and-receiving-custom-objects-using-tcpclient-class-in-c-sharp
            how it works:
            - the stream first receives 8 byte (2x32 bit) containing a command code which tells the program what type of object will be received and the size of the data that will be received
            - the task which will handle the received data will be determined beforehand, so that the reading process doesn't need to be executed in separate methods
            - finally, the object data is read from the stream and can be cast on a variable of the expected type
            */

            byte[] comBlock = new byte[8];
            int bytesRec = 0;

            //continuously read incoming confirmation until clients shuts down
            while (Connection.isConnected(sender))
            {
                if (netS.DataAvailable)
                {
                    Console.WriteLine("New data is available.");
                    while (bytesRec < comBlock.Length)
                    { bytesRec += await netS.ReadAsync(comBlock, bytesRec, comBlock.Length - bytesRec); }
                    Console.WriteLine("Data has been read.");

                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(comBlock, 0, 4);
                        Array.Reverse(comBlock, 4, 4);
                    }
                    uint command = BitConverter.ToUInt32(comBlock, 0); //first 4 bytes (more isn't read): command, i.e. what kind of object will be received
                    int dataSize = BitConverter.ToInt32(comBlock, 4); //second 4 bytes: length of the upcoming message (arrays can only have a size of int, so sending a uint wouldn't work)

                    await handleMessage(Commands.getServerCommand(command), dataSize);

                    //reset
                    bytesRec = 0;
                }
                //await Task.Delay(500).ConfigureAwait(false); //check for new messages after a short delay
                await Task.Delay(500);
            }
            MessageBox.Show("The connection with the server has been severed.", "Connection Severed", MessageBoxButton.OK, MessageBoxImage.Information);
            myWindow.Close(); //this is necessary somehow
        }

        //information processing, client-server communication
        private async Task handleMessage(Commands.ServerCommands sCom, int dataSize)
        {
            Console.WriteLine("Handling message... {0}, {1}", sCom, dataSize);
            //note: this method isn't async, but it wouldn't be able to return async methods if it wasn't declared as one itself
            switch (sCom)
            {
                case Commands.ServerCommands.ValidVersionNumber:
                case Commands.ServerCommands.InvalidVersionNumber:
                case Commands.ServerCommands.UserAlreadyOnline:
                case Commands.ServerCommands.UserNameFound:
                case Commands.ServerCommands.UserNameNotFound:
                case Commands.ServerCommands.PasswordIncorrect:
                case Commands.ServerCommands.LoginSuccessful:
                case Commands.ServerCommands.AccountCreatedSuccessfully:
                case Commands.ServerCommands.ServerError:
                case Commands.ServerCommands.MessageSent:
                case Commands.ServerCommands.RequestAccepted:
                case Commands.ServerCommands.RequestDenied:
                case Commands.ServerCommands.ConnectionValid:
                case Commands.ServerCommands.GameAborted:
                case Commands.ServerCommands.AlreadyIngame:
                    await saveServerCommand(sCom);
                    Console.WriteLine("Received server command: {0}.", sCom);
                    return;
                case Commands.ServerCommands.ReceiveString:
                    await receiveString(sCom, dataSize);
                    Console.WriteLine("Received string.");
                    return;
                case Commands.ServerCommands.ReceiveList_String:
                    await receiveObject(sCom, dataSize);
                    Console.WriteLine("Received list of string.");
                    return;
                case Commands.ServerCommands.MessageForwarded:
                    await receiveObject(sCom, dataSize);
                    removeServerResponse(sCom);
                    Console.WriteLine("Received forwarded message.");
                    myWindow.Dispatcher.BeginInvoke(new ChatMsgAdderDelegate(myWindow.addChatMessage), System.Windows.Threading.DispatcherPriority.Background, (ChatMessage)receivedData);
                    return;

                case Commands.ServerCommands.OnlineUsersChanged:
                    await receiveObject(sCom, dataSize);
                    removeServerResponse(sCom);
                    Console.WriteLine("Received online users.");
                    //myWindow.updateUserList((List<string>)receivedData);
                    //foreach (string user in ((List<string>)receivedData)) Console.WriteLine(user);
                    updateUserList((List<string>)receivedData);
                    return;

                case Commands.ServerCommands.ReceiveTurnData:
                    await receiveObject(sCom, dataSize);
                    Console.WriteLine("Received turn data.");
                    return;

                case Commands.ServerCommands.RequestGameReply_ConnectFour:
                    await receiveObject(sCom, dataSize);
                    removeServerResponse(sCom);
                    MessageBoxResult mbrConnectFour = MessageBoxResult.No;
                    var cfSpecs = (ConnectFourSpecificationsPortable)receivedData;
                    myWindow.Dispatcher.Invoke(new Action(() => mbrConnectFour = MessageBox.Show($"{cfSpecs.PlayerNames[0]} is challenging you to a game of Connect Four!\n\nSpecs:\nParticipating players: {cfSpecs.PlayerNames[0]}, {cfSpecs.PlayerNames[1]}\nSize: {cfSpecs.BoardSize.Width}x{cfSpecs.BoardSize.Height}\nStarting: {cfSpecs.PlayerNames[cfSpecs.First]}\n\nDo you accept the challenge?", "Challenge", MessageBoxButton.YesNo, MessageBoxImage.Information)));
                    if (mbrConnectFour == MessageBoxResult.Yes)
                    {
                        await sendCommand(Commands.ClientCommands.SubmitRequestAccepted);
                        removeServerResponse(Commands.ServerCommands.GameAborted);
                        IngameInf = IngameStatus.ConnectFour;
                        opponent = cfSpecs.PlayerNames[0];
                        var players = new Player[2];
                        players[0] = new Player(Player.playerType.RemoteHuman, cfSpecs.PlayerNames[0]);
                        players[1] = new Player(this);
                        var newConnectFour = new ConnectFour(new ConnectFourSpecifications(cfSpecs, Game.GameLocation.Client, players, clientWrapper: this));
                        if (newConnectFour.myWindow != null) newConnectFour.myWindow.Owner = myWindow;
                    }
                    else
                    {
                        await sendCommand(Commands.ClientCommands.SubmitRequestDenied);
                    }
                    return;

                case Commands.ServerCommands.RequestGameReply_Chomp:
                    await receiveObject(sCom, dataSize);
                    removeServerResponse(sCom);
                    MessageBoxResult mbrChomp = MessageBoxResult.No;
                    var cSpecs = (ChompSpecificationsPortable)receivedData;
                    myWindow.Dispatcher.Invoke(new Action(() => mbrChomp = MessageBox.Show($"{cSpecs.PlayerNames[0]} is challenging you to a game of Chomp!\n\nSpecs:\nParticipating players: {cSpecs.PlayerNames[0]}, {cSpecs.PlayerNames[1]}\nSize: {cSpecs.BoardSize.Width}x{cSpecs.BoardSize.Height}\nStarting: {cSpecs.PlayerNames[cSpecs.First]}\n\nDo you accept the challenge?", "Challenge", MessageBoxButton.YesNo, MessageBoxImage.Information)));
                    if (mbrChomp == MessageBoxResult.Yes)
                    {
                        await sendCommand(Commands.ClientCommands.SubmitRequestAccepted);
                        removeServerResponse(Commands.ServerCommands.GameAborted);
                        IngameInf = IngameStatus.Chomp;
                        opponent = cSpecs.PlayerNames[0];
                        var players = new Player[2];
                        players[0] = new Player(Player.playerType.RemoteHuman, cSpecs.PlayerNames[0]);
                        players[1] = new Player(this);
                        var newChomp = new Chomp(new ChompSpecifications(cSpecs, Game.GameLocation.Client, players, clientWrapper: this));
                        if (newChomp.myWindow != null) newChomp.myWindow.Owner = myWindow;
                    }
                    else
                    {
                        await sendCommand(Commands.ClientCommands.SubmitRequestDenied);
                    }
                    return;

                case Commands.ServerCommands.RequestClientInfo:
                    var cInf = new ClientInfo(IngameInf, opponent);
                    await sendObject(Commands.ClientCommands.SubmitClientInfo, cInf);
                    break;


                default:
                    Console.WriteLine("Unhandled server command ({0}).", sCom);
                    return;
            }
        }

        private async Task saveServerCommand(Commands.ServerCommands sCom)
        { serverMsgs.Add(sCom); /*Console.WriteLine("Saved server command {0}: {1}.", sCom, serverMsgs.Contains(sCom));*/ }
        public async Task awaitServerResponse(List<Commands.ServerCommands> sComs)
        {
            foreach (Commands.ServerCommands sCom in sComs) removeServerResponse(sCom);

            bool contained = false;
            while (!contained)
            {
                foreach (Commands.ServerCommands sCom in sComs)
                { if (serverMsgs.Contains(sCom)) { contained = true; break; } }
                //if (contained) break;
                //Console.WriteLine("Didn't receive respone yet...");
                await Task.Delay(500);
            }
            //Console.WriteLine("###Received response.###");
        }
        public async Task awaitServerResponse(Commands.ServerCommands sCom)
        {
            removeServerResponse(sCom);

            while (!serverMsgs.Contains(sCom))
            {
                //Console.WriteLine("Didn't reveive {0} yet ({1}).", sCom, serverMsgs.Contains(sCom));
                await Task.Delay(500);
            }
            //Console.WriteLine("###Received {0}.###", sCom);
        }

        private bool checkServerResponse(Commands.ServerCommands sCom)
        {
            return serverMsgs.Contains(sCom);
        }
        public void removeServerResponse(Commands.ServerCommands sCom)
        {
            while (serverMsgs.Contains(sCom)) serverMsgs.Remove(sCom);
        }

        private async Task<byte[]> receiveData(int dataSize)
        {
            int bytesRec = 0;
            byte[] infBlock = new byte[dataSize];
            while (bytesRec < dataSize)
            { bytesRec += await netS.ReadAsync(infBlock, bytesRec, infBlock.Length - bytesRec); }
            return infBlock;
        }
        private async Task receiveString(Commands.ServerCommands sCom, int dataSize)
        {
            receivedData = Encoding.Unicode.GetString(await receiveData(dataSize));
            await saveServerCommand(sCom);
        }
        private async Task receiveObject(Commands.ServerCommands sCom, int dataSize)
        {
            MemoryStream ms = new MemoryStream(await receiveData(dataSize));
            BinaryFormatter bf = new BinaryFormatter();
            ms.Position = 0;
            receivedData = bf.Deserialize(ms);
            await saveServerCommand(sCom);
        }

        public async Task sendCommand(Commands.ClientCommands cCom)
        {
            Console.WriteLine("Sending command: {0}", cCom);
            await sendObject(cCom, null);
        }
        private async Task sendString(Commands.ClientCommands cCom, string str)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                //convert object to byte array
                byte[] data;
                if (str != "")
                { data = Encoding.Unicode.GetBytes(str); } //get data from string slightly differently than for objects because of encoding
                else
                { data = new byte[0]; }

                //send command number
                byte[] prep = BitConverter.GetBytes(Commands.getClientCommandUInt(cCom));
                if (BitConverter.IsLittleEndian) //target computer might use different endian -> send and receive as big endian and if necessary, restore to little endian in client
                { Array.Reverse(prep); }
                await netS.WriteAsync(prep, 0, prep.Length); //write command (uint) to stream

                //send data size
                prep = BitConverter.GetBytes(data.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(prep);
                await netS.WriteAsync(prep, 0, prep.Length); //write length of data (int) to stream

                //send object
                await netS.WriteAsync(data, 0, data.Length); //write object to stream
            }
        }
        public async Task sendObject(Commands.ClientCommands cCom, object obj)
        {
            //see e.g.: https://stackoverflow.com/questions/2316397/sending-and-receiving-custom-objects-using-tcpclient-class-in-c-sharp

            using (MemoryStream ms = new MemoryStream())
            {
                //convert object to byte array
                byte[] data;
                if (obj != null)
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, obj); //serialise object to memory stream first -> can provide size of data in byte
                    data = ms.ToArray(); //the object converted to a byte array
                }
                else
                { data = new byte[0]; }

                //send command number
                byte[] prep = BitConverter.GetBytes(Commands.getClientCommandUInt(cCom));
                if (BitConverter.IsLittleEndian) //target computer might use different endian -> send and receive as big endian and if necessary, restore to little endian in client
                    Array.Reverse(prep);
                await netS.WriteAsync(prep, 0, prep.Length); //write command (uint) to stream

                //send data size
                prep = BitConverter.GetBytes(data.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(prep);
                await netS.WriteAsync(prep, 0, prep.Length); //write length of data (int) to stream

                //send object
                await netS.WriteAsync(data, 0, data.Length); //write object to stream
            }
        }
        
        private async Task<bool?> applyDeadline(Task task, int timeInMs)
        {
            //try
            //{ task.Start(); }
            //catch (Exception)
            //{ }
            await Task.WhenAny(task, Task.Delay(timeInMs));
            return task?.IsCompleted;
        }


        //user interface
        private delegate int ObjectAdderDelegate(object obj);
        private delegate void ChatMsgAdderDelegate(ChatMessage msg);
        private delegate Task UpdaterDelegate(List<string> onlineUsers);
        private async Task<bool> requestCompareVersionNumber()
        {
            await sendString(Commands.ClientCommands.RequestCompareVersionNumber, VersionNumber.get());
            await awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.ValidVersionNumber, Commands.ServerCommands.InvalidVersionNumber });

            if (checkServerResponse(Commands.ServerCommands.ValidVersionNumber))
            {
                removeServerResponse(Commands.ServerCommands.ValidVersionNumber);
                return true;
            }
            else
            {
                removeServerResponse(Commands.ServerCommands.InvalidVersionNumber);
                return false;
            }
        }
        private async Task<bool> login()
        {
            while (true)
            {
                var winLogin = new LoginWindow();
                //winLogin.Owner = this; //behaviour when using ShowDialog: winLogin is always in front of ClientMenu, if ClientMenu is closed so is winLogin, user cannot interact with ClientMenu while winLogin is shown
                if (winLogin.ShowDialog() == true)
                {
                    await sendString(Commands.ClientCommands.SubmitUserName, winLogin.tbUsername.Text);

                    if (winLogin.ReturnValue == "login")
                    {
                        await awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.UserNameFound, Commands.ServerCommands.UserNameNotFound, Commands.ServerCommands.InvalidUserName, Commands.ServerCommands.UserAlreadyOnline });

                        if (checkServerResponse(Commands.ServerCommands.InvalidUserName))
                        {
                            removeServerResponse(Commands.ServerCommands.InvalidUserName);
                            MessageBox.Show("The entered username is invalid.", "Invalid Username", MessageBoxButton.OK, MessageBoxImage.Error);
                            continue;
                        }
                        else if (checkServerResponse(Commands.ServerCommands.UserNameNotFound))
                        {
                            removeServerResponse(Commands.ServerCommands.UserNameNotFound);
                            MessageBox.Show("The entered username does not exist. Please check the spelling or register to create a new account.", "User Name Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                            continue;
                        }
                        else if (checkServerResponse(Commands.ServerCommands.UserAlreadyOnline))
                        {
                            removeServerResponse(Commands.ServerCommands.UserAlreadyOnline);
                            MessageBox.Show("The account using this username is already online. If this isn't you, try again later and consider changing your password.", "User Already Online", MessageBoxButton.OK, MessageBoxImage.Error);
                            continue;
                        }
                        else if (checkServerResponse(Commands.ServerCommands.UserNameFound))
                        {
                            removeServerResponse(Commands.ServerCommands.UserNameFound);
                            //user found -> submit password
                            await sendString(Commands.ClientCommands.SubmitPassword_ExistingUser, winLogin.pbPassword.Password);
                            await awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.PasswordIncorrect, Commands.ServerCommands.LoginSuccessful });

                            if (checkServerResponse(Commands.ServerCommands.PasswordIncorrect))
                            {
                                removeServerResponse(Commands.ServerCommands.PasswordIncorrect);
                                MessageBox.Show("The entered username and password don't match.", "Password Incorrect", MessageBoxButton.OK, MessageBoxImage.Error);
                                continue;
                            }
                            else if (checkServerResponse(Commands.ServerCommands.LoginSuccessful))
                            {
                                removeServerResponse(Commands.ServerCommands.LoginSuccessful);
                                username = winLogin.tbUsername.Text;
                                MessageBox.Show($"You have been logged in successfully. Welcome back, {username}!", "Login Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                                break;
                            }
                        }
                    }
                    else //winLogin.ReturnValue = "register"
                    {
                        await awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.UserNameFound, Commands.ServerCommands.UserNameNotFound, Commands.ServerCommands.InvalidUserName, Commands.ServerCommands.UserAlreadyOnline });

                        if (checkServerResponse(Commands.ServerCommands.InvalidUserName))
                        {
                            removeServerResponse(Commands.ServerCommands.InvalidUserName);
                            MessageBox.Show("The entered username is invalid.", "Invalid Username", MessageBoxButton.OK, MessageBoxImage.Error);
                            continue;
                        }
                        else if (checkServerResponse(Commands.ServerCommands.UserNameFound))
                        {
                            removeServerResponse(Commands.ServerCommands.UserNameFound);
                            MessageBox.Show("The entered username already exists. Please choose a different name or login to the existing account.", "User Name Found", MessageBoxButton.OK, MessageBoxImage.Error);
                            continue;
                        }
                        else if (checkServerResponse(Commands.ServerCommands.UserAlreadyOnline))
                        {
                            removeServerResponse(Commands.ServerCommands.UserAlreadyOnline);
                            MessageBox.Show("The entered username already exists. Please choose a different name or login to the existing account.", "User Name Found", MessageBoxButton.OK, MessageBoxImage.Error);
                            continue;
                        }
                        else if (checkServerResponse(Commands.ServerCommands.UserNameNotFound))
                        {
                            removeServerResponse(Commands.ServerCommands.UserNameNotFound);
                            //user not found -> create new account by submitting password
                            await sendString(Commands.ClientCommands.SubmitPassword_NewUser, winLogin.pbPassword.Password);
                            await awaitServerResponse(Commands.ServerCommands.AccountCreatedSuccessfully);
                            removeServerResponse(Commands.ServerCommands.AccountCreatedSuccessfully);
                            username = winLogin.tbUsername.Text;
                            MessageBox.Show($"Your account has been created successfully. Welcome, {username}!", "Account Creation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                            break;
                        }
                    }
                }
                else //DialogResult != true -> window has been closed instead of pressing one of the buttons
                {
                    return false;
                }
            }

            //login successful -> list of online users will be sent automatically soon because server registers new users coming online
            myWindow.Dispatcher.Invoke(new Action(() => myWindow.Title = $"Client Menu (connected to {sender.RemoteEndPoint.ToString()} as {username})"));
            return true;
        }
        private async Task requestOnlineUserList()
        {
            await sendCommand(Commands.ClientCommands.RequestOnlineUserList);
            await awaitServerResponse(Commands.ServerCommands.ReceiveList_String);
            removeServerResponse(Commands.ServerCommands.ReceiveList_String);
            myWindow.updateUserList((List<string>)receivedData);
        }
        public async Task requestBroadcastChatMessage(string msg)
        {
            ChatMessage newMsg = new ChatMessage(username, msg);
            await sendObject(Commands.ClientCommands.RequestSendMessage, newMsg);
            myWindow.Dispatcher.BeginInvoke(new ChatMsgAdderDelegate(myWindow.addChatMessage), System.Windows.Threading.DispatcherPriority.Background, newMsg);
            await awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.MessageSent, Commands.ServerCommands.ServerError });

            if (checkServerResponse(Commands.ServerCommands.MessageSent))
            {
                removeServerResponse(Commands.ServerCommands.MessageSent);
            }
            else
            {
                removeServerResponse(Commands.ServerCommands.ServerError);
                MessageBox.Show("An error has occurred while trying to send the message.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public async Task requestWhisperChatMessage(string recipient, string msg)
        {
            ChatMessage newMsg = new ChatMessage(username, msg, recipient);
            await sendObject(Commands.ClientCommands.RequestSendMessage, newMsg);
            if (recipient != username) //this would otherwise cause the message to appear in the chat box twice
            { myWindow.Dispatcher.BeginInvoke(new ChatMsgAdderDelegate(myWindow.addChatMessage), System.Windows.Threading.DispatcherPriority.Background, newMsg); }
            await awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.MessageSent, Commands.ServerCommands.ServerError });

            if (checkServerResponse(Commands.ServerCommands.MessageSent))
            {
                removeServerResponse(Commands.ServerCommands.MessageSent);
            }
            else
            {
                removeServerResponse(Commands.ServerCommands.ServerError);
                MessageBox.Show("An error has occurred while trying to send the message.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void updateUserList(List<string> onlineUsers)
        {
            myWindow.Dispatcher.BeginInvoke(new UpdaterDelegate(myWindow.updateUserList), System.Windows.Threading.DispatcherPriority.Normal, onlineUsers);
        }

        public async Task<Commands.ServerCommands> requestGameChomp(ChompSpecificationsPortable specs)
        {
            await sendObject(Commands.ClientCommands.RequestGame_Chomp, specs);
            await awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.RequestAccepted, Commands.ServerCommands.RequestDenied, Commands.ServerCommands.AlreadyIngame, Commands.ServerCommands.ServerError });
            if (serverMsgs.Contains(Commands.ServerCommands.RequestAccepted))
            {
                removeServerResponse(Commands.ServerCommands.RequestAccepted);
                return Commands.ServerCommands.RequestAccepted;
            }
            else if (serverMsgs.Contains(Commands.ServerCommands.RequestDenied))
            {
                removeServerResponse(Commands.ServerCommands.RequestDenied);
                return Commands.ServerCommands.RequestDenied;
            }
            else if (serverMsgs.Contains(Commands.ServerCommands.AlreadyIngame))
            {
                removeServerResponse(Commands.ServerCommands.AlreadyIngame);
                return Commands.ServerCommands.AlreadyIngame;
            }
            else
            {
                removeServerResponse(Commands.ServerCommands.ServerError);
                return Commands.ServerCommands.ServerError;
            }
        }
        public async Task<Commands.ServerCommands> requestGameConnectFour(ConnectFourSpecificationsPortable specs)
        {
            await sendObject(Commands.ClientCommands.RequestGame_ConnectFour, specs);
            await awaitServerResponse(new List<Commands.ServerCommands>() { Commands.ServerCommands.RequestAccepted, Commands.ServerCommands.RequestDenied, Commands.ServerCommands.AlreadyIngame, Commands.ServerCommands.ServerError });
            if (serverMsgs.Contains(Commands.ServerCommands.RequestAccepted))
            {
                removeServerResponse(Commands.ServerCommands.RequestAccepted);
                return Commands.ServerCommands.RequestAccepted;
            }
            else if (serverMsgs.Contains(Commands.ServerCommands.RequestDenied))
            {
                removeServerResponse(Commands.ServerCommands.RequestDenied);
                return Commands.ServerCommands.RequestDenied;
            }
            else if (serverMsgs.Contains(Commands.ServerCommands.AlreadyIngame))
            {
                removeServerResponse(Commands.ServerCommands.AlreadyIngame);
                return Commands.ServerCommands.AlreadyIngame;
            }
            else
            {
                removeServerResponse(Commands.ServerCommands.ServerError);
                return Commands.ServerCommands.ServerError;
            }
        }



        //shutdown
        public void Dispose()
        {
            //WIP: shut down active games!

            //dispose of socket, streams etc.
            try
            { bufS.Close(); bufS.Dispose(); }
            catch (Exception)
            { }

            try
            { netS.Close(); netS.Dispose(); }
            catch (Exception)
            { }

            try
            { sender.Shutdown(SocketShutdown.Both); } //release socket
            catch (Exception)
            { }

            try
            { sender.Dispose(); }
            catch (Exception)
            { }

            try
            { myWindow.Close(); }
            catch (Exception)
            { }

            receivedData = null;
        }
    }
}
