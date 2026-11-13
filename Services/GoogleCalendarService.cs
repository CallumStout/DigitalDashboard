using DigitalDashboard.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public class GoogleCalendarService : ICalendarService
{
    private readonly string _credentialsPath;
    private readonly string _tokenPath;

    public GoogleCalendarService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalDashboard");

        _credentialsPath = Path.Combine(appDataPath, "google-credentials.json");
        _tokenPath = Path.Combine(appDataPath, "google-tokens");
    }

    public async Task<IReadOnlyList<CalendarEventModel>> GetUpcomingEventsAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(_credentialsPath);

        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            [CalendarService.Scope.CalendarReadonly],
            "digitaldashboard-user",
            cancellationToken,
            new FileDataStore(_tokenPath, true));

        using var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DigitalDashboard"
        });

        var request = service.Events.List("primary");

        var now = DateTimeOffset.Now;

        request.TimeMinDateTimeOffset = now;
        request.TimeMaxDateTimeOffset = now.AddDays(14);
        request.MaxResults = 10;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var response = await request.ExecuteAsync(cancellationToken);

        var events = (response.Items ?? [])
            .Select(item => new CalendarEventModel
            {
                Title = item.Summary ?? "Untitled event",

                Start = item.Start.DateTimeDateTimeOffset?.LocalDateTime
                    ?? DateTime.Parse(item.Start.Date),

                End = item.End.DateTimeDateTimeOffset?.LocalDateTime
                    ?? (item.End.Date is not null
                        ? DateTime.Parse(item.End.Date)
                        : null),

                IsAllDay = item.Start.Date is not null
            })
            .ToList();

        return events;
    }
}
