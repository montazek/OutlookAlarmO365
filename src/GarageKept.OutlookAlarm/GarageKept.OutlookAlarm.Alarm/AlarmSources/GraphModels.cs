using System.Text.Json.Serialization;

namespace GarageKept.OutlookAlarm.Alarm.AlarmSources.Graph;

internal sealed class GraphEventPage
{
    [JsonPropertyName("value")]
    public List<GraphEvent> Value { get; set; } = new();

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }
}

internal sealed class GraphEvent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("start")]
    public GraphDateTimeTimeZone? Start { get; set; }

    [JsonPropertyName("end")]
    public GraphDateTimeTimeZone? End { get; set; }

    [JsonPropertyName("isAllDay")]
    public bool IsAllDay { get; set; }

    [JsonPropertyName("isCancelled")]
    public bool IsCancelled { get; set; }

    [JsonPropertyName("isReminderOn")]
    public bool IsReminderOn { get; set; }

    [JsonPropertyName("reminderMinutesBeforeStart")]
    public int ReminderMinutesBeforeStart { get; set; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = new();

    [JsonPropertyName("organizer")]
    public GraphRecipient? Organizer { get; set; }

    [JsonPropertyName("location")]
    public GraphLocation? Location { get; set; }

    [JsonPropertyName("responseStatus")]
    public GraphResponseStatus? ResponseStatus { get; set; }

    [JsonPropertyName("onlineMeeting")]
    public GraphOnlineMeeting? OnlineMeeting { get; set; }

    [JsonPropertyName("onlineMeetingUrl")]
    public string? OnlineMeetingUrl { get; set; }
}

internal sealed class GraphDateTimeTimeZone
{
    [JsonPropertyName("dateTime")]
    public string? DateTime { get; set; }

    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; set; }
}

internal sealed class GraphRecipient
{
    [JsonPropertyName("emailAddress")]
    public GraphEmailAddress? EmailAddress { get; set; }
}

internal sealed class GraphEmailAddress
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

internal sealed class GraphLocation
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

internal sealed class GraphResponseStatus
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }
}

internal sealed class GraphOnlineMeeting
{
    [JsonPropertyName("joinUrl")]
    public string? JoinUrl { get; set; }
}
