using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using JetBrains.Space.Client;
using JetBrains.Space.Client.CalendarEventSpecPartialBuilder;
using JetBrains.Space.Client.MeetingPartialBuilder;
using JetBrains.Space.Client.TDMemberProfilePartialBuilder;
using JetBrains.Space.Common;

#nullable enable

namespace BookSomeSpace.Pages;

public class IndexModel : PageModel
{
    private const string MeetingTitlePrefix = "[BookSomeSpace] Meeting with ";
        
    private readonly TeamDirectoryClient _teamDirectoryClient;
    private readonly AbsenceClient _absenceClient;
    private readonly CalendarClient _calendarClient;
    private readonly ChatClient _chatClient;
    private readonly SettingsStorage _settingsStorage;
    private readonly string _meetingUrlTemplate;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        TeamDirectoryClient teamDirectoryClient, 
        AbsenceClient absenceClient, 
        CalendarClient calendarClient,
        ChatClient chatClient,
        SettingsStorage settingsStorage,
        IConfiguration configuration,
        ILogger<IndexModel> logger)
    {
        _teamDirectoryClient = teamDirectoryClient;
        _absenceClient = absenceClient;
        _calendarClient = calendarClient;
        _chatClient = chatClient;
        _settingsStorage = settingsStorage;
        _meetingUrlTemplate = configuration["Space:ServerUrl"].TrimEnd('/') + "/meetings/{0}";
        _logger = logger;
    }

    public string DisplayName { get; set; } = default!;
    public Dictionary<DateTime, bool> Availability { get; set; } = new();
        
    public DateTime NextWeek { get; set; } = default!;
    public DateTime PreviousWeek { get; set; } = default!;

    [BindProperty, Required, DisplayName("When")] public DateTime When { get; set; } = default!;
    [BindProperty, Required, DisplayName("Name")] public string Name { get; set; } = default!;
    [BindProperty, Required, DataType(DataType.EmailAddress), DisplayName("E-mail address")] public string Email { get; set; } = default!;
    [BindProperty, Required, DisplayName("Summary")] public string Summary { get; set; } = default!;
        
    [TempData] public string? SuccessMessage { get; set; } = default!;

    public async Task<IActionResult> OnGet(DateTime? startDate, string? username)
    {
        if (string.IsNullOrEmpty(username)) return NotFound();
            
        TDMemberProfile profile;
        try
        {
            profile = await _teamDirectoryClient.Profiles.GetProfileAsync(ProfileIdentifier.Username(username), _ => _
                .WithAllFieldsWildcard()
                .WithHolidays());
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

        // Absences
        var absences = await _absenceClient.GetAllAbsencesAsyncEnumerable(AbsenceListMode.All,
            members: new List<string> { profile.Id }, since: startingAfter, till: endingBefore).ToListAsync();
            
        unavailabilities.AddRange(absences.Select(it => new Unavailability(it.Id, it.Since, it.Till)));

        // Holidays
        var holidays = profile.Holidays.Where(it => !it.IsWorkingDay);
        unavailabilities.AddRange(holidays.Select(it => new Unavailability(it.Id, it.Date, it.Date.AddDays(1))));
            
        // Meetings
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

        // Recurring meetings
        var recurringMeetings = meetings.Where(it => it.OccurrenceRule.RecurrenceRule != null).ToList();
        foreach (var recurringMeeting in recurringMeetings)
        {
            var meetingOccurrences = await _calendarClient.Meetings.GetMeetingOccurrencesForPeriodAsync(recurringMeeting.Id, startingAfter, endingBefore);
            unavailabilities.AddRange(meetingOccurrences.Select(it => new Unavailability(recurringMeeting.Id, it.Start, it.End)));
        }
            
        // Working hours - don't book outside working hours
        var workingDays = await _teamDirectoryClient.Profiles.WorkingDays
            .QueryWorkingDaysForAProfileAsyncEnumerable(ProfileIdentifier.Id(profile.Id)).ToListAsync();
        var workingHours = workingDays.FirstOrDefault(it =>
            (it.DateStart <= startingAfter && it.DateEnd >= endingBefore) ||
            (it.DateStart == null && it.DateEnd == null));            
        if (workingHours?.WorkingDaysSpec.WorkingHours != null)
        {
            foreach (var day in startingAfter.EachDayUntil(endingBefore))
            {
                var spaceDayOfWeek = day.DayOfWeek switch
                {
                    DayOfWeek.Sunday => 0,
                    DayOfWeek.Monday => 1,
                    DayOfWeek.Tuesday => 2,
                    DayOfWeek.Wednesday => 3,
                    DayOfWeek.Thursday => 4,
                    DayOfWeek.Friday => 5,
                    DayOfWeek.Saturday => 6,
                    _ => 0
                };
                var timeInterval = workingHours.WorkingDaysSpec.WorkingHours.FirstOrDefault(it => it.Day == spaceDayOfWeek);
                if (timeInterval != null)
                {
                    unavailabilities.Add(new Unavailability("WH" + workingHours.Id, day.WithHour(0).WithMinute(0).WithSecond(0), day.WithHour(timeInterval.Interval.Since.Hours).WithMinute(timeInterval.Interval.Since.Minutes).WithSecond(0)));
                    unavailabilities.Add(new Unavailability("WH" + workingHours.Id, day.WithHour(timeInterval.Interval.Till.Hours).WithMinute(timeInterval.Interval.Till.Minutes).WithSecond(0), day.WithHour(23).WithMinute(59).WithSecond(59)));
                }
            }
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
                
            var meeting = await _calendarClient.Meetings.CreateMeetingAsync(
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
                organizer: profile.Id,
                    
                conferenceData: new EventConferenceData(EventConferenceKind.GOOGLEMEET)
            );

            if (profileSettings.NotifyViaChat)
            {
                await _chatClient.Messages.SendMessageAsync(
                    recipient: MessageRecipient.Member(ProfileIdentifier.Id(profile.Id)),
                    content: ChatMessage.Text("📅 A new meeting was booked.\n\n" + string.Format(_meetingUrlTemplate, meeting.Id)),
                    unfurlLinks: true);
            }
                
            SuccessMessage = "Thank you, a meeting has been booked!";
                
            return RedirectToPage("Index", new { startDate, username });
        }
            
        return await OnGet(startDate, username);
    }
        
    private static DateTime StartOfWeek(DateTime dt, DayOfWeek startOfWeek)
    {
        var diff = (7 + (dt.Date.DayOfWeek - startOfWeek)) % 7;
        return dt.AddDays(-1 * diff).Date;
    }
}