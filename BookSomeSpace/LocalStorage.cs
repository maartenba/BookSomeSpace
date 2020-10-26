using System.IO;

#nullable enable

namespace BookSomeSpace
{
    public class LocalStorage
    {
        public string RootPath { get; }

        public LocalStorage(string rootPath)
        {
            RootPath = rootPath;
            if (!Directory.Exists(RootPath))
            {
                Directory.CreateDirectory(RootPath);
            }
        }
    }
}