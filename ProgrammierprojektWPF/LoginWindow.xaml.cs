using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ProgrammierprojektWPF
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private string retVal = "";
        public string ReturnValue
        {
            get { return retVal; }
            private set { retVal = value; }
        }

        public LoginWindow()
        {
            InitializeComponent();
            ReturnValue = "";
        }

        private void cmdLogin_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            ReturnValue = "login";
            Close();
        }
        private void cmdRegister_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            ReturnValue = "register";
            Close();
        }
    }
}
