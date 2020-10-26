using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SpaceDotNet.Client;
using SpaceDotNet.Common;
using SpaceDotNet.Common.Utilities;

#nullable enable

namespace BookSomeSpace.Pages
{
    [Authorize]
    public class EnableModel : PageModel
    {
        private readonly TeamDirectoryClient _teamDirectoryClient;
        private readonly SettingsStorage _settingsStorage;
        private readonly ILogger<EnableModel> _logger;

        public EnableModel(
            TeamDirectoryClient teamDirectoryClient,
            SettingsStorage settingsStorage,
            ILogger<EnableModel> logger)
        {
            _teamDirectoryClient = teamDirectoryClient;
            _settingsStorage = settingsStorage;
            _logger = logger;
        }

        [TempData] public string? BookUrl { get; set; }
        
        public async Task<IActionResult> OnGet()
        {
            TDMemberProfile profile;
            try
            {
                profile = await _teamDirectoryClient.Profiles.GetProfileAsync(ProfileIdentifier.Me);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }

            await _settingsStorage.Store(profile.Username, new BookSomeSpaceSettings
            {
                Enabled = true,
                Username = profile.Username,
                MinHourUtc = 7,
                MaxHourUtc = 15
            });
            
            BookUrl = Request.GetDisplayUrl().SubstringBefore("/enable", StringComparison.OrdinalIgnoreCase)
                      + Url.Page(nameof(Index), new { username = profile.Username });

            return Page();
        }
    }
}