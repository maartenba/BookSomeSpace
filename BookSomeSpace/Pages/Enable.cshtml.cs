using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using JetBrains.Space.AspNetCore.Authentication.Space;
using JetBrains.Space.Client;
using JetBrains.Space.Common;
using JetBrains.Space.Common.Utilities;

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

        public readonly List<SelectListItem> HoursOfDay =
            Enumerable.Range(0, 23)
                .Select(it => new SelectListItem(it + ":00", it.ToString()))
                .ToList();
        
        [BindProperty, DisplayName("Enable BookSomeSpace")] public bool Enabled { get; set; }
        [BindProperty, Range(0, 23), DisplayName("Meetings can start at (UTC)")] public int MinHourUtc { get; set; }
        [BindProperty, Range(0, 23), DisplayName("Meetings should end at (UTC)")] public int MaxHourUtc { get; set; }
        [BindProperty, Range(0, 23), DisplayName("Hours to book beforehand")] public int MinScheduleNoticeInHours { get; set; }
        [BindProperty, DisplayName("Notify via chat when meeting is booked")] public bool NotifyViaChat { get; set; }
        
        [TempData] public string? BookUrl { get; set; }
        
        public async Task<IActionResult> OnGet()
        {
            TDMemberProfile profile;
            try
            {
                profile = await _teamDirectoryClient.Profiles.GetProfileAsync(ProfileIdentifier.Id(User.Identity.GetClaimValue(SpaceClaimTypes.UserId)));
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            
            BookUrl = Request.GetDisplayUrl().SubstringBefore("/enable", StringComparison.OrdinalIgnoreCase)
                      + Url.Page(nameof(Index), new { username = profile.Username });

            // If no settings exist, enable booking with default values
            if (!_settingsStorage.HasSettings(profile.Username))
            {
                await _settingsStorage.Store(profile.Username, new BookSomeSpaceSettings
                {
                    Enabled = true,
                    Username = profile.Username,
                    MinHourUtc = 7,
                    MaxHourUtc = 15,
                    MinScheduleNoticeInHours = 1,
                    NotifyViaChat = true
                });
            }

            var settings = await _settingsStorage.Retrieve(profile.Username);
            Enabled = settings.Enabled;
            MinHourUtc = settings.MinHourUtc;
            MaxHourUtc = settings.MaxHourUtc;
            MinScheduleNoticeInHours = settings.MinScheduleNoticeInHours;
            NotifyViaChat = settings.NotifyViaChat;

            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            TDMemberProfile profile;
            try
            {
                profile = await _teamDirectoryClient.Profiles.GetProfileAsync(ProfileIdentifier.Id(User.Identity.GetClaimValue(SpaceClaimTypes.UserId)));
            }
            catch (NotFoundException)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var settings = await _settingsStorage.Retrieve(profile.Username);
                
                settings.Enabled = Enabled;
                settings.MinHourUtc = MinHourUtc;
                settings.MaxHourUtc = MaxHourUtc;
                settings.MinScheduleNoticeInHours = MinScheduleNoticeInHours;
                settings.NotifyViaChat = NotifyViaChat;
                
                await _settingsStorage.Store(profile.Username, settings);
                
                return RedirectToPage("Enable");
            }
            
            return await OnGet();
        }
    }
}