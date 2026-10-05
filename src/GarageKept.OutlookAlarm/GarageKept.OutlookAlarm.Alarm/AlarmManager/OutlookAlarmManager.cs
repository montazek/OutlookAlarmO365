using System.Collections.Concurrent;
using System.Diagnostics;
using GarageKept.OutlookAlarm.Alarm.Diagnostics;
using GarageKept.OutlookAlarm.Alarm.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Timer = System.Threading.Timer;

namespace GarageKept.OutlookAlarm.Alarm.AlarmManager;

public sealed class OutlookAlarmManager : IAlarmManager
{
    private static readonly TimeSpan AlarmTimerReviewInterval = TimeSpan.FromMinutes(1);

    public OutlookAlarmManager(IAlarmSource alarmSource, ISettings settings)
    {
        AlarmSource = alarmSource;
        Settings = settings;
        AlarmsUpdatedCallback = delegate { };
    }

    private IAlarmSource AlarmSource { get; }
    private ConcurrentDictionary<string, IAlarm> Alarms { get; } = new();
    private ConcurrentDictionary<string, AlarmTimerRegistration> AlarmTimers { get; } = new();
    private ConcurrentDictionary<string, byte> DismissedAlarms { get; } = new();
    private ConcurrentDictionary<string, byte> TriggeredAlarms { get; } = new();
    private ConcurrentDictionary<string, DateTime> LocalReminderTimes { get; } = new();
    private ISettings Settings { get; }
    private object SynchronizationLock { get; } = new();
    private Timer? UpdateAlarmListTimer { get; set; }
    private Timer? AlarmTimerReviewTimer { get; set; }

    public IAlarmManager.UpdateAlarmList AlarmsUpdatedCallback { get; set; }

    public void ChangeAlarmState(IAlarm alarm, AlarmAction action)
    {
        lock (SynchronizationLock)
        {
            if (!Alarms.TryGetValue(alarm.Id, out var currentAlarm)) return;

            switch (action)
            {
                case AlarmAction.FifteenMinBefore:
                case AlarmAction.TenMinBefore:
                case AlarmAction.FiveMinBefore:
                case AlarmAction.ZeroMinBefore:
                case AlarmAction.SnoozeFiveMin:
                case AlarmAction.SnoozeTenMin:
                    DismissedAlarms.TryRemove(alarm.Id, out _);
                    TriggeredAlarms.TryRemove(alarm.Id, out _);
                    UpdateReminderTime(currentAlarm, action);
                    LocalReminderTimes[alarm.Id] = currentAlarm.ReminderTime;
                    ScheduleAlarmTimerLocked(currentAlarm, currentAlarm.ReminderTime, $"user action {action}");
                    OutlookAlarmLog.Write(
                        $"Alarm '{currentAlarm.Name}' ({currentAlarm.Id}) changed to {action}; reminder={currentAlarm.ReminderTime:O}.");
                    break;
                case AlarmAction.Dismiss:
                    DismissedAlarms[alarm.Id] = 0;
                    TriggeredAlarms.TryRemove(alarm.Id, out _);
                    LocalReminderTimes.TryRemove(alarm.Id, out _);
                    RemoveAlarmTimerLocked(alarm.Id);
                    OutlookAlarmLog.Write($"Dismissed alarm '{currentAlarm.Name}' ({currentAlarm.Id}).");
                    break;
                case AlarmAction.Remove:
                    DeactivateAlarmLocked(currentAlarm);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }
    }

    public void ForceFetch() { FetchAlarms(null); }

    public void Reset()
    {
        lock (SynchronizationLock)
        {
            ClearLocalStateLocked();
            OutlookAlarmLog.Write("Reset all local alarms and timers.");
            FetchAlarmsCore();
        }
    }

    public void Start()
    {
        lock (SynchronizationLock)
        {
            UpdateAlarmListTimer?.Dispose();
            AlarmTimerReviewTimer?.Dispose();

            UpdateAlarmListTimer = new Timer(FetchAlarms, null, TimeSpan.FromSeconds(1),
                TimeSpan.FromMinutes(Settings.AlarmSource.FetchIntervalInMinutes));
            AlarmTimerReviewTimer = new Timer(ReviewAlarmTimers, null, TimeSpan.FromSeconds(1),
                AlarmTimerReviewInterval);
            OutlookAlarmLog.Write("Alarm manager started; timer review interval is 60 seconds.");
        }
    }

    public void Stop()
    {
        lock (SynchronizationLock)
        {
            UpdateAlarmListTimer?.Dispose();
            UpdateAlarmListTimer = null;
            AlarmTimerReviewTimer?.Dispose();
            AlarmTimerReviewTimer = null;
            OutlookAlarmLog.Write("Alarm manager periodic work stopped.");
        }
    }

    public void Dispose()
    {
        Stop();
        lock (SynchronizationLock)
        {
            foreach (var registration in AlarmTimers.Values) registration.Timer.Dispose();
            AlarmTimers.Clear();
        }

        AlarmSource.Dispose();
    }

    private void AddAlarmLocked(IAlarm alarm)
    {
        if (!Alarms.TryAdd(alarm.Id, alarm)) return;

        OutlookAlarmLog.Write(
            $"Added alarm '{alarm.Name}' ({alarm.Id}); start={alarm.Start:O}, reminder={alarm.ReminderTime:O}, enabled={alarm.IsReminderEnabled}.");
        EnsureAlarmTimerLocked(alarm, "new Graph event");
    }

    private void AlarmCallback(object? state)
    {
        if (state is string alarmId) TriggerAlarm(alarmId, "scheduled timer");
    }

    private void TriggerAlarm(string alarmId, string source)
    {
        IAlarm? alarm;

        lock (SynchronizationLock)
        {
            if (!Alarms.TryGetValue(alarmId, out alarm) || !alarm.IsActive || !alarm.IsReminderEnabled ||
                DismissedAlarms.ContainsKey(alarmId) || !TriggeredAlarms.TryAdd(alarmId, 0))
                return;

            RemoveAlarmTimerLocked(alarmId);
            OutlookAlarmLog.Write(
                $"Triggering alarm '{alarm.Name}' ({alarm.Id}) via {source}; reminder={GetEffectiveReminderTime(alarm):O}, now={DateTime.Now:O}.");
        }

        try
        {
            var alarmWindow = Program.ServiceProvider?.GetRequiredService<IAlarmForm>();
            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (mainForm is null)
                throw new InvalidOperationException("No application window is open.");

            mainForm.Invoke(delegate { alarmWindow?.Show(alarm); });
        }
        catch (Exception exception)
        {
            lock (SynchronizationLock) TriggeredAlarms.TryRemove(alarmId, out _);
            OutlookAlarmLog.Write($"Unable to display alarm '{alarm.Name}' ({alarm.Id}); it will be retried.", exception);
        }
    }

    private void DeactivateAlarmLocked(IAlarm alarm)
    {
        alarm.IsActive = false;
        DismissedAlarms[alarm.Id] = 0;
        TriggeredAlarms.TryRemove(alarm.Id, out _);
        LocalReminderTimes.TryRemove(alarm.Id, out _);
        RemoveAlarmTimerLocked(alarm.Id);
        OutlookAlarmLog.Write($"Deactivated alarm '{alarm.Name}' ({alarm.Id}).");
        OnAlarmsUpdated();
    }

    private void FetchAlarms(object? signature)
    {
        try
        {
            lock (SynchronizationLock)
            {
                FetchAlarmsCore();
            }
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"Unable to refresh calendar alarms: {exception}");
            OutlookAlarmLog.Write("Unable to refresh calendar alarms.", exception);
        }
    }

    private void FetchAlarmsCore()
    {
        RemoveOldAlarmsLocked();

        var alarmsToCheck = GetActiveAlarms().Select(a => a.Id).ToHashSet();
        var alarms = GetAlarmsFromSource(Settings.AlarmSource.FetchTimeInHours).ToList();

        foreach (var alarm in alarms) alarmsToCheck.Remove(alarm.Id);

        foreach (var alarmId in alarmsToCheck)
            if (Alarms.TryGetValue(alarmId, out var orphan))
                ForgetAlarmLocked(orphan);

        foreach (var alarm in alarms)
            if (Alarms.ContainsKey(alarm.Id))
                UpdateAlarmLocked(alarm);
            else
                AddAlarmLocked(alarm);

        OnAlarmsUpdated();
    }

    private IEnumerable<IAlarm> GetActiveAlarms() => Alarms.Values.Where(a => a.IsActive).OrderBy(a => a.Start);

    private IEnumerable<IAlarm> GetAlarmsFromSource(int hours)
    {
        var alarms = AlarmSource.GetAlarms(hours).ToList();

        if (hours >= 24) return alarms;

        var deactivatedAlarms = Alarms.Values.Where(a => !a.IsActive).ToList();
        foreach (var remove in deactivatedAlarms
                     .Select(deactivatedAlarm =>
                         alarms.FirstOrDefault(a => a.Id == deactivatedAlarm.Id && a.Start == deactivatedAlarm.Start))
                     .Where(remove => remove != null))
            if (remove != null)
                alarms.Remove(remove);

        if (!alarms.Any()) alarms = GetAlarmsFromSource(hours + Settings.AlarmSource.FetchTimeInHours).ToList();

        return alarms;
    }

    private void ReviewAlarmTimers(object? state)
    {
        List<string> overdueAlarmIds = new();

        try
        {
            lock (SynchronizationLock)
            {
                RemoveOldAlarmsLocked();
                var now = DateTime.Now;

                foreach (var alarm in Alarms.Values)
                {
                    if (!alarm.IsActive || !alarm.IsReminderEnabled || DismissedAlarms.ContainsKey(alarm.Id) ||
                        TriggeredAlarms.ContainsKey(alarm.Id))
                    {
                        RemoveAlarmTimerLocked(alarm.Id);
                        continue;
                    }

                    var reminderTime = GetEffectiveReminderTime(alarm);
                    if (reminderTime <= now)
                        overdueAlarmIds.Add(alarm.Id);
                    else
                        EnsureAlarmTimerLocked(alarm, "60-second review");
                }
            }

            foreach (var alarmId in overdueAlarmIds) TriggerAlarm(alarmId, "60-second recovery review");
        }
        catch (Exception exception)
        {
            OutlookAlarmLog.Write("Unable to review alarm timers.", exception);
        }
    }

    private void EnsureAlarmTimerLocked(IAlarm alarm, string reason)
    {
        if (!alarm.IsActive || !alarm.IsReminderEnabled || DismissedAlarms.ContainsKey(alarm.Id) ||
            TriggeredAlarms.ContainsKey(alarm.Id))
        {
            RemoveAlarmTimerLocked(alarm.Id);
            return;
        }

        var reminderTime = GetEffectiveReminderTime(alarm);
        if (AlarmTimers.TryGetValue(alarm.Id, out var existing) && existing.ScheduledFor == reminderTime) return;

        var scheduleReason = existing is null
            ? reason == "new Graph event" ? reason : $"missing timer detected during {reason}"
            : $"reminder changed during {reason}";
        ScheduleAlarmTimerLocked(alarm, reminderTime, scheduleReason);
    }

    private void ScheduleAlarmTimerLocked(IAlarm alarm, DateTime reminderTime, string reason)
    {
        RemoveAlarmTimerLocked(alarm.Id);

        var timer = new Timer(AlarmCallback, alarm.Id, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        AlarmTimers[alarm.Id] = new AlarmTimerRegistration(timer, reminderTime);
        var dueTime = reminderTime - DateTime.Now;
        if (dueTime <= TimeSpan.Zero) dueTime = TimeSpan.FromMilliseconds(1);
        timer.Change(dueTime, Timeout.InfiniteTimeSpan);

        OutlookAlarmLog.Write(
            $"Scheduled alarm timer for '{alarm.Name}' ({alarm.Id}); reminder={reminderTime:O}, reason={reason}.");
    }

    private DateTime GetEffectiveReminderTime(IAlarm alarm) =>
        LocalReminderTimes.TryGetValue(alarm.Id, out var reminderTime) ? reminderTime : alarm.ReminderTime;

    private void OnAlarmsUpdated() { AlarmsUpdatedCallback.Invoke(GetActiveAlarms()); }

    private void RemoveAlarmTimerLocked(string alarmId)
    {
        if (!AlarmTimers.TryRemove(alarmId, out var registration)) return;
        registration.Timer.Dispose();
    }

    private void ForgetAlarmLocked(IAlarm alarm)
    {
        RemoveAlarmTimerLocked(alarm.Id);
        Alarms.TryRemove(alarm.Id, out _);
        DismissedAlarms.TryRemove(alarm.Id, out _);
        TriggeredAlarms.TryRemove(alarm.Id, out _);
        LocalReminderTimes.TryRemove(alarm.Id, out _);
        OutlookAlarmLog.Write(
            $"Forgot local alarm '{alarm.Name}' ({alarm.Id}); it will be added again if the provider returns it.");
    }

    private void RemoveOldAlarmsLocked()
    {
        var now = DateTime.Now;
        var alarmsToRemove = Alarms.Values.Where(a => a.End < now).ToList();

        foreach (var alarm in alarmsToRemove) ForgetAlarmLocked(alarm);
    }

    private void UpdateAlarmLocked(IAlarm alarm)
    {
        var existing = Alarms[alarm.Id];
        if (LocalReminderTimes.TryGetValue(alarm.Id, out var localReminderTime))
            alarm.ReminderTime = localReminderTime;

        if (!AlarmComparer.AreEqual(existing, alarm))
        {
            Alarms[alarm.Id] = alarm;
            OutlookAlarmLog.Write(
                $"Updated alarm '{alarm.Name}' ({alarm.Id}); start={alarm.Start:O}, reminder={alarm.ReminderTime:O}.");
        }

        EnsureAlarmTimerLocked(alarm, "Graph refresh");
    }

    private static void UpdateReminderTime(IAlarm alarm, AlarmAction action)
    {
        alarm.ReminderTime = action switch
        {
            AlarmAction.FifteenMinBefore => alarm.Start.AddMinutes(-15),
            AlarmAction.TenMinBefore => alarm.Start.AddMinutes(-10),
            AlarmAction.FiveMinBefore => alarm.Start.AddMinutes(-5),
            AlarmAction.ZeroMinBefore => alarm.Start,
            AlarmAction.SnoozeFiveMin => DateTime.Now.AddMinutes(5),
            AlarmAction.SnoozeTenMin => DateTime.Now.AddMinutes(10),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    private void ClearLocalStateLocked()
    {
        foreach (var registration in AlarmTimers.Values) registration.Timer.Dispose();
        AlarmTimers.Clear();
        Alarms.Clear();
        DismissedAlarms.Clear();
        TriggeredAlarms.Clear();
        LocalReminderTimes.Clear();
    }

    private sealed record AlarmTimerRegistration(Timer Timer, DateTime ScheduledFor);
}
