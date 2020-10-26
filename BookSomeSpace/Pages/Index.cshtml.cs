using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SpaceDotNet.Client;
using SpaceDotNet.Client.CalendarEventSpecPartialBuilder;
using SpaceDotNet.Client.MeetingPartialBuilder;
using SpaceDotNet.Common;

#nullable enable

namespace BookSomeSpace.Pages
{
    public class IndexModel : PageModel
    {
        private const string MeetingTitlePrefix = "[BookSomeSpace] Meeting with ";
        
        private readonly TeamDirectoryClient _teamDirectoryClient;
        private readonly AbsenceClient _absenceClient;
        private readonly CalendarClient _calendarClient;
        private readonly SettingsStorage _settingsStorage;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            TeamDirectoryClient teamDirectoryClient, 
            AbsenceClient absenceClient, 
            CalendarClient calendarClient,
            SettingsStorage settingsStorage,
            ILogger<IndexModel> logger)
        {
            _teamDirectoryClient = teamDirectoryClient;
            _absenceClient = absenceClient;
            _calendarClient = calendarClient;
            _settingsStorage = settingsStorage;
            _logger = logger;
        }

        public string DisplayName { get; set; }
        public Dictionary<DateTime, bool> Availability { get; set; } = new Dictionary<DateTime, bool>();
        
        public DateTime NextWeek { get; set; }
        public DateTime PreviousWeek { get; set; }

        [BindProperty, Required] public DateTime When { get; set; }
        [BindProperty, Required] public string Name { get; set; }
        [BindProperty, Required, DataType(DataType.EmailAddress)] public string Email { get; set; }
        [BindProperty, Required] public string Summary { get; set; }
        
        [TempData] public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGet(DateTime? startDate, string? username)
        {
            if (string.IsNullOrEmpty(username)) return NotFound();
            
            TDMemberProfile profile;
            try
            {
                profile = await _teamDirectoryClient.Profiles.GetProfileAsync(ProfileIdentifier.Username(username));
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            
            var profileSettings = await _settingsStorage.Retrieve(profile.Username);
            if (!profileSettings.Enabled) return NotFound();
            
            DisplayName = $"{profile.Name.FirstName} {profile.Name.LastName}";
            
            var startingAfter = startDate.HasValue
                ? StartOfWeek(startDate.Value, DayOfWeek.Monday)
                : StartOfWeek(DateTime.UtcNow, DayOfWeek.Monday);
            var endingBefore = startingAfter.AddDays(5);

            NextWeek = startingAfter.AddDays(7);
            PreviousWeek = startingAfter.AddDays(-7);

            var unavailabilities = new List<Unavailability>();

            var absences = await _absenceClient.GetAllAbsencesAsyncEnumerable(AbsenceListMode.All,
                members: new List<string> { profile.Id }, since: startingAfter, till: endingBefore).ToListAsync();
            
            unavailabilities.AddRange(absences.Select(it => new Unavailability(it.Id, it.Since, it.Till)));
            
            var meetings = await _calendarClient.Meetings.GetAllMeetingsAsyncEnumerable(profiles: new List<string> { profile.Id }, includePrivate: true, includeArchived: false, includeMeetingInstances: true, 
                startingAfter: startingAfter,
                endingBefore: endingBefore, partial: _ => _
                    .WithId()
                    .WithSummary()
                    .WithOccurrenceRule(occurrence => occurrence
                        .WithStart()
                        .WithEnd()
                        .WithRecurrenceRule(recurrence => recurrence
                            .WithAllFieldsWildcard())
                        .WithIsAllDay()
                        .WithTimezone(timezone => timezone.WithAllFieldsWildcard())))
                .OrderBy(it => it.OccurrenceRule.Start)
                .ToListAsync();

            unavailabilities.AddRange(meetings.Select(it => new Unavailability(it.Id, it.OccurrenceRule.Start, it.OccurrenceRule.End)));

            var recurringMeetings = meetings.Where(it => it.OccurrenceRule.RecurrenceRule != null).ToList();
            foreach (var recurringMeeting in recurringMeetings)
            {
                var meetingOccurrences = await _calendarClient.Meetings.GetMeetingOccurrencesForPeriodAsync(recurringMeeting.Id, startingAfter, endingBefore);
                unavailabilities.AddRange(meetingOccurrences.Select(it => new Unavailability(recurringMeeting.Id, it.Start, it.End)));
            }
            
            var currentDateTime = startingAfter;
            while (currentDateTime < endingBefore)
            {
                if (currentDateTime.Hour < profileSettings.MinHourUtc)
                    currentDateTime = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, profileSettings.MinHourUtc, 0, 0);
                
                Availability[currentDateTime] = 
                    !unavailabilities.Any(it => it.Start <= currentDateTime && it.End >= currentDateTime) &&
                    currentDateTime > DateTime.UtcNow.AddHours(profileSettings.MinScheduleNoticeInHours);
                currentDateTime = currentDateTime.AddMinutes(30);

                if (currentDateTime.Hour >= profileSettings.MaxHourUtc)
                {
                    currentDateTime = currentDateTime.AddDays(1);
                    currentDateTime = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, profileSettings.MinHourUtc, 0, 0);
                }
            }
            
            return Page();
        }

        public async Task<IActionResult> OnPost(DateTime? startDate, string? username)
        {
            if (string.IsNullOrEmpty(username)) return NotFound();
            
            if (ModelState.IsValid)
            {
                TDMemberProfile profile;
                try
                {
                    profile = await _teamDirectoryClient.Profiles.GetProfileAsync(ProfileIdentifier.Username(username));
                }
                catch (NotFoundException)
                {
                    return NotFound();
                }
                
                var profileSettings = await _settingsStorage.Retrieve(profile.Username);
                if (!profileSettings.Enabled) return NotFound();

                var whenAsUtc = When.ToUniversalTime();
                
                await _calendarClient.Meetings.CreateMeetingAsync(
                    summary: MeetingTitlePrefix + Name,
                    description: MeetingTitlePrefix + Name + "\n\n" + Summary,
                    occurrenceRule: new CalendarEventSpec
                    {
                        Start = whenAsUtc,
                        End = whenAsUtc.AddMinutes(30),
                        Timezone = new ATimeZone { Id = "UTC" },
                        BusyStatus = BusyStatus.Busy,
                        IsAllDay = false
                    },
                    profiles: new List<string>
                    {
                        profile.Id
                    },
                    externalParticipants: new List<string>
                    {
                        Email
                    },
                    visibility: MeetingVisibility.PARTICIPANTS,
                    modificationPreference: MeetingModificationPreference.ORGANIZER,
                    joiningPreference: MeetingJoiningPreference.NOBODY,
        
                    notifyOnExport: true,
                    organizer: profile.Id
                );
                
                SuccessMessage = "Thank you, a meeting has been booked!";
                
                return RedirectToPage(nameof(Index), new { startDate, username });
            }
            
            return await OnGet(startDate, username);
        }
        
        private static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
        {
            var diff = (7 + (dt.Date.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }
}