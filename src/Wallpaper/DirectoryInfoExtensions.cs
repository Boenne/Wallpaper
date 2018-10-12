using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Wallpaper
{
    public static class DirectoryInfoExtensions
    {
        public static List<FileInfo> GetImageFiles(this DirectoryInfo directoryInfo)
        {
            return directoryInfo.GetFiles().Where(x => x.FullName.EndsWith(".jpg") || x.FullName.EndsWith(".png")).ToList();
        }
    }
}