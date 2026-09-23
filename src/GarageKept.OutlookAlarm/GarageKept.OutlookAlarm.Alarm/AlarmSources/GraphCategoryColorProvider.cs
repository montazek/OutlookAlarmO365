namespace GarageKept.OutlookAlarm.Alarm.AlarmSources.Graph;

/// <summary>
/// Keeps category-color handling isolated so Graph master-category colors can be
/// added later without changing Appointment or the UI. For now the Graph build
/// deliberately uses the normal Windows control color, matching uncategorized
/// appointments in the classic Outlook build.
/// </summary>
internal static class GraphCategoryColorProvider
{
    public static Color GetAlarmColor(IReadOnlyList<string> categories)
    {
        // TODO: Optionally read /me/outlook/masterCategories and map the first
        // event category to its Graph preset color. Do not remove AlarmColor;
        // AlarmControl and AlarmForm already use it.
        return SystemColors.Control;
    }
}
