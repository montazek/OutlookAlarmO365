using System.Text.Json;
using GarageKept.OutlookAlarm.Alarm.Interfaces;

namespace GarageKept.OutlookAlarm.Alarm.Settings;

public class OutlookAlarmSettings : ISettings
{
    private static bool _loadedOnce;

    public OutlookAlarmSettings()
    {
        Alarm = new AlarmSettings(Save);
        AlarmSource = new AlarmSourceSettings(Save);
        Audio = new AudioSettings(Save);
        Color = new ColorSettings(Save);
        Graph = new GraphSettings(Save);
        Main = new MainSettings(Save);
        TimeManagement = new TimeManagementSettings(Save);

        if (_loadedOnce) return;

        _loadedOnce = true;
        var settings = Load();

        Alarm = settings.Alarm;
        AlarmSource = settings.AlarmSource;
        Audio = settings.Audio;
        Color = settings.Color;
        Graph = settings.Graph ?? new GraphSettings();
        Main = settings.Main;
        TimeManagement = settings.TimeManagement;

        Alarm.Save = Save;
        AlarmSource.Save = Save;
        Audio.Save = Save;
        Color.Save = Save;
        Graph.Save = Save;
        Main.Save = Save;
        TimeManagement.Save = Save;
    }

    public AlarmSettings Alarm { get; set; }
    public AlarmSourceSettings AlarmSource { get; set; }
    public AudioSettings Audio { get; set; }
    public ColorSettings Color { get; set; }
    public GraphSettings Graph { get; set; }
    public MainSettings Main { get; set; }
    public TimeManagementSettings TimeManagement { get; set; }

    private static string GetAppFolder()
    {
        const string appFolder = @"GarageKept\OutlookAlarmO365\";
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsFilePath = Path.Combine(appDataPath, appFolder);

        if (!Directory.Exists(settingsFilePath)) Directory.CreateDirectory(settingsFilePath);

        return settingsFilePath;
    }

    private static JsonSerializerOptions GetJsonSerializeOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new ColorJsonConverter() }
        };
    }

    private static string GetSettingsFile()
    {
        const string settingsFilePath = @"settings.json";

        var fullPath = Path.Combine(GetAppFolder(), settingsFilePath);

        return fullPath;
    }

    public static OutlookAlarmSettings Load()
    {
        OutlookAlarmSettings? settings;
        var migrateGraphSettings = false;

        if (File.Exists(GetSettingsFile()))
        {
            var settingsJson = File.ReadAllText(GetSettingsFile());

            if (string.IsNullOrWhiteSpace(settingsJson)) return new OutlookAlarmSettings();

            var options = GetJsonSerializeOptions();

            // Use the JsonSerializer to deserialize the settings.
            settings = JsonSerializer.Deserialize<OutlookAlarmSettings>(settingsJson, options) ??
                       new OutlookAlarmSettings();

            // Older files did not have a registration mode. Preserve a user's custom
            // registration; only the previously bundled pair becomes the new default.
            using (var document = JsonDocument.Parse(settingsJson))
            {
                var graphElement = document.RootElement.EnumerateObject()
                    .FirstOrDefault(property => property.Name.Equals("Graph", StringComparison.OrdinalIgnoreCase)).Value;
                var hasMode = graphElement.ValueKind == JsonValueKind.Object &&
                    graphElement.EnumerateObject().Any(property =>
                        property.Name.Equals("UseCustomAppRegistration", StringComparison.OrdinalIgnoreCase));
                if (!hasMode)
                {
                    var graph = settings.Graph ?? new GraphSettings();
                    var isBundledPair = string.Equals(graph.ClientId, GraphSettings.DefaultClientId,
                            StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(graph.TenantId, GraphSettings.LegacyDefaultTenantId,
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(graph.TenantId, GraphSettings.OrganizationsTenant,
                             StringComparison.OrdinalIgnoreCase));
                    graph.UseCustomAppRegistration = !isBundledPair;
                    settings.Graph = graph;
                    migrateGraphSettings = true;
                }
            }

            settings.Alarm.Save = settings.Save;
            settings.AlarmSource.Save = settings.Save;
            settings.Audio.Save = settings.Save;
            settings.Audio.Save = settings.Save;
            settings.Color.Save = settings.Save;
            settings.Graph ??= new GraphSettings();
            settings.Graph.Save = settings.Save;
            settings.Main.Save = settings.Save;
            settings.TimeManagement.Save = settings.Save;
            if (migrateGraphSettings) settings.Save();
        }
        else
        {
            // If the settings file doesn't exist, create a new settings object.
            settings = new OutlookAlarmSettings();
            settings.Save();
        }

        return settings;
    }

    public void Save()
    {
        var options = GetJsonSerializeOptions();
        var settingsJson = JsonSerializer.Serialize(this, options);
        File.WriteAllText(GetSettingsFile(), settingsJson);
    }
}
