using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wallpaper.Properties;
using Image = System.Drawing.Image;

namespace Wallpaper
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cancellationTokenSource;
        private DirectoryInfo directoryInfo;
        private string mainDir, tempFolderPath, tempPath;
        private bool pause;
        private Task task;

        public MainWindow()
        {
            InitializeComponent();
            SetBookmarks();
            TextBoxDirectory.Focus();
            UpdateSettings();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            cancellationTokenSource?.Cancel();
            File.Delete(tempPath);
        }

        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentTask();
            Start();
        }

        private void Button_Restart_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentTask();
            Start();
        }

        private void Button_Pause_Click(object sender, RoutedEventArgs e)
        {
            pause = !pause;
            ButtonPause.Content = pause ? "Resume" : "_Pause";
        }

        private void Button_Bookmark_Click(object sender, RoutedEventArgs e)
        {
            var dir = new DirectoryInfo(TextBoxDirectory.Text);
            if (!dir.Exists) return;
            var bookmarks = Settings.Default.Bookmarks;
            if (bookmarks.Contains(directoryInfo.FullName)) return;
            bookmarks += $",{directoryInfo.FullName}";
            Settings.Default.Bookmarks = bookmarks;
            Settings.Default.Save();
            SetBookmarks();
        }

        private void Bookmarks_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete) return;
            var bookmarks = Settings.Default.Bookmarks;
            bookmarks = bookmarks.Replace($",{Bookmarks.SelectedItem}", string.Empty);
            Settings.Default.Bookmarks = bookmarks;
            Settings.Default.Save();
            SetBookmarks();
        }

        private void Bookmarks_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Bookmarks.SelectedItem?.ToString())) return;
            TextBoxDirectory.Text = Bookmarks.SelectedItem.ToString();
            CancelCurrentTask();
            Start();
        }

        private void Button_Settings_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow(this).ShowDialog();
        }

        public void UpdateSettings()
        {
            mainDir = Settings.Default.BasePath;
            tempFolderPath = Settings.Default.AppDataPath;
            tempPath = $"{tempFolderPath}/temp.jpg";
        }

        private void CreateImage(string path)
        {
            try
            {
                int width = 2560, height = 1440;
                var image = Image.FromFile(path);
                var rightImageRatio = image.Width / (double) image.Height;

                using (image)
                {
                    using (var bitmap = new Bitmap(width, height))
                    {
                        using (var canvas = Graphics.FromImage(bitmap))
                        {
                            canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            var rightSideWith = (int) (height * rightImageRatio);
                            var leftSideWith = width - rightSideWith;
                            var ratio = leftSideWith / (double) height;

                            canvas.DrawImage(image,
                                new Rectangle(0,
                                    0,
                                    leftSideWith,
                                    height),
                                new Rectangle(0,
                                    0,
                                    image.Width,
                                    (int) (image.Width / ratio)),
                                GraphicsUnit.Pixel);
                            canvas.DrawImage(image,
                                new Rectangle(width - rightSideWith,
                                    0,
                                    rightSideWith,
                                    height),
                                new Rectangle(0,
                                    0,
                                    image.Width,
                                    image.Height),
                                GraphicsUnit.Pixel);
                            canvas.Save();
                        }
                        bitmap.Save(tempPath, ImageFormat.Jpeg);
                    }
                }
            }
            catch
            {
            }
        }

        private void SetBookmarks()
        {
            var bookmarkString = Settings.Default.Bookmarks;
            if (string.IsNullOrWhiteSpace(bookmarkString))
            {
                Bookmarks.ItemsSource = new string[0];
                return;
            }
            var bookmarks = bookmarkString.Split(',').Where(x => !string.IsNullOrWhiteSpace(x));
            Bookmarks.ItemsSource = bookmarks;
        }

        private void Start()
        {
            SetDirectory();
            cancellationTokenSource = new CancellationTokenSource();

            task = Task.Run(async () =>
            {
                if (!directoryInfo.Exists) return;
                var files = directoryInfo.GetFiles();
                if (!files.Any()) return;
                files.Shuffle();
                var i = 0;
                while (true)
                {
                    if (cancellationTokenSource.Token.IsCancellationRequested)
                        break;
                    if (!pause)
                    {
                        if (i >= files.Length) i = 0;
                        var file = files[i];

                        var fileName = file.FullName;
                        var bitmap = new Bitmap(fileName);

                        if (bitmap.Width < bitmap.Height)
                        {
                            CreateImage(fileName);
                            fileName = tempPath;
                        }
                        WallpaperManager.Set(fileName, WallpaperManager.Style.Centered);
                        i++;
                    }
                    await WaitSeconds(10);
                }
            }, cancellationTokenSource.Token);
        }

        private async Task WaitSeconds(int seconds)
        {
            double secondsCount = 0;
            while (secondsCount < seconds)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                    break;
                secondsCount += 0.5;
                await Task.Delay(500);
            }
        }

        private void SetDirectory()
        {
            var folderName = TextBoxDirectory.Text;
            directoryInfo = Directory.Exists(folderName)
                ? new DirectoryInfo(folderName)
                : new DirectoryInfo($"{mainDir}/{folderName}");
        }

        private void CancelCurrentTask()
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            if (cancellationTokenSource == null) return;
            cancellationTokenSource.Cancel();
            try
            {
                task.Wait();
            }
            finally
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
                pause = false;
                ButtonPause.Content = pause ? "Resume" : "_Pause";
            }
        }
    }
}