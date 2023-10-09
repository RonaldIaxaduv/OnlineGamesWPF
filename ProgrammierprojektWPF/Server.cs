using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Net;
using System.Net.Sockets;
//using System.Runtime.Serialization.Formatters; //for xml formatter
using System.Runtime.Serialization.Formatters.Binary;
using Communication; //defined in this project

namespace ProgrammierprojektWPF
{
    public sealed class Server : IDisposable
    {
        public bool active = true;
        private const int bufferSize = 1000; //size of the buffer for the network stream
        private const int backlogSize = 10; //length of the pending connections queue
        public Socket clientListener = null;
        private NetworkStream netS = null;
        private BufferedStream bufS = null;

        public Dictionary<string, string> userInf; //contains information about all users (names and passwords)
        public List<ServerClientConnection> activeConnections = new List<ServerClientConnection>(); //list of users that are currently online
        public ServerMenu myWindow;

        public Server()
        {
            myWindow = new ServerMenu(this);
        }

        //basic behaviour
        public async Task startServer()
        {
            await Task.Delay(100); //wait so that MainWindow can close
            
            //load userInf
            loadUserInf();

            //get IP address and endpoint
            IPAddress ipAddress;
            IPEndPoint remoteEP;
            if (!setupEndpoint(out ipAddress, out remoteEP))
            {
                Dispose();
                return;
            }

            //set up new socket
            try
            {
                clientListener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                clientListener.Bind(remoteEP);
                //active = true;
                clientListener.Listen(backlogSize);

                myWindow.Dispatcher.Invoke(new Action(() => myWindow.Title = $"Server Menu (bound to {clientListener.LocalEndPoint.ToString()})"));

                //start listening for connections and provide server interface
                await Task.WhenAny(listen(), ShowAsync());
                active = false;
            }
            catch (ObjectDisposedException)
            { }
            catch (Exception e)
            { Console.WriteLine("Error: {0}", e.Message); }

            Dispose();
        }
        private void loadUserInf()
        {
            //binary serialisation (unsafe)
            try
            {
                using (Stream stream = File.Open("userInf.bin", FileMode.Open))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    userInf = (Dictionary<string, string>)bin.Deserialize(stream);
                }
            }
            catch (FileNotFoundException fnfe)
            {
                Console.WriteLine("User information could not be found ({0}). Creating new instance...", fnfe.Message);
                userInf = new Dictionary<string, string>();
                SaveUserInf();
            }
            catch (IOException ioe)
            { Console.WriteLine("An IOException has occurred during deserialisation: {0}", ioe.Message); }
            catch (Exception e)
            { Console.WriteLine("An error has occurred during deserialisation: {0}", e.Message); }


            /*//xml serialisation (doesn't work with dictionaries apparently...)
            try
            {
                var reader = new System.Xml.Serialization.XmlSerializer(typeof(Dictionary<string, string>));
                using (StreamReader file = new StreamReader("userInf.xml"))
                {
                    userInf = (Dictionary<string, string>)reader.Deserialize(file);
                    file.Close();
                }
            }
            catch (FileNotFoundException fnfe)
            {
                Console.WriteLine("User information could not be found ({0}). Creating new instance...", fnfe.Message);
                userInf = new Dictionary<string, string>();
            }
            catch (IOException ioe)
            { Console.WriteLine("An IOException has occurred during deserialisation: {0}", ioe.Message); }
            catch (Exception e)
            { Console.WriteLine("An error has occurred during deserialisation: {0}", e.Message); }
            */
        }
        private void SaveUserInf()
        {
            //binary serialisation (unsafe)
            try
            {
                using (Stream stream = File.Open("userInf.bin", FileMode.Create))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(stream, userInf);
                }
            }
            catch (IOException ioe)
            { Console.WriteLine("An IOException has occurred during serialisation: {0}", ioe.Message); }
            catch (Exception e)
            { Console.WriteLine("An error has occurred during serialisation: {0}", e.Message); }


            /*//xml serialisation (doesn't work with dictionaries apparently...)
            var writer = new System.Xml.Serialization.XmlSerializer(typeof(Dictionary<string, string>));
            try
            {
                using (StreamWriter wfile = new StreamWriter("userInf.xml"))
                {
                    writer.Serialize(wfile, userInf);
                    wfile.Close();
                }                
            }
            catch (IOException ioe)
            { Console.WriteLine("An IOException has occurred during serialisation: {0}", ioe.Message); }
            catch (Exception e)
            { Console.WriteLine("An error has occurred during serialisation: {0}", e.Message); }
            */
        }
        private bool setupEndpoint(out IPAddress ipAddress, out IPEndPoint remoteEP)
        {
            ipAddress = null; remoteEP = null;

            IPWindow windIP = new IPWindow();
            //windIP.Owner = this;
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
        private async Task ShowAsync()
        {
            myWindow.ShowDialog();
        }

        private async Task listen()
        {
            while (active)
            {
                await Task.Delay(2500).ConfigureAwait(false); //this needs to come before the accepting, otherwise the server menu task is blocked

                //execution is suspended while waiting for an incoming connection.
                //ServerClientConnection newConnection = new ServerClientConnection(clientListener.Accept(), this); //blocks the window...
                await Task.Run(() =>
                {
                    try
                    { ServerClientConnection newConnection = new ServerClientConnection(clientListener.Accept(), this); }
                    catch (SocketException)
                    { }
                    catch (ObjectDisposedException)
                    { }
                });
            }
            myWindow.Close();
        }

        public bool tryLogin(string username, string password)
        {
            try
            {
                foreach (KeyValuePair<string, string> kvp in userInf) //WIP: this could throw an exception if a new user registers while the loop is executed
                {
                    if (username == kvp.Key)
                    {
                        if (password == kvp.Value)
                        {
                            if (!getOnlineUsers().Contains(username))
                            { return true; }                            
                        }
                    }
                }
            }
            catch (Exception)
            { }
            return false;
        }
        public bool tryRegister(string username, string password)
        {
            if (!userInf.ContainsKey(username))
            {
                if (username.ToLower() == "server" || username == "") return false;

                userInf.Add(username, password); //lbUsers will be updated soon afterwards (see sub in ServerClientConnection here this method is triggered from)
                SaveUserInf();
                return true;
            }
            return false;
        }

        private List<string> sortList(List<string> input)
        {
            List<string> sorted = new List<string>();
            foreach (string item in input)
            {
                if (sorted.Count == 0)
                {
                    sorted.Add(item);
                }
                else
                {
                    if (String.Compare(sorted[0], item, true) > 0)
                    { sorted.Insert(0, item); }
                    else
                    {
                        if (sorted.Count < 2)
                        {
                            sorted.Add(item);
                        }
                        else
                        {
                            bool inserted = false;
                            for (int i = 0; i <= sorted.Count - 2; i++)
                            {
                                if (String.Compare(sorted[i], item, true) < 0 && String.Compare(sorted[i + 1], item, true) > 0)
                                {
                                    sorted.Insert(i + 1, item);
                                    inserted = true;
                                    break;
                                }
                            }
                            if (!inserted)
                            { sorted.Add(item); }
                        }
                    }
                }
            }
            return sorted;
        }

        //UI-related methods
        private delegate int ObjectAdderDelegate(object obj);
        private delegate void ChatMsgAdderDelegate(ChatMessage msg);
        private delegate Task UpdaterDelegate(List<string> onlineUsers, List<string> regUsers);
        public List<string> getRegisteredUsers()
        {
            List<string> output = new List<string>();
            while (true)
            {
                try
                {
                    foreach (string name in userInf.Keys) //this could throw an exception if a new user registers while the loop is executed
                    { output.Add(name); }
                }
                catch (Exception e)
                {
                    output.Clear();
                    addSystemMessage($"An exception has occurred while trying to list all registered users: {e.Message}\nRetrying...");
                    continue;
                }
                break;
            }

            return sortList(output);
        }
        public List<string> getOnlineUsers()
        {
            List<string> output = new List<string>();
            for (int i = 0; i < activeConnections.Count; i++)
            {
                if (activeConnections[i].online)
                { output.Add(activeConnections[i].username); }
            }

            return sortList(output);
        }
        public ServerClientConnection getConnectionFromUsername(string username)
        {
            for (int i = 0; i < activeConnections.Count; i++)
            {
                if (activeConnections[i].username == username)
                { return activeConnections[i]; }
            }
            return null;
        }
        public async Task<ClientInfo> getClientInfoFromConnection(string username)
        {
            var connection = getConnectionFromUsername(username);
            if (connection != null)
            {
                await connection.sendCommand(Commands.ServerCommands.RequestClientInfo);
                await connection.awaitClientResponse(Commands.ClientCommands.SubmitClientInfo);
                connection.removeClientResponse(Commands.ClientCommands.SubmitClientInfo);
                return (ClientInfo)connection.receivedData;
            }
            return null;
        }
        public Dictionary<string, string> getUserInf()
        { return userInf; }
        public void addSystemMessage(string msg)
        {
            myWindow.lbSystemMessages.Dispatcher.BeginInvoke(new ObjectAdderDelegate(myWindow.lbSystemMessages.Items.Add), System.Windows.Threading.DispatcherPriority.Background, msg);
        }
        public void updateUserList()
        {
            myWindow.Dispatcher.BeginInvoke(new UpdaterDelegate(myWindow.updateUserList), System.Windows.Threading.DispatcherPriority.Background, getOnlineUsers(), getRegisteredUsers());
        }

        public async Task<bool> sendMessage(ChatMessage msg)
        {
            if (msg.global)
            {
                myWindow.Dispatcher.BeginInvoke(new ChatMsgAdderDelegate(myWindow.addChatMessage), System.Windows.Threading.DispatcherPriority.Background, msg);

                for (int i = 0; i < activeConnections.Count; i++)
                { if (activeConnections[i].username != msg.sender) await activeConnections[i].sendMessage(msg); }
                return true;
            }
            else
            {
                if (msg.sender == "Server" || isCheckBoxChecked(myWindow.cbPrivateMessages))
                {
                    myWindow.Dispatcher.BeginInvoke(new ChatMsgAdderDelegate(myWindow.addChatMessage), System.Windows.Threading.DispatcherPriority.Background, msg);
                }

                for (int i = 0; i < activeConnections.Count; i++)
                {
                    if (activeConnections[i].username == msg.recipient)
                    { await activeConnections[i].sendMessage(msg); return true; }
                }
                if (msg.sender.ToLower() == "server")
                {
                    if (getOnlineUsers().Contains(msg.recipient))
                    { addSystemMessage("Message could not be sent."); }
                    else
                    { addSystemMessage("Message could not be sent (user is offline)."); }
                }
                else
                { addSystemMessage("Forwarding failed."); }                
                return false;
            }
        }
        public async Task deleteUser(string username)
        {
            //kick if online
            if (getOnlineUsers().Contains(username))
            {
                for (int i = 0; i < activeConnections.Count; i++) //not a foreach because it would throw an exception if a new user came online in the meantime
                {
                    if (activeConnections[i].username == username)
                    { await activeConnections[i].Shutdown(); break; }
                }
            }

            //delete user
            userInf.Remove(username);
            SaveUserInf();
            addSystemMessage($"{username}'s account has been deleted.");

            if (isCheckBoxChecked(myWindow.cbUsers))
            { updateUserList(); }
            else
            { await updateClientUserLists(); }
        }
        public async Task updateClientUserLists()
        {
            Console.WriteLine("Updating client user lists...");
            var userList = getOnlineUsers();
            Console.WriteLine("online users:");
            foreach (string name in userList) Console.WriteLine(name);
            for (int i = 0; i < activeConnections.Count; i++)
            { await activeConnections[i].sendObject(Commands.ServerCommands.OnlineUsersChanged, userList); }
        }
        private bool isCheckBoxChecked(CheckBox cb)
        {
            //original idea https://stackoverflow.com/questions/9266908/reading-checkbox-status-from-another-thread
            bool? isCbChecked = null;
            cb.Dispatcher.Invoke(new Action(
                () => isCbChecked = cb.IsChecked));
            return isCbChecked ?? false; //if isCbChecked==null, false is returned, otherwise isCbChecked is returned
        }


        //shutdown
        public void Dispose()
        {
            active = false;

            if (activeConnections.Count > 0)
            {
                try
                {
                    for (int i = 0; i < activeConnections.Count; i++)
                    { activeConnections[i].Dispose(); }
                }
                catch (Exception)
                { }
            }

            try
            { bufS.Close(); bufS.Dispose(); }
            catch (Exception)
            { }

            try
            { netS.Close(); netS.Dispose(); }
            catch (Exception)
            { }

            try
            { clientListener.Shutdown(SocketShutdown.Both); } //release socket
            catch (Exception)
            { }

            try
            { clientListener.Dispose(); }
            catch (Exception)
            { }

            try
            { myWindow.Close(); }
            catch (Exception)
            { }
        }
    }
}
