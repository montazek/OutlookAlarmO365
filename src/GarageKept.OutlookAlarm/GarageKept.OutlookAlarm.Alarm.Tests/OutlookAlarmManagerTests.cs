using System.Reflection;
using GarageKept.OutlookAlarm.Alarm.AlarmManager;
using GarageKept.OutlookAlarm.Alarm.AlarmSources;
using GarageKept.OutlookAlarm.Alarm.Interfaces;
using GarageKept.OutlookAlarm.Alarm.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GarageKept.OutlookAlarm.Alarm.Tests;

[TestClass]
public class OutlookAlarmManagerTests
{
    [TestMethod]
    public void DismissedAlarm_DoesNotRegainTimerDuringGraphRefresh()
    {
        var source = new FakeAlarmSource(CreateAlarm("dismissed", DateTime.Now.AddHours(2)));
        using var manager = CreateManager(source);
        manager.ForceFetch();

        var displayedAlarm = source.CurrentAlarm;
        manager.ChangeAlarmState(displayedAlarm, AlarmAction.Dismiss);
        manager.ForceFetch();

        Assert.AreEqual(0, GetDictionaryCount(manager, "AlarmTimers"));
    }

    [TestMethod]
    public void TimerReview_RecreatesMissingTimer()
    {
        var source = new FakeAlarmSource(CreateAlarm("repair", DateTime.Now.AddHours(2)));
        using var manager = CreateManager(source);
        manager.ForceFetch();

        InvokePrivate(manager, "RemoveAlarmTimerLocked", source.CurrentAlarm.Id);
        Assert.AreEqual(0, GetDictionaryCount(manager, "AlarmTimers"));

        InvokePrivate(manager, "ReviewAlarmTimers", null);

        Assert.AreEqual(1, GetDictionaryCount(manager, "AlarmTimers"));
    }

    [TestMethod]
    public void ChangedGraphReminder_ReschedulesExistingTimer()
    {
        var alarm = CreateAlarm("changed", DateTime.Now.AddHours(2));
        var source = new FakeAlarmSource(alarm);
        using var manager = CreateManager(source);
        manager.ForceFetch();
        var changedReminder = alarm.Start.AddMinutes(-30);

        source.CurrentAlarm.ReminderTime = changedReminder;
        manager.ForceFetch();

        Assert.AreEqual(changedReminder, GetScheduledTime(manager, alarm.Id));
    }

    [TestMethod]
    public void Snooze_RemainsScheduledAfterGraphRefresh()
    {
        var source = new FakeAlarmSource(CreateAlarm("snooze", DateTime.Now.AddHours(2)));
        using var manager = CreateManager(source);
        manager.ForceFetch();
        var beforeSnooze = DateTime.Now;

        manager.ChangeAlarmState(source.CurrentAlarm, AlarmAction.SnoozeFiveMin);
        manager.ForceFetch();

        var scheduledFor = GetScheduledTime(manager, source.CurrentAlarm.Id);
        Assert.IsTrue(scheduledFor >= beforeSnooze.AddMinutes(5));
        Assert.IsTrue(scheduledFor <= DateTime.Now.AddMinutes(5));
    }

    [TestMethod]
    public async Task CalendarRefreshAndExpiration_CanPublishWithoutDeadlock()
    {
        var source = new FakeAlarmSource(CreateAlarm("concurrent", DateTime.Now.AddHours(2)));
        using var manager = CreateManager(source);
        manager.ForceFetch();
        var displayedAlarm = GetAlarm(manager, source.CurrentAlarm.Id);
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        long refreshVersion = 0;
        long expirationVersion = 0;
        manager.AlarmsUpdatedCallback = (version, alarms) =>
        {
            if (alarms.Any())
            {
                refreshVersion = version;
                callbackEntered.Set();
                releaseCallback.Wait(TimeSpan.FromSeconds(5));
            }
            else
            {
                expirationVersion = version;
            }
        };

        var fetchTask = Task.Run(manager.ForceFetch);
        Assert.IsTrue(callbackEntered.Wait(TimeSpan.FromSeconds(2)), "The refresh callback did not start.");

        try
        {
            displayedAlarm.End = DateTime.Now.AddSeconds(-1);
            var stateChangeTask = Task.Run(() =>
                manager.ChangeAlarmState(displayedAlarm, AlarmAction.Dismiss));
            var completedTask = await Task.WhenAny(stateChangeTask, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.AreSame(stateChangeTask, completedTask,
                "The state change waited for a UI update, indicating that the synchronization lock was still held.");
            await stateChangeTask;
            Assert.IsTrue(expirationVersion > refreshVersion,
                "The expiration update should be newer than the in-flight calendar refresh.");
        }
        finally
        {
            releaseCallback.Set();
            await fetchTask;
        }
    }

    [TestMethod]
    public void ExpiredAlarm_RepeatedDismissPublishesRemovalOnlyOnce()
    {
        var source = new FakeAlarmSource(CreateAlarm("expired", DateTime.Now.AddHours(2)));
        using var manager = CreateManager(source);
        manager.ForceFetch();
        var displayedAlarm = GetAlarm(manager, source.CurrentAlarm.Id);
        displayedAlarm.End = DateTime.Now.AddSeconds(-1);
        var updateCount = 0;
        IReadOnlyList<IAlarm>? latestAlarms = null;
        manager.AlarmsUpdatedCallback = (_, alarms) =>
        {
            updateCount++;
            latestAlarms = alarms.ToArray();
        };

        manager.ChangeAlarmState(displayedAlarm, AlarmAction.Dismiss);
        manager.ChangeAlarmState(displayedAlarm, AlarmAction.Dismiss);

        Assert.AreEqual(1, updateCount);
        Assert.IsNotNull(latestAlarms);
        Assert.AreEqual(0, latestAlarms.Count);
        Assert.AreEqual(1, GetDictionaryCount(manager, "DismissedAlarms"));
    }

    [TestMethod]
    public void CalendarRefresh_DoesNotAddAlreadyExpiredEvent()
    {
        var expiredAlarm = CreateAlarm("expired-source", DateTime.Now.AddHours(-1));
        expiredAlarm.IsReminderEnabled = false;
        var source = new FakeAlarmSource(expiredAlarm);
        using var manager = CreateManager(source);
        IReadOnlyList<IAlarm>? displayedAlarms = null;
        manager.AlarmsUpdatedCallback = (_, alarms) => displayedAlarms = alarms.ToArray();

        manager.ForceFetch();

        Assert.IsNotNull(displayedAlarms);
        Assert.AreEqual(0, displayedAlarms.Count);
        Assert.AreEqual(0, GetDictionaryCount(manager, "Alarms"));
    }

    private static OutlookAlarmManager CreateManager(FakeAlarmSource source) =>
        new(source, new FakeSettings());

    private static Appointment CreateAlarm(string id, DateTime start) => new()
    {
        Id = id,
        Name = id,
        Start = start,
        End = start.AddMinutes(30),
        ReminderTime = start.AddMinutes(-15),
        IsReminderEnabled = true,
        IsActive = true,
        IsAudible = true
    };

    private static int GetDictionaryCount(OutlookAlarmManager manager, string propertyName)
    {
        var dictionary = GetPrivateProperty(manager, propertyName);
        return (int)dictionary.GetType().GetProperty("Count")!.GetValue(dictionary)!;
    }

    private static DateTime GetScheduledTime(OutlookAlarmManager manager, string alarmId)
    {
        var dictionary = GetPrivateProperty(manager, "AlarmTimers");
        var registration = dictionary.GetType().GetProperty("Item")!.GetValue(dictionary, new object[] { alarmId })!;
        return (DateTime)registration.GetType().GetProperty("ScheduledFor")!.GetValue(registration)!;
    }

    private static IAlarm GetAlarm(OutlookAlarmManager manager, string alarmId)
    {
        var dictionary = GetPrivateProperty(manager, "Alarms");
        return (IAlarm)dictionary.GetType().GetProperty("Item")!.GetValue(dictionary, new object[] { alarmId })!;
    }

    private static object GetPrivateProperty(OutlookAlarmManager manager, string propertyName) =>
        typeof(OutlookAlarmManager)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(manager)!;

    private static void InvokePrivate(OutlookAlarmManager manager, string methodName, object? argument)
    {
        typeof(OutlookAlarmManager)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, new[] { argument });
    }

    private sealed class FakeAlarmSource : IAlarmSource
    {
        public FakeAlarmSource(Appointment alarm) { CurrentAlarm = alarm; }

        public Appointment CurrentAlarm { get; }

        public IEnumerable<IAlarm> GetAlarms(int hours)
        {
            yield return Clone(CurrentAlarm);
        }

        public void Dispose() { }

        private static Appointment Clone(Appointment alarm) => new()
        {
            Id = alarm.Id,
            Name = alarm.Name,
            Start = alarm.Start,
            End = alarm.End,
            ReminderTime = alarm.ReminderTime,
            IsReminderEnabled = alarm.IsReminderEnabled,
            IsActive = alarm.IsActive,
            IsAudible = alarm.IsAudible,
            AlarmColor = alarm.AlarmColor,
            CustomSound = alarm.CustomSound,
            HasCustomSound = alarm.HasCustomSound,
            Organizer = alarm.Organizer,
            Categories = new List<string>(alarm.Categories),
            Location = alarm.Location,
            TeamsMeetingUrl = alarm.TeamsMeetingUrl
        };
    }

    private sealed class FakeSettings : ISettings
    {
        public AlarmSettings Alarm { get; set; } = new();
        public AudioSettings Audio { get; set; } = new();
        public AlarmSourceSettings AlarmSource { get; set; } = new();
        public ColorSettings Color { get; set; } = new();
        public MainSettings Main { get; set; } = new();
        public TimeManagementSettings TimeManagement { get; set; } = new();
        public GraphSettings Graph { get; set; } = new();
    }
}
