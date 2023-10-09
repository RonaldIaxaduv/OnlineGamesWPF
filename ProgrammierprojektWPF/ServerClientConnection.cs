using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using Communication;
using System.Collections.Generic;

namespace ProgrammierprojektWPF
{

    public class ServerClientConnection
    {
        private const int bufferSize = 1000; //size of the buffer for the network stream
        public Socket handler = null;
        private NetworkStream netS = null;
        private BufferedStream bufS = null;
        public Server parentServer = null;
        public List<Commands.ClientCommands> clientMsgs = new List<Commands.ClientCommands>();
        public object receivedData;

        public string username = "";
        public bool online = false;


        //basic behaviour
        public ServerClientConnection(Socket handler, Server parentServer)
        {
            this.handler = handler;
            this.parentServer = parentServer;
            netS = new NetworkStream(this.handler);
            bufS = new BufferedStream(netS, bufferSize);
            StartConnection();
        }

        public async void StartConnection()
        {
            try
            { await listen(); }
            catch (ObjectDisposedException)
            { }
            catch (InvalidOperationException iope) //thrown when executed on wrong thread
            { Console.WriteLine("InvalidOperationException: {0}\n\n{1}\n\n{2}", iope.Message, iope.Source, iope.StackTrace); }
            catch (IOException ioe)
            { Console.WriteLine("IOException: {0}\n\n{1}\n\n{2}", ioe.Message, ioe.Source, ioe.StackTrace); }
            //catch (Exception e)
            //{ Console.WriteLine("Error: {0}\n\n{1}\n\n{2}", e.Message, e.Source, e.StackTrace); }

            if (username != "")
            { Console.WriteLine("Shutting down ServerClientConnection of {0}.", username); }
            else
            { Console.WriteLine("Shutting down ServerClientConnection of pending user."); }

            try
            {
                await Shutdown();
            }
            catch (ObjectDisposedException)
            { }
            catch (Exception e)
            { Console.WriteLine("Error: {0}\n\n{1}\n\n{2}", e.Message, e.Source, e.StackTrace); }

            Dispose();
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
            while (Connection.isConnected(handler))
            {
                if (netS.DataAvailable)
                {
                    //Console.WriteLine("New data available.");
                    while (bytesRec < comBlock.Length)
                    //{ bytesRec += await netS.ReadAsync(comBlock, bytesRec, comBlock.Length - bytesRec); }
                    { bytesRec += netS.Read(comBlock, bytesRec, comBlock.Length - bytesRec); }
                    //Console.WriteLine("Data has been read.");

                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(comBlock, 0, 4);
                        Array.Reverse(comBlock, 4, 4);
                    }
                    uint command = BitConverter.ToUInt32(comBlock, 0); //first 4 bytes (more isn't read): command, i.e. what kind of object will be received
                    int dataSize = BitConverter.ToInt32(comBlock, 4); //second 4 bytes: length of the upcoming message (arrays can only have a size of int, so sending a uint wouldn't work)
                                                                      //Console.WriteLine("Received command number: {0}", command);
                                                                      //Console.WriteLine("Received data size: {0}", dataSize);

                    await handleMessage(Commands.getClientCommand(command), dataSize);

                    //reset
                    bytesRec = 0;
                }
                await Task.Delay(500); //check for new messages after a short delay
            }
            return;
        }


        //information processing, server-client communication
        private async Task handleMessage(Commands.ClientCommands cCom, int dataSize)
        {
            Console.WriteLine("Handling message ({0})...", cCom);
            switch (cCom)
            {
                case Commands.ClientCommands.SubmitRequestAccepted:
                case Commands.ClientCommands.SubmitRequestDenied:
                case Commands.ClientCommands.ClientError:
                    await saveClientCommand(cCom);
                    Console.WriteLine("Received client command: {0}.", cCom);
                    break;
                    
                case Commands.ClientCommands.RequestCompareVersionNumber:
                    Console.WriteLine("Comparing version number...");
                    await receiveString(cCom, dataSize);
                    removeClientResponse(cCom);
                    Console.WriteLine("String received. {0} vs {1}", VersionNumber.get(), (string)receivedData);

                    if (VersionNumber.get() == (string)receivedData)
                    { await sendCommand(Commands.ServerCommands.ValidVersionNumber); Console.WriteLine("Reply sent: valid."); }
                    else
                    { await sendCommand(Commands.ServerCommands.InvalidVersionNumber); Console.WriteLine("Reply sent: invalid."); }
                    return;

                case Commands.ClientCommands.SubmitUserName:
                    Console.Write("Checking submitted username... ");
                    await receiveString(cCom, dataSize);
                    removeClientResponse(cCom);
                    username = "";
                    if (parentServer.getOnlineUsers().Contains((string)receivedData)) //the user going by this name is already logged in -> choose different name
                    { await sendCommand(Commands.ServerCommands.UserAlreadyOnline); Console.WriteLine("Already online."); }
                    else if (parentServer.getRegisteredUsers().Contains((string)receivedData)) //submitted name contained in the list of registered users?
                    { await sendCommand(Commands.ServerCommands.UserNameFound); Console.WriteLine("Found."); }
                    else
                    { await sendCommand(Commands.ServerCommands.UserNameNotFound); Console.WriteLine("Not found."); }
                    username = (string)receivedData;
                    Console.WriteLine("username set to {0}.", username);
                    return;

                case Commands.ClientCommands.SubmitPassword_ExistingUser:
                    await receiveString(cCom, dataSize);
                    removeClientResponse(cCom);
                    //Console.WriteLine("Checking password ({0} | {1})...", username, (string)receivedData);
                    if (parentServer.tryLogin(username, (string)receivedData))
                    {
                        await sendCommand(Commands.ServerCommands.LoginSuccessful);
                        await goOnline(); //sets online to true and updates all lbUser listboxes
                        parentServer.addSystemMessage($"{username} has come online.");
                    }
                    else
                    { await sendCommand(Commands.ServerCommands.PasswordIncorrect); username = ""; }
                    break;

                case Commands.ClientCommands.SubmitPassword_NewUser:
                    await receiveString(cCom, dataSize);
                    removeClientResponse(cCom);
                    //Console.WriteLine("Creating new user ({0} | {1})...", username, (string)receivedData);
                    if (parentServer.tryRegister(username, (string)receivedData))
                    {
                        await sendCommand(Commands.ServerCommands.AccountCreatedSuccessfully);
                        await goOnline(); //sets online to true and updates all lbUser listboxes
                        parentServer.addSystemMessage($"{username} has created their account and is now online for the first time.");
                    }
                    else
                    { await sendCommand(Commands.ServerCommands.ServerError); username = ""; }
                    break;

                case Commands.ClientCommands.RequestOnlineUserList:
                    removeClientResponse(cCom);
                    Console.WriteLine("Sending user list...");
                    await sendObject(Commands.ServerCommands.ReceiveList_String, parentServer.getOnlineUsers());
                    break;

                case Commands.ClientCommands.RequestSendMessage:
                    Console.WriteLine("Sending message...");
                    await receiveObject(Commands.ClientCommands.RequestSendMessage, dataSize);
                    ChatMessage recMsg = (ChatMessage)receivedData;
                    if (await parentServer.sendMessage(recMsg))
                    { await sendCommand(Commands.ServerCommands.MessageSent); }
                    else
                    { await sendCommand(Commands.ServerCommands.ServerError); }
                    break;

                case Commands.ClientCommands.RequestGame_ConnectFour:
                    removeClientResponse(cCom);
                    Console.WriteLine("Forwarding request to play connect four...");
                    await receiveObject(Commands.ClientCommands.RequestGame_ConnectFour, dataSize);
                    var cfConnection = parentServer.getConnectionFromUsername(((ConnectFourSpecificationsPortable)receivedData).PlayerNames[1]);
                    if (cfConnection == null)
                    {
                        await sendCommand(Commands.ServerCommands.ServerError);
                    }
                    else
                    {
                        await cfConnection.sendCommand(Commands.ServerCommands.RequestClientInfo);
                        await cfConnection.awaitClientResponse(Commands.ClientCommands.SubmitClientInfo);
                        cfConnection.removeClientResponse(Commands.ClientCommands.SubmitClientInfo);
                        if (((ClientInfo)cfConnection.receivedData).ingameStatus != Client.IngameStatus.NotIngame)
                        {
                            Console.WriteLine("User is already ingame. Forwarding reply...");
                            await sendCommand(Commands.ServerCommands.AlreadyIngame);
                        }
                        else
                        {
                            await cfConnection.sendObject(Commands.ServerCommands.RequestGameReply_ConnectFour, (ConnectFourSpecificationsPortable)receivedData);
                            await cfConnection.awaitClientResponse(new List<Commands.ClientCommands>() { Commands.ClientCommands.SubmitRequestAccepted, Commands.ClientCommands.SubmitRequestDenied, Commands.ClientCommands.ClientError });
                            foreach (Commands.ClientCommands c in clientMsgs) Console.WriteLine(c);
                            if (cfConnection.clientMsgs.Contains(Commands.ClientCommands.SubmitRequestAccepted))
                            {
                                cfConnection.removeClientResponse(Commands.ClientCommands.SubmitRequestAccepted);
                                cfConnection.removeClientResponse(Commands.ClientCommands.AbortGame);
                                removeClientResponse(Commands.ClientCommands.AbortGame);
                                Console.WriteLine("Request accepted. Forwarding reply...");
                                await sendCommand(Commands.ServerCommands.RequestAccepted);
                            }
                            else if (cfConnection.clientMsgs.Contains(Commands.ClientCommands.SubmitRequestDenied))
                            {
                                cfConnection.removeClientResponse(Commands.ClientCommands.SubmitRequestDenied);
                                Console.WriteLine("Request denied. Forwarding reply...");
                                await sendCommand(Commands.ServerCommands.RequestDenied);
                            }
                            else
                            {
                                cfConnection.removeClientResponse(Commands.ClientCommands.ClientError);
                                Console.WriteLine("Request unanswered due to an error. Forwarding reply...");
                                await sendCommand(Commands.ServerCommands.ServerError);
                            }
                        }
                    }
                    break;

                case Commands.ClientCommands.RequestGame_Chomp:
                    removeClientResponse(cCom);
                    Console.WriteLine("Forwarding request to play chomp...");
                    await receiveObject(Commands.ClientCommands.RequestGame_Chomp, dataSize);
                    var cConnection = parentServer.getConnectionFromUsername(((ChompSpecificationsPortable)receivedData).PlayerNames[1]);
                    if (cConnection == null)
                    {
                        await sendCommand(Commands.ServerCommands.ServerError);
                    }
                    else
                    {
                        await cConnection.sendCommand(Commands.ServerCommands.RequestClientInfo);
                        await cConnection.awaitClientResponse(Commands.ClientCommands.SubmitClientInfo);
                        cConnection.removeClientResponse(Commands.ClientCommands.SubmitClientInfo);
                        if (((ClientInfo)cConnection.receivedData).ingameStatus != Client.IngameStatus.NotIngame)
                        {
                            Console.WriteLine("User is already ingame. Forwarding reply...");
                            await sendCommand(Commands.ServerCommands.AlreadyIngame);
                        }
                        else
                        {
                            await cConnection.sendObject(Commands.ServerCommands.RequestGameReply_Chomp, (ChompSpecificationsPortable)receivedData);
                            await cConnection.awaitClientResponse(new List<Commands.ClientCommands>() { Commands.ClientCommands.SubmitRequestAccepted, Commands.ClientCommands.SubmitRequestDenied, Commands.ClientCommands.ClientError });
                            foreach (Commands.ClientCommands c in clientMsgs) Console.WriteLine(c);
                            if (cConnection.clientMsgs.Contains(Commands.ClientCommands.SubmitRequestAccepted))
                            {
                                cConnection.removeClientResponse(Commands.ClientCommands.SubmitRequestAccepted);
                                cConnection.removeClientResponse(Commands.ClientCommands.AbortGame);
                                removeClientResponse(Commands.ClientCommands.AbortGame);
                                Console.WriteLine("Request accepted. Forwarding reply...");
                                await sendCommand(Commands.ServerCommands.RequestAccepted);
                                //var specs = (ChompSpecificationsPortable)receivedData;
                                //var players = new Player[2];
                                //players[0] = new Player(this);
                                //players[1] = new Player(connection);
                                //var newChomp = new Chomp(new ChompSpecifications(specs, Game.GameLocation.Server, players, serverWrapper: parentServer));
                            }
                            else if (cConnection.clientMsgs.Contains(Commands.ClientCommands.SubmitRequestDenied))
                            {
                                cConnection.removeClientResponse(Commands.ClientCommands.SubmitRequestDenied);
                                Console.WriteLine("Request denied. Forwarding reply...");
                                await sendCommand(Commands.ServerCommands.RequestDenied);
                            }
                            else
                            {
                                cConnection.removeClientResponse(Commands.ClientCommands.ClientError);
                                Console.WriteLine("Request unanswered due to an error. Forwarding reply...");
                                await sendCommand(Commands.ServerCommands.ServerError);
                            }
                        }
                    }
                    break;

                case Commands.ClientCommands.SubmitTurnData:
                    await receiveObject(Commands.ClientCommands.SubmitTurnData, dataSize);
                    var recipient = parentServer.getConnectionFromUsername(((TurnDataPortable)receivedData).recipient);
                    if (recipient != null)
                    { await recipient.sendObject(Commands.ServerCommands.ReceiveTurnData, (TurnDataPortable)receivedData); }
                    else
                    {
                        //await sendCommand(Commands.ServerCommands.ServerError); //this might cause errors as long as it isn't handled
                    }
                    break;

                case Commands.ClientCommands.RequestConnectionVerification:
                case Commands.ClientCommands.AbortGame:
                    await receiveObject(Commands.ClientCommands.RequestConnectionVerification, dataSize);
                    //removeClientResponse(Commands.ClientCommands.RequestConnectionVerification); //don't remove AbortGame - it's needed for other clients to notice when a user has gone offline
                    removeClientResponse(cCom);
                    var players = (Player[])receivedData;
                    if (cCom == Commands.ClientCommands.RequestConnectionVerification)
                    {
                        bool valid = true;
                        foreach (Player p in players)
                        {
                            if (p.Name != username)
                            {
                                var otherConnection = parentServer.getConnectionFromUsername(p.Name);
                                Console.Write("Checking connection to {0}... ", otherConnection.username);
                                if (!Connection.isConnected(otherConnection?.handler))
                                {
                                    Console.WriteLine("connection severed.");
                                    valid = false;
                                    break;
                                }
                                await otherConnection.sendCommand(Commands.ServerCommands.RequestClientInfo);
                                await otherConnection.awaitClientResponse(Commands.ClientCommands.SubmitClientInfo);
                                otherConnection.removeClientResponse(Commands.ClientCommands.SubmitClientInfo);
                                var otherCInf = (ClientInfo)otherConnection.receivedData;
                                if (otherCInf.ingameStatus == Client.IngameStatus.NotIngame)
                                {
                                    Console.WriteLine("not ingame anymore.");
                                    valid = false;
                                    break;
                                }
                                else
                                { Console.WriteLine("still ingame ({0}).", otherCInf.ingameStatus); }
                            }
                        }
                        if (valid)
                        {
                            Console.WriteLine("All connections valid. Sending response...");
                            await sendCommand(Commands.ServerCommands.ConnectionValid);
                            return;
                        }
                        Console.WriteLine("A user's connection has been severed. Sending commands to abort game...");
                    }
                    else
                    { Console.WriteLine("A user has sent a request to abort the game. Forwarding..."); }
                    foreach (Player p in players)
                    {
                        await parentServer.getConnectionFromUsername(p.Name)?.sendCommand(Commands.ServerCommands.GameAborted);
                    }
                    break;

                case Commands.ClientCommands.SubmitClientInfo:
                    await receiveObject(Commands.ClientCommands.SubmitClientInfo, dataSize);
                    Console.WriteLine("Received {0}'s client info.", username);
                    break;


                default:
                    Console.WriteLine("Unhandled client command ({0}).", cCom);
                    break;
            }
            return;
        }

        private async Task saveClientCommand(Commands.ClientCommands cCom)
        {
            clientMsgs.Add(cCom);
            //Console.WriteLine("clientMsgs now include {0} ({1}).", cCom, clientMsgs.Contains(cCom));
        }
        public async Task awaitClientResponse(List<Commands.ClientCommands> cComs)
        {
            foreach (Commands.ClientCommands cCom in cComs) removeClientResponse(cCom);

            bool contained = false;
            while (!contained)
            {
                foreach (Commands.ClientCommands cCom in cComs)
                { if (clientMsgs.Contains(cCom)) { contained = true; break; } }
                //if (contained) break;
                //Console.WriteLine("Didn't receive respone yet...");
                await Task.Delay(500);
            }
            //Console.WriteLine("###Received response.###");
        }
        public async Task awaitClientResponse(Commands.ClientCommands cCom)
        {
            removeClientResponse(cCom);

            while (!clientMsgs.Contains(cCom))
            {
                //Console.WriteLine("Didn't reveive {0} yet ({1}).", cCom, clientMsgs.Contains(cCom));
                await Task.Delay(500);
            }
            //Console.WriteLine("###Received {0}.###", cCom);
        }
        public void removeClientResponse(Commands.ClientCommands cCom)
        {
            while (clientMsgs.Contains(cCom)) clientMsgs.Remove(cCom);
            //Console.WriteLine("clientMsgs no longer contains {0} ({1}).", cCom, !clientMsgs.Contains(cCom));
        }

        private async Task<byte[]> receiveData(int dataSize)
        {
            int bytesRec = 0;
            byte[] infBlock = new byte[dataSize];
            while (bytesRec < dataSize)
            { bytesRec += await netS.ReadAsync(infBlock, bytesRec, infBlock.Length - bytesRec); }
            return infBlock;
        }
        private async Task receiveString(Commands.ClientCommands cCom, int dataSize)
        {
            receivedData = Encoding.Unicode.GetString(await receiveData(dataSize));
            await saveClientCommand(cCom);
        }
        private async Task receiveObject(Commands.ClientCommands cCom, int dataSize)
        {
            MemoryStream ms = new MemoryStream(await receiveData(dataSize));
            BinaryFormatter bf = new BinaryFormatter();
            ms.Position = 0;
            receivedData = bf.Deserialize(ms);
            await saveClientCommand(cCom);
        }

        public async Task sendCommand(Commands.ServerCommands sCom)
        {
            await sendObject(sCom, null);
        }
        private async Task sendString(Commands.ServerCommands sCom, string str)
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
                byte[] prep = BitConverter.GetBytes(Commands.getServerCommandUInt(sCom));
                if (BitConverter.IsLittleEndian) //target computer might use different endian -> send and receive as big endian and if necessary, restore to little endian in client
                { Array.Reverse(prep); }
                await netS.WriteAsync(prep, 0, prep.Length); //write command (uint) to stream

                //send data size
                prep = BitConverter.GetBytes(data.Length);
                if (BitConverter.IsLittleEndian)
                { Array.Reverse(prep); }
                await netS.WriteAsync(prep, 0, prep.Length); //write length of data (int) to stream

                //send object
                await netS.WriteAsync(data, 0, data.Length); //write object to stream
            }
        }
        public async Task sendObject(Commands.ServerCommands sCom, object obj)
        {
            //see e.g.: https://stackoverflow.com/questions/2316397/sending-and-receiving-custom-objects-using-tcpclient-class-in-c-sharp

            Console.WriteLine("Connection to user '{0}' here. Sending an object with {1}.", username, sCom);

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
                byte[] prep = BitConverter.GetBytes(Commands.getServerCommandUInt(sCom));
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
        public async Task sendMessage(ChatMessage msg)
        {
            await sendObject(Commands.ServerCommands.MessageForwarded, msg);
        }


        //user-related
        private async Task goOnline()
        {
            online = true;
            parentServer.activeConnections.Add(this);
            parentServer.updateUserList();
        }


        //shutdown
        public async Task Shutdown()
        {
            if (parentServer?.activeConnections.Contains(this) == true)
            {
                online = false;
                parentServer.activeConnections.Remove(this);
                parentServer.updateUserList();
                parentServer.addSystemMessage($"{username} has gone offline.");
            }
            Dispose();
        }
        public void Dispose()
        {
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
            { handler.Shutdown(SocketShutdown.Both); }
            catch (Exception)
            { }

            try
            { handler.Close(); }
            catch (Exception)
            { }

            try
            { handler.Dispose(); }
            catch (Exception)
            { }

            receivedData = null;
            parentServer = null;
        }
    }

}