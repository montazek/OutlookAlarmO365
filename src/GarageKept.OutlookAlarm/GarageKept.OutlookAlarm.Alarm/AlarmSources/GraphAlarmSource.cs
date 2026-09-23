using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using GarageKept.OutlookAlarm.Alarm.Diagnostics;
using GarageKept.OutlookAlarm.Alarm.AlarmSources;
using GarageKept.OutlookAlarm.Alarm.Interfaces;

namespace GarageKept.OutlookAlarm.Alarm.AlarmSources.Graph;

/// <summary>
/// Reads the signed-in user's Microsoft 365 calendar directly through Microsoft Graph.
/// It does not start or automate either Classic Outlook or New Outlook.
/// </summary>
internal sealed class GraphAlarmSource : IAlarmSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GraphAuthenticationService _authenticationService;
    private readonly HttpClient _httpClient;

    public GraphAlarmSource(GraphAuthenticationService authenticationService, HttpClient httpClient)
    {
        _authenticationService = authenticationService;
        _httpClient = httpClient;
    }

    public IEnumerable<IAlarm> GetAlarms(int hours)
    {
        return GetAlarmsAsync(hours).GetAwaiter().GetResult();
    }

    private async Task<IReadOnlyList<IAlarm>> GetAlarmsAsync(int hours)
    {
        var token = await _authenticationService.GetAccessTokenAsync().ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var end = now.AddHours(hours);

        var select = string.Join(',', new[]
        {
            "id", "subject", "start", "end", "isAllDay", "isCancelled",
            "isReminderOn", "reminderMinutesBeforeStart", "categories",
            "organizer", "location", "responseStatus", "onlineMeeting", "onlineMeetingUrl"
        });

        string? nextUrl = "https://graph.microsoft.com/v1.0/me/calendarView" +
                      $"?startDateTime={Uri.EscapeDataString(now.ToString("o", CultureInfo.InvariantCulture))}" +
                      $"&endDateTime={Uri.EscapeDataString(end.ToString("o", CultureInfo.InvariantCulture))}" +
                      $"&$select={Uri.EscapeDataString(select)}" +
                      "&$top=100";

        var appointments = new List<IAlarm>();

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // Returning UTC makes conversion deterministic regardless of the mailbox's display timezone.
            request.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"UTC\"");

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Microsoft Graph calendar request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {json}");

            var page = JsonSerializer.Deserialize<GraphEventPage>(json, JsonOptions)
                       ?? throw new InvalidOperationException("Microsoft Graph returned an empty calendar response.");

            foreach (var graphEvent in page.Value)
            {
                try
                {
                    if (graphEvent.IsAllDay || graphEvent.IsCancelled) continue;
                    if (string.IsNullOrWhiteSpace(graphEvent.Id) || graphEvent.Start is null || graphEvent.End is null)
                        continue;

                    var start = ToLocalDateTime(graphEvent.Start);
                    var eventEnd = ToLocalDateTime(graphEvent.End);
                    if (eventEnd <= DateTime.Now) continue;

                    var reminderMinutes = Math.Max(0, graphEvent.ReminderMinutesBeforeStart);
                    var categories = graphEvent.Categories ?? new List<string>();
                    var organizer = graphEvent.Organizer?.EmailAddress?.Name
                                    ?? graphEvent.Organizer?.EmailAddress?.Address
                                    ?? string.Empty;

                    appointments.Add(new Appointment
                    {
                        Id = graphEvent.Id,
                        Name = graphEvent.Subject ?? string.Empty,
                        Start = start,
                        End = eventEnd,
                        Duration = Math.Max(0, (eventEnd - start).TotalMinutes),
                        ReminderTime = start.AddMinutes(-reminderMinutes),
                        // Preserve the classic application's behaviour: every fetched meeting
                        // is alarmable. The Graph reminder offset is still respected when present.
                        IsReminderEnabled = true,
                        IsActive = true,
                        IsAudible = true,
                        CustomSound = string.Empty,
                        HasCustomSound = false,
                        Organizer = organizer,
                        Location = graphEvent.Location?.DisplayName ?? string.Empty,
                        Categories = new List<string>(categories),
                        AlarmColor = GraphCategoryColorProvider.GetAlarmColor(categories),
                        TeamsMeetingUrl = graphEvent.OnlineMeeting?.JoinUrl
                                          ?? graphEvent.OnlineMeetingUrl
                                          ?? string.Empty,
                        Response = ConvertResponse(graphEvent.ResponseStatus?.Response),
                        IsOwnEvent = string.Equals(graphEvent.ResponseStatus?.Response, "organizer", StringComparison.OrdinalIgnoreCase)
                    });
                }
                catch (Exception exception)
                {
                    OutlookAlarmLog.Write($"Skipped Graph appointment '{graphEvent.Subject ?? "<no subject>"}'.", exception);
                }
            }

            nextUrl = page.NextLink;
        }

        OutlookAlarmLog.Write($"Microsoft Graph fetch completed with {appointments.Count} appointment(s).");
        return appointments;
    }

    private static DateTime ToLocalDateTime(GraphDateTimeTimeZone value)
    {
        if (string.IsNullOrWhiteSpace(value.DateTime))
            throw new InvalidOperationException("Graph event contains no date/time value.");

        // The request asks Graph to return UTC. Graph normally returns a dateTime value
        // without a trailing Z, so explicitly mark it as UTC before converting locally.
        var parsed = DateTime.Parse(value.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces);
        var utc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        return utc.ToLocalTime();
    }

    private static ResponseType ConvertResponse(string? response)
    {
        return response?.ToLowerInvariant() switch
        {
            "accepted" => ResponseType.Accepted,
            "declined" => ResponseType.Declined,
            "notresponded" => ResponseType.NotResponded,
            "organizer" => ResponseType.Organized,
            "tentativelyaccepted" => ResponseType.Tentative,
            "none" => ResponseType.None,
            _ => ResponseType.None
        };
    }

    public void Dispose() { }
}
