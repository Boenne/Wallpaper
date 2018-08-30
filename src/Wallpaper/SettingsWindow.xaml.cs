using System.Windows;
using Wallpaper.Properties;

namespace Wallpaper
{
    /// <summary>
    ///     Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow mainWindow;

        public SettingsWindow(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow; //the lazy way!
            InitializeComponent();
            TextBoxBasePath.Text = Settings.Default.BasePath;
            TextBoxAppDataPath.Text = Settings.Default.AppDataPath;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.BasePath = TextBoxBasePath.Text;
            Settings.Default.AppDataPath = TextBoxAppDataPath.Text;
            Settings.Default.Save();
            mainWindow.UpdateSettings();
            Close();
        }
    }
}