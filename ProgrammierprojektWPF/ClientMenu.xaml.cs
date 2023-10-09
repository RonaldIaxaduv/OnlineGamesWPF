using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Communication;
using System.Threading.Tasks;

namespace ProgrammierprojektWPF
{
    /// <summary>
    /// Interaction logic for ClientMenu.xaml
    /// </summary>
    public partial class ClientMenu : Window, IDisposable
    {
        private uint msgBufSz;
        private uint MessageBufferSize
        {
            get { return msgBufSz; }
            set
            {
                if (value > 0)
                {
                    msgBufSz = value;
                    lbMessages_ItemsChanged(lbChatMessages.Items, null);
                }
            }
        }
        private List<string> userList = new List<string>(); //used to get the username from the item selected in lbUsers
        public Client wrapper;

        public ClientMenu()
        {
            InitializeComponent();
            tblVersion.Text = "Version Number: " + VersionNumber.get();
            toggleGameButtons(true);
            toggleMessageButtons(true);
            ((INotifyCollectionChanged)lbChatMessages.Items).CollectionChanged += lbMessages_ItemsChanged;
        }
        public ClientMenu(Client wrapper) : this()
        {
            this.wrapper = wrapper;
        }


        public async Task updateUserList(List<string> onlineUsers)
        {
            lbUsers.Items.Clear(); userList.Clear();
            foreach (string username in onlineUsers) //clients can only display other online clients. including all offline clients would probably be a violation of privacy anyway
            { lbUsers.Items.Add(ListBoxUserItem.generate(lbUsers.FontSize, username, true)); userList.Add(username); }
        }
        public void addChatMessage(ChatMessage msg)
        {
            lbChatMessages.Items.Add(ListBoxChatItem.generate(msg.sender, msg));
        }
        

        //controls
        private async void cmdWhisper_Click(object sender, RoutedEventArgs e)
        {
            if (lbUsers.SelectedIndex < 0)
            { MessageBox.Show("Select the user whom you'd like to message.", "No User Selected", MessageBoxButton.OK, MessageBoxImage.Error); }
            else
            {
                if (tbMessage.Text == "")
                { MessageBox.Show("Please enter a message to send.", "No Message Entered", MessageBoxButton.OK, MessageBoxImage.Error); }
                else
                {
                    cmdWhisper.IsEnabled = false;
                    cmdGlobalMessage.IsEnabled = false;
                    string recipient = userList[lbUsers.SelectedIndex];
                    string msg = tbMessage.Text;
                    tbMessage.Text = "";
                    lbUsers.SelectedIndex = -1; //unselect user
                    await wrapper.requestWhisperChatMessage(recipient, msg);
                    cmdWhisper.IsEnabled = true;
                    cmdGlobalMessage.IsEnabled = true;
                }
            }
        }
        private async void cmdGlobalMessage_Click(object sender, RoutedEventArgs e)
        {
            if (tbMessage.Text == "")
            { MessageBox.Show("Please enter a message to send to all users (bottom-left box).", "No Message Entered", MessageBoxButton.OK, MessageBoxImage.Error); }
            else
            {
                cmdWhisper.IsEnabled = false;
                cmdGlobalMessage.IsEnabled = false;
                string msg = tbMessage.Text;
                tbMessage.Text = "";
                await wrapper.requestBroadcastChatMessage(msg);
                cmdWhisper.IsEnabled = true;
                cmdGlobalMessage.IsEnabled = true;
                lbUsers.SelectedIndex = -1; //unselect user
            }
        }

        private async void cmdChomp_Click(object sender, RoutedEventArgs e)
        {
            if (lbUsers.SelectedIndex < 0)
            { MessageBox.Show("Select the user whom you'd like to challenge to a game of Chomp.", "No User Selected", MessageBoxButton.OK, MessageBoxImage.Error); }
            else
            {
                toggleGameButtons(false);

                ChompSpecificationsPortable specs;
                System.Drawing.Size boardSize;
                var newChompSet = new ChompSettings();
                if (newChompSet.ShowDialog() == true)
                {
                    try
                    {
                        boardSize = new System.Drawing.Size(
                            int.Parse(newChompSet.tbWidth.Text),
                            int.Parse(newChompSet.tbHeight.Text));
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("The entered size is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        toggleGameButtons(true);
                        return;
                    }
                    var selected = userList[lbUsers.SelectedIndex];
                    var playerNames = new string[2];
                    playerNames[0] = wrapper.username;
                    playerNames[1] = selected;

                    try
                    { specs = new ChompSpecificationsPortable(boardSize, playerNames, 0); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        toggleGameButtons(true);
                        return;
                    }

                    var reply = await wrapper.requestGameChomp(specs);
                    switch (reply)
                    {
                        case Commands.ServerCommands.ServerError:
                            wrapper.removeServerResponse(reply);
                            MessageBox.Show($"There has been an error while trying to challenge {selected} to a game of Chomp.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        case Commands.ServerCommands.AlreadyIngame:
                            wrapper.removeServerResponse(reply);
                            MessageBox.Show($"{selected} is already ingame.", "Already Ingame", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        case Commands.ServerCommands.RequestDenied:
                            wrapper.removeServerResponse(reply);
                            MessageBox.Show($"{selected} has denied your request to play Chomp.", "Challenge Denied", MessageBoxButton.OK, MessageBoxImage.Information);
                            break;
                        case Commands.ServerCommands.RequestAccepted:
                            wrapper.removeServerResponse(reply);
                            wrapper.IngameInf = Client.IngameStatus.Chomp;
                            wrapper.opponent = selected;
                            MessageBox.Show($"{selected} has accepted your request to play Chomp.", "Challenge Accepted", MessageBoxButton.OK, MessageBoxImage.Information);
                            wrapper.removeServerResponse(Commands.ServerCommands.GameAborted);
                            var players = new Player[2];
                            players[0] = new Player(wrapper);
                            players[1] = new Player(Player.playerType.RemoteHuman, selected);
                            var newChomp = new Chomp(new ChompSpecifications(specs, Game.GameLocation.Client, players, clientWrapper:wrapper));
                            if (newChomp.myWindow != null) newChomp.myWindow.Owner = this;
                            return;
                        default:
                            wrapper.removeServerResponse(reply);
                            break;
                    }
                }

                toggleGameButtons(true);
            }
        }
        private void cmdChompSolo_Click(object sender, RoutedEventArgs e)
        {
            toggleGameButtons(false);

            System.Drawing.Size boardSize;
            var newChompSet = new ChompSettings();
            if (newChompSet.ShowDialog() == true)
            {
                try
                {
                    boardSize = new System.Drawing.Size(
                        int.Parse(newChompSet.tbWidth.Text),
                        int.Parse(newChompSet.tbHeight.Text));
                }
                catch (Exception)
                {
                    MessageBox.Show("The entered size is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    toggleGameButtons(true);
                    return;
                }
                wrapper.IngameInf = Client.IngameStatus.ChompSolo;
                var players = new Player[2];
                players[0] = new Player(Player.playerType.LocalHuman, wrapper.username);
                players[1] = new Player(Player.playerType.LocalComputer);
                var newChomp = new Chomp(new ChompSpecifications(Game.GameLocation.Local, boardSize, players, 0, clientWrapper: wrapper));
                if (newChomp.myWindow != null) newChomp.myWindow.Owner = this;
                return;
            }
            toggleGameButtons(true);
        }
        private async void cmdFourConnect_Click(object sender, RoutedEventArgs e)
        {
            if (lbUsers.SelectedIndex < 0)
            { MessageBox.Show("Select the user whom you'd like to challenge to a game of Connect Four.", "No User Selected", MessageBoxButton.OK, MessageBoxImage.Error); }
            else
            {
                toggleGameButtons(false);

                ConnectFourSpecificationsPortable specs;
                System.Drawing.Size boardSize;
                var newChompSet = new ConnectFourSettings();
                if (newChompSet.ShowDialog() == true)
                {
                    try
                    {
                        boardSize = new System.Drawing.Size(
                            int.Parse(newChompSet.tbWidth.Text),
                            int.Parse(newChompSet.tbHeight.Text));
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("The entered size is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        toggleGameButtons(true);
                        return;
                    }
                    var selected = userList[lbUsers.SelectedIndex];
                    var playerNames = new string[2];
                    playerNames[0] = wrapper.username;
                    playerNames[1] = selected;

                    try
                    { specs = new ConnectFourSpecificationsPortable(boardSize, playerNames, 0); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        toggleGameButtons(true);
                        return;
                    }

                    var reply = await wrapper.requestGameConnectFour(specs);
                    switch (reply)
                    {
                        case Commands.ServerCommands.ServerError:
                            wrapper.removeServerResponse(reply);
                            MessageBox.Show($"There has been an error while trying to challenge {selected} to a game of Connect Four.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        case Commands.ServerCommands.AlreadyIngame:
                            wrapper.removeServerResponse(reply);
                            MessageBox.Show($"{selected} is already ingame.", "Already Ingame", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        case Commands.ServerCommands.RequestDenied:
                            wrapper.removeServerResponse(reply);
                            MessageBox.Show($"{selected} has denied your request to play Connect Four.", "Challenge Denied", MessageBoxButton.OK, MessageBoxImage.Information);
                            break;
                        case Commands.ServerCommands.RequestAccepted:
                            wrapper.removeServerResponse(reply);
                            wrapper.IngameInf = Client.IngameStatus.ConnectFour;
                            wrapper.opponent = selected;
                            MessageBox.Show($"{selected} has accepted your request to play Connect Four.", "Challenge Accepted", MessageBoxButton.OK, MessageBoxImage.Information);
                            wrapper.removeServerResponse(Commands.ServerCommands.GameAborted);
                            var players = new Player[2];
                            players[0] = new Player(wrapper);
                            players[1] = new Player(Player.playerType.RemoteHuman, selected);
                            var newConnectFour = new ConnectFour(new ConnectFourSpecifications(specs, Game.GameLocation.Client, players, clientWrapper: wrapper));
                            if (newConnectFour.myWindow != null) newConnectFour.myWindow.Owner = this;
                            return;
                        default:
                            wrapper.removeServerResponse(reply);
                            break;
                    }
                }

                toggleGameButtons(true);
            }
        }
        private void cmdFourConnectSolo_Click(object sender, RoutedEventArgs e)
        {
            toggleGameButtons(false);

            System.Drawing.Size boardSize;
            var newChompSet = new ConnectFourSettings();
            if (newChompSet.ShowDialog() == true)
            {
                try
                {
                    boardSize = new System.Drawing.Size(
                        int.Parse(newChompSet.tbWidth.Text),
                        int.Parse(newChompSet.tbHeight.Text));
                }
                catch (Exception)
                {
                    MessageBox.Show("The entered size is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    toggleGameButtons(true);
                    return;
                }
                wrapper.IngameInf = Client.IngameStatus.ConnectFourSolo;
                var players = new Player[2];
                players[0] = new Player(Player.playerType.LocalHuman, wrapper.username);
                players[1] = new Player(Player.playerType.LocalComputer);
                var newConnectFour = new ConnectFour(new ProgrammierprojektWPF.ConnectFourSpecifications(Game.GameLocation.Local, boardSize, players, 0, clientWrapper: wrapper));
                if (newConnectFour.myWindow != null) newConnectFour.myWindow.Owner = this;
                return;
            }
            toggleGameButtons(true);
        }
        private void cmdLogout_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void toggleGameButtons(bool enabled)
        {
            cmdFourConnect.IsEnabled = enabled;
            cmdFourConnectSolo.IsEnabled = enabled;
            cmdChomp.IsEnabled = enabled;
            cmdChompSolo.IsEnabled = enabled;
        }
        private void toggleMessageButtons(bool enabled)
        {
            cmdWhisper.IsEnabled = enabled;
            cmdGlobalMessage.IsEnabled = enabled;
        }


        private void tbBuffer_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                uint input = uint.Parse(tbBuffer.Text);
                MessageBufferSize = input;
            }
            catch (Exception)
            { tbBuffer.Text = "1000"; }
        }

        private void lbMessages_ItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            while (((ItemCollection)sender).Count > MessageBufferSize)
            { ((ItemCollection)sender).RemoveAt(0); }
            ((ItemCollection)sender).MoveCurrentToLast();
            if (lbChatMessages.Items.Count > 0)
            { lbChatMessages.ScrollIntoView(lbChatMessages.Items.GetItemAt(lbChatMessages.Items.Count - 1)); }
        }


        //shutdown
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try { wrapper.sender.Close(); } catch (Exception) { }
            Dispose(); //sever connection
            MainWindow newMainWindow = new MainWindow();
            newMainWindow.Show();
        }
        public void Dispose()
        {
            wrapper = null;
        }
    }
}
