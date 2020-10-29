using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace BookSomeSpace
{
    public class BookSomeSpaceSettings
    {
        public bool Enabled { get; set; }
        public string Username { get; set; }
        public int MinHourUtc { get; set; } = 7;
        public int MaxHourUtc { get; set; } = 15;
        public int MinScheduleNoticeInHours { get; set; } = 1;
        public bool NotifyViaChat { get; set; } = true;
    }
    
    public class SettingsStorage
    {
        private readonly string _rootPath;
        private readonly ILogger<SettingsStorage> _logger;

        public SettingsStorage(string rootPath, ILogger<SettingsStorage> logger)
        {
            _rootPath = rootPath;
            _logger = logger;
            
            if (!Directory.Exists(_rootPath))
            {
                Directory.CreateDirectory(_rootPath);
            }
        }
        
        public bool HasSettings(string username) => 
            File.Exists(Path.Combine(_rootPath, username.ToLowerInvariant()));

        public async Task<BookSomeSpaceSettings> Retrieve(string username) 
        {
            _logger.LogTrace("Retrieve settings for {username}...", username);
            if (!File.Exists(Path.Combine(_rootPath, username.ToLowerInvariant())))
            {
                _logger.LogInformation("No settings found for {username} - returning default.", username);
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
            _logger.LogTrace("Store settings for {username}...", username);
            await File.WriteAllTextAsync(Path.Combine(_rootPath, username.ToLowerInvariant()),
                JsonSerializer.Serialize(settings));
        }
    }
}