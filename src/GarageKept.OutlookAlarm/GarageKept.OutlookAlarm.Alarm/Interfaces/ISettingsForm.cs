namespace GarageKept.OutlookAlarm.Alarm.Interfaces;

public interface ISettingsForm : IDisposable
{
    Form? Owner { get; set; }
    DialogResult ShowDialog();
    DialogResult ShowMicrosoft365Dialog();
}
