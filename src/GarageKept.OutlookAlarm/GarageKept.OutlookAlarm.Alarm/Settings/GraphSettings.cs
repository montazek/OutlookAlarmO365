using System.Text.Json.Serialization;

namespace GarageKept.OutlookAlarm.Alarm.Settings;

public sealed class GraphSettings : SettingsBase
{
    public const string DefaultClientId = "3a1efa33-d7f2-414b-99f4-d40a0d878489";
    public const string LegacyDefaultTenantId = "f3aeef27-32e1-498e-9e4c-fcfd22434441";
    public const string OrganizationsTenant = "organizations";

    private string _clientId = DefaultClientId;
    private string _tenantId = OrganizationsTenant;
    private bool _useCustomAppRegistration;
    private string? _selectedAccountId;
    private string? _selectedTenantId;
    private string? _selectedConfigurationKey;

    public GraphSettings() { }

    public GraphSettings(Action save) : base(save) { }

    public bool UseCustomAppRegistration
    {
        get => _useCustomAppRegistration;
        set { _useCustomAppRegistration = value; Save?.Invoke(); }
    }

    // Retained for the optional custom registration.
    public string ClientId
    {
        get => _clientId;
        set { _clientId = string.IsNullOrWhiteSpace(value) ? DefaultClientId : value.Trim(); Save?.Invoke(); }
    }

    public string TenantId
    {
        get => _tenantId;
        set { _tenantId = string.IsNullOrWhiteSpace(value) ? OrganizationsTenant : value.Trim(); Save?.Invoke(); }
    }

    public string? SelectedAccountId
    {
        get => _selectedAccountId;
        set { _selectedAccountId = value; Save?.Invoke(); }
    }

    public string? SelectedTenantId
    {
        get => _selectedTenantId;
        set { _selectedTenantId = value; Save?.Invoke(); }
    }

    public string? SelectedConfigurationKey
    {
        get => _selectedConfigurationKey;
        set { _selectedConfigurationKey = value; Save?.Invoke(); }
    }

    [JsonIgnore]
    public string EffectiveClientId => UseCustomAppRegistration ? ClientId : DefaultClientId;

    [JsonIgnore]
    public string EffectiveTenantId => UseCustomAppRegistration ? TenantId : OrganizationsTenant;

    [JsonIgnore]
    public string ConfigurationKey => $"{EffectiveClientId}|{EffectiveTenantId}";

    public void ConfigureRegistration(bool useCustom, string clientId, string tenantId)
    {
        _useCustomAppRegistration = useCustom;
        _clientId = string.IsNullOrWhiteSpace(clientId) ? DefaultClientId : clientId.Trim();
        _tenantId = string.IsNullOrWhiteSpace(tenantId) ? OrganizationsTenant : tenantId.Trim();
        Save?.Invoke();
    }

    public void SelectAccount(string accountId, string tenantId)
    {
        _selectedAccountId = accountId;
        _selectedTenantId = tenantId;
        _selectedConfigurationKey = ConfigurationKey;
        Save?.Invoke();
    }
}
