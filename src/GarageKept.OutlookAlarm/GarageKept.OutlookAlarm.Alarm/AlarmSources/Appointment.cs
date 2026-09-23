using GarageKept.OutlookAlarm.Alarm.Interfaces;

namespace GarageKept.OutlookAlarm.Alarm.AlarmSources;

public class Appointment : IAlarm
{
    public Appointment()
    {
        CustomSound = string.Empty;
        Id = string.Empty;
        Location = string.Empty;
        Name = string.Empty;
        Organizer = string.Empty;
        TeamsMeetingUrl = string.Empty;
        AlarmColor = SystemColors.Control;
    }

    public double Duration { get; set; }
    public bool IsOwnEvent { get; set; }
    public ResponseType Response { get; set; }
    public Color AlarmColor { get; set; }
    public List<string> Categories { get; set; } = new();
    public string CustomSound { get; set; }
    public DateTime End { get; set; }
    public bool HasCustomSound { get; set; }
    public string Id { get; set; }
    public bool IsActive { get; set; }
    public bool IsAudible { get; set; }
    public bool IsReminderEnabled { get; set; }
    public string Location { get; set; }
    public string Name { get; set; }
    public string Organizer { get; set; }
    public DateTime ReminderTime { get; set; }
    public DateTime Start { get; set; }
    public string TeamsMeetingUrl { get; set; }
}
