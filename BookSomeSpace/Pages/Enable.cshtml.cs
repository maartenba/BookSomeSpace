using System;
using System.IO;
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
        private readonly LocalStorage _localStorage;
        private readonly ILogger<EnableModel> _logger;

        public EnableModel(
            TeamDirectoryClient teamDirectoryClient,
            LocalStorage localStorage,
            ILogger<EnableModel> logger)
        {
            _teamDirectoryClient = teamDirectoryClient;
            _localStorage = localStorage;
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

            await System.IO.File.WriteAllTextAsync(Path.Combine(_localStorage.RootPath, profile.Username.ToLowerInvariant()), profile.Username);
            
            BookUrl = Request.GetDisplayUrl().SubstringBefore("/enable", StringComparison.OrdinalIgnoreCase)
                      + Url.Page(nameof(Index), new { username = profile.Username });

            return Page();
        }
    }
}