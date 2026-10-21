namespace GarageKept.OutlookAlarm.Alarm.Interfaces;

public interface IMainForm : IDisposable
{
    void UpdateAlarms(long version, IEnumerable<IAlarm> alarms);
}
