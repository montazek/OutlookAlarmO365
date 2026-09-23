namespace GarageKept.OutlookAlarm.Alarm.Settings;

public sealed class GraphSettings : SettingsBase
{
    public const string DefaultClientId = "3a1efa33-d7f2-414b-99f4-d40a0d878489";
    public const string DefaultTenantId = "f3aeef27-32e1-498e-9e4c-fcfd22434441";

    private string _clientId = DefaultClientId;
    private string _tenantId = DefaultTenantId;

    public GraphSettings(Action? save = null) : base(save) { }

    public string ClientId
    {
        get => _clientId;
        set
        {
            _clientId = string.IsNullOrWhiteSpace(value) ? DefaultClientId : value.Trim();
            Save?.Invoke();
        }
    }

    public string TenantId
    {
        get => _tenantId;
        set
        {
            _tenantId = string.IsNullOrWhiteSpace(value) ? DefaultTenantId : value.Trim();
            Save?.Invoke();
        }
    }
}
