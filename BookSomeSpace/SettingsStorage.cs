using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable

namespace BookSomeSpace
{
    public class BookSomeSpaceSettings
    {
        public bool Enabled { get; set; }
        public string Username { get; set; }
        public int MinHourUtc { get; set; }
        public int MaxHourUtc { get; set; }
    }
    
    public class SettingsStorage
    {
        private readonly string _rootPath;

        public SettingsStorage(string rootPath)
        {
            _rootPath = rootPath;
            if (!Directory.Exists(_rootPath))
            {
                Directory.CreateDirectory(_rootPath);
            }
        }
        
        public async Task<BookSomeSpaceSettings> Retrieve(string username) 
        {
            if (!File.Exists(Path.Combine(_rootPath, username.ToLowerInvariant())))
            {
                return new BookSomeSpaceSettings
                {
                    Enabled = false,
                    Username = username
                };
            }

            return JsonSerializer.Deserialize<BookSomeSpaceSettings>(
                await File.ReadAllTextAsync(Path.Combine(_rootPath, username.ToLowerInvariant())));
        }
        
        public async Task Store(string username, BookSomeSpaceSettings settings) 
        {
            await File.WriteAllTextAsync(Path.Combine(_rootPath, username.ToLowerInvariant()),
                JsonSerializer.Serialize(settings));
        }
    }
}