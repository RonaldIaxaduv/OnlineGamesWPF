using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Communication; //defined in this project
using System.Threading.Tasks;

namespace ProgrammierprojektWPF
{
    /// <summary>
    /// Interaction logic for ServerMenu.xaml
    /// </summary>
    public partial class ServerMenu : Window, IDisposable
    {
        private uint msgBufSz = 100;
        private uint MessageBufferSize
        {
            get { return msgBufSz; }
            set
            {
                if (value > 0)
                {
                    msgBufSz = value;
                    lbSystemMessages_ItemsChanged(lbSystemMessages.Items, null);
                    lbChatMessages_ItemsChanged(lbChatMessages.Items, null);
                }
            }
        }
        public Server wrapper = null;
        private List<string> userList = new List<string>(); //used to get the username from the item selected in lbUsers

        public ServerMenu()
        {
            InitializeComponent();
            tblVersion.Text = "Version Number: " + VersionNumber.get();
            ((INotifyCollectionChanged)lbSystemMessages.Items).CollectionChanged += lbSystemMessages_ItemsChanged;
            ((INotifyCollectionChanged)lbChatMessages.Items).CollectionChanged += lbChatMessages_ItemsChanged;
        }
        public ServerMenu(Server wrapper) : this()
        {
            this.wrapper = wrapper;
        }

        public async Task updateUserList(List<string> onlineUsers, List<string> regUsers)
        {
            lbUsers.Items.Clear(); userList.Clear();
            if (cbUsers.IsChecked == true) //true: display offline users as well as online users
            {
                //display online users first
                foreach (string username in onlineUsers)
                { regUsers.Remove(username); }
                foreach (string username in onlineUsers)
                { lbUsers.Items.Add(ListBoxUserItem.generate(lbUsers.FontSize, username, true)); userList.Add(username); }
                foreach (string username in regUsers)
                { lbUsers.Items.Add(ListBoxUserItem.generate(lbUsers.FontSize, username, false)); userList.Add(username); }
                //foreach (string username in onlineUsers)
                //{ ListBoxUserItem.add(lbUsers, username, onlineUsers.Contains(username)); userList.Add(username); }
            }
            else
            {
                foreach (string username in onlineUsers)
                { lbUsers.Items.Add(ListBoxUserItem.generate(lbUsers.FontSize, username, true)); userList.Add(username); }
            }
            await wrapper.updateClientUserLists();
        }
        public void addChatMessage(ChatMessage msg)
        {
            lbChatMessages.Items.Add(ListBoxChatItem.generate(msg.sender, msg));
        }


        //controls
        private async void cmdUserInf_Click(object sender, RoutedEventArgs e)
        {
            if (lbUsers.SelectedIndex < 0)
            { MessageBox.Show("Select the user whose information you'd like to view (list on the right).", "No User Selected", MessageBoxButton.OK, MessageBoxImage.Error); }
            else
            {
                string username = userList[lbUsers.SelectedIndex];
                string pw; wrapper.userInf.TryGetValue(username, out pw);
                var cInf = await wrapper.getClientInfoFromConnection(username);

                MessageBox.Show($"Full information on {username}:\n\nIngame Status: {cInf?.displayIngameStatus()}\n\nPassword: {pw}");
                lbUsers.SelectedIndex = -1; //unselect user
            }
        }

        private async void cmdWhisper_Click(object sender, RoutedEventArgs e)
        {
            if (lbUsers.SelectedIndex < 0)
            { MessageBox.Show("Select the user whom you'd like to message (list on the right).", "No User Selected", MessageBoxButton.OK, MessageBoxImage.Error); }
            else
            {
                if (tbMessage.Text == "")
                { MessageBox.Show("Please enter a message to send (bottom-left box).", "No Message Entered", MessageBoxButton.OK, MessageBoxImage.Error); }
                else
                {
                    cmdWhisper.IsEnabled = false;
                    cmdGlobalMessage.IsEnabled = false;
                    string username = userList[lbUsers.SelectedIndex];
                    var msg = new ChatMessage("Server", tbMessage.Text, username);
                    tbMessage.Text = "";
                    lbUsers.SelectedIndex = -1; //unselect user
                    await wrapper.sendMessage(msg);
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
                var msg = new ChatMessage("Server", tbMessage.Text);
                tbMessage.Text = "";
                lbUsers.SelectedIndex = -1; //unselect user
                await wrapper.sendMessage(msg);
                cmdWhisper.IsEnabled = true;
                cmdGlobalMessage.IsEnabled = true;
            }
        }

        private async void cmdDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (lbUsers.SelectedIndex < 0)
            { MessageBox.Show("Select the user whom you'd like to delete (list on the right).", "No User Selected", MessageBoxButton.OK, MessageBoxImage.Error); }
            else
            {
                cmdDeleteUser.IsEnabled = false;
                cmdDeleteAll.IsEnabled = false;
                string username = userList[lbUsers.SelectedIndex];
                MessageBoxResult msgbRes = MessageBox.Show($"Are you sure that you want to delete {username}'s account?", "Confirmation Needed", MessageBoxButton.YesNo, MessageBoxImage.Information);
                lbUsers.SelectedIndex = -1; //unselect user
                if (msgbRes == MessageBoxResult.Yes)
                { await wrapper.deleteUser(username); }
                cmdDeleteUser.IsEnabled = true;
                cmdDeleteAll.IsEnabled = true;
            }
        }
        private async void cmdDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult msgbRes = MessageBox.Show("Are you sure that you want to delete all users?", "Confirmation Needed", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (msgbRes == MessageBoxResult.Yes)
            {
                cmdDeleteUser.IsEnabled = false;
                cmdDeleteAll.IsEnabled = false;
                var regUsers = wrapper.getRegisteredUsers();
                while (regUsers.Count > 0)
                { await wrapper.deleteUser(regUsers[0]); regUsers.RemoveAt(0); }
                cmdDeleteUser.IsEnabled = true;
                cmdDeleteAll.IsEnabled = true;
            }
        }

        private void cbUsers_Checked(object sender, RoutedEventArgs e)
        {
            updateUserList(wrapper.getOnlineUsers(), wrapper.getRegisteredUsers());
        }
        private void cbUsers_Unchecked(object sender, RoutedEventArgs e)
        {
            updateUserList(wrapper.getOnlineUsers(), wrapper.getRegisteredUsers());
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

        private void lbSystemMessages_ItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            while (((ItemCollection)sender).Count > MessageBufferSize)
            { ((ItemCollection)sender).RemoveAt(0); }
            if (lbSystemMessages.Items.Count > 0)
            { lbSystemMessages.ScrollIntoView(lbSystemMessages.Items.GetItemAt(lbSystemMessages.Items.Count - 1)); }
        }
        private void lbChatMessages_ItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            while (((ItemCollection)sender).Count > MessageBufferSize)
            { ((ItemCollection)sender).RemoveAt(0); }
            if (lbChatMessages.Items.Count > 0)
            { lbChatMessages.ScrollIntoView(lbChatMessages.Items.GetItemAt(lbChatMessages.Items.Count - 1)); }
        }


        //shutdown
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try { wrapper.clientListener.Close(); } catch (Exception) { }
            Dispose();
            MainWindow newMainWindow = new MainWindow();
            newMainWindow.Show();
        }
        public void Dispose()
        {
            wrapper = null;
        }
    }
}
