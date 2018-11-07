using System;
using System.Collections.Generic;
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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wallpaper.Properties;
using Image = System.Drawing.Image;

namespace Wallpaper
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cancellationTokenSource;
        private DirectoryInfo mainDirectory, tempDirectoryInfo;
        private string mainDir;
        private bool pause;
        private Task task;
        private static FileStream bitmapStreamSource;
        private string tempFolderName = "wallpaper_temps";
        private List<FileInfo> images;
        private DispatcherOperation imagePreviewTask;

        public MainWindow()
        {
            InitializeComponent();
            SetBookmarks();
            TextBoxDirectory.Focus();
            UpdateSettings();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            CancelCurrentTask();
            DeleteTempFolder();
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
            if (bookmarks.Contains(dir.FullName)) return;
            bookmarks += $",{dir.FullName}";
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
        }

        private string CreateImage(string path)
        {
            try
            {
                int width = 2560, height = 1440;
                var image = Image.FromFile(path);
                var rightImageRatio = image.Width / (double) image.Height;

                var fileInfo = new FileInfo(path);
                var tempFile = $"{tempDirectoryInfo.FullName}/" + fileInfo.Name;
                if (File.Exists(tempFile)) return tempFile;

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
                        bitmap.Save(tempFile, ImageFormat.Jpeg);
                        return tempFile;
                    }
                }
            }
            catch
            {
                return null;
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
            var bookmarks = bookmarkString.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries);
            Bookmarks.ItemsSource = bookmarks;
        }

        private void Start()
        {
            SetDirectory();
            cancellationTokenSource = new CancellationTokenSource();
            var includeSubFolders = CheckBoxIncludeSubFolders.IsChecked.HasValue &&
                                    CheckBoxIncludeSubFolders.IsChecked.Value;
            task = Task.Run(async () =>
            {
                if (!mainDirectory.Exists) return;
                images = mainDirectory.GetImageFiles();
                if (includeSubFolders)
                    GetFilesInSubFolders(mainDirectory, images);

                if (!images.Any()) return;

                images.Shuffle();
                var files = images.Select(x => x.FullName).ToList();
                var i = 1;
                var file = GetFilePath(files, 0);

                while (true)
                {
                    if (cancellationTokenSource.Token.IsCancellationRequested)
                        break;
                    if (!pause)
                    {
                        if (i >= files.Count) i = 0;
                        WallpaperManager.Set(file, WallpaperManager.Style.Centered);

                        file = GetFilePath(files, i);
                        SetPreviewImage(() => { SetPreviewImage(file); });
                        i++;
                    }
                    await WaitSeconds(10);
                }
            }, cancellationTokenSource.Token);
        }

        private string GetFilePath(IReadOnlyList<string> files, int i)
        {
            var file = files[i];

            using (var bitmap = new Bitmap(file))
            {
                if (bitmap.Width >= bitmap.Height) return file;

                file = CreateImage(file);
            }
            return file;
        }

        private void SetPreviewImage(Action action)
        {
            imagePreviewTask = Dispatcher.BeginInvoke(DispatcherPriority.Background, action);
        }

        private void SetPreviewImage(string path)
        {
            try
            {
                ClearBitmapStream();

                var bitmap = new BitmapImage();
                bitmapStreamSource = File.OpenRead(path);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.None;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = bitmapStreamSource;
                bitmap.EndInit();
                ImagePreview.Source = bitmap;
            }
            catch
            {
                //Sometimes there's a problem with the meta data of the image
                //so just fail silently
            }
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
            mainDirectory = Directory.Exists(folderName)
                ? new DirectoryInfo(folderName)
                : new DirectoryInfo($"{mainDir}/{folderName}");

            tempDirectoryInfo = new DirectoryInfo($"{mainDirectory.FullName}/{tempFolderName}");
            
            if (tempDirectoryInfo.Exists) return;
            tempDirectoryInfo.Create();
        }

        private void CancelCurrentTask()
        {
            if (cancellationTokenSource == null) return;

            cancellationTokenSource.Cancel();
            try
            {
                task.Wait();
                imagePreviewTask?.Wait();
            }
            finally
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
                pause = false;
                ButtonPause.Content = "_Pause";

                ClearBitmapStream();
            }
        }

        private void ClearBitmapStream()
        {
            if (bitmapStreamSource == null) return;
            bitmapStreamSource.Close();
            bitmapStreamSource.Dispose();
            bitmapStreamSource = null;
            GC.Collect();
        }

        private void GetFilesInSubFolders(DirectoryInfo dir, List<FileInfo> result)
        {
            var subDirectories = dir.GetDirectories().Where(x => x.Name != tempFolderName).ToList();
            if (!subDirectories.Any()) return;
            foreach (var subDirectory in subDirectories)
            {
                result.AddRange(subDirectory.GetImageFiles());
                GetFilesInSubFolders(subDirectory, result);
            }
        }

        private void DeleteTempFolder()
        {
            if (tempDirectoryInfo != null && Directory.Exists(tempDirectoryInfo.FullName))
                Directory.Delete(tempDirectoryInfo.FullName, true);
        }
    }
}