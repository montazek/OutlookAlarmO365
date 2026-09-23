namespace GarageKept.OutlookAlarm.Alarm.Diagnostics;

internal static class OutlookAlarmLog
{
    private const long MaximumLogSize = 1024 * 1024;
    private static readonly object SyncRoot = new();

    internal static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GarageKept", "OutlookAlarmO365", "outlook-alarm-o365.log");

    internal static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (SyncRoot)
            {
                var directory = Path.GetDirectoryName(FilePath)!;
                Directory.CreateDirectory(directory);

                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaximumLogSize)
                    File.Move(FilePath, FilePath + ".old", true);

                var details = exception is null ? string.Empty : Environment.NewLine + exception;
                File.AppendAllText(FilePath,
                    $"{DateTime.Now:O} [{Environment.ProcessId}] {message}{details}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never interfere with reminders.
        }
    }
}
