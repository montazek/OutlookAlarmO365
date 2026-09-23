using GarageKept.OutlookAlarm.Alarm.Interfaces;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace GarageKept.OutlookAlarm.Alarm.AlarmSources.Graph;

internal sealed class GraphSignInRequiredException : InvalidOperationException
{
    public GraphSignInRequiredException() : base(
        "Microsoft 365 sign-in is required. Open Settings, select Microsoft 365 and click Connect.") { }
}

internal sealed class GraphAuthenticationService
{
    private static readonly string[] Scopes = { "Calendars.Read" };
    private readonly ISettings _settings;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private IPublicClientApplication? _app;
    private string? _configurationKey;

    public GraphAuthenticationService(ISettings settings)
    {
        _settings = settings;
    }

    public event Action? SignInRequired;

    public async Task<string> GetAccessTokenAsync()
    {
        var app = await GetApplicationAsync().ConfigureAwait(false);
        var account = (await app.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();
        if (account is null)
        {
            SignInRequired?.Invoke();
            throw new GraphSignInRequiredException();
        }

        try
        {
            var result = await app.AcquireTokenSilent(Scopes, account).ExecuteAsync().ConfigureAwait(false);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            SignInRequired?.Invoke();
            throw new GraphSignInRequiredException();
        }
    }

    public async Task<string> ConnectAsync(IntPtr parentWindowHandle)
    {
        var app = await GetApplicationAsync().ConfigureAwait(true);
        var builder = app.AcquireTokenInteractive(Scopes)
            .WithUseEmbeddedWebView(false)
            .WithPrompt(Prompt.SelectAccount);

        if (parentWindowHandle != IntPtr.Zero)
            builder = builder.WithParentActivityOrWindow(parentWindowHandle);

        var result = await builder.ExecuteAsync().ConfigureAwait(true);
        return result.Account?.Username ?? "Connected";
    }

    public async Task<string?> GetConnectedAccountAsync()
    {
        try
        {
            var app = await GetApplicationAsync().ConfigureAwait(false);
            var account = (await app.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();
            if (account is null) return null;

            await app.AcquireTokenSilent(Scopes, account).ExecuteAsync().ConfigureAwait(false);
            return account.Username;
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    private async Task<IPublicClientApplication> GetApplicationAsync()
    {
        var clientId = _settings.Graph.ClientId.Trim();
        var tenantId = _settings.Graph.TenantId.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Microsoft 365 Client ID is required.");
        if (string.IsNullOrWhiteSpace(tenantId)) tenantId = "organizations";

        var key = $"{clientId}|{tenantId}";
        if (_app is not null && string.Equals(_configurationKey, key, StringComparison.OrdinalIgnoreCase))
            return _app;

        await _initializationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_app is not null && string.Equals(_configurationKey, key, StringComparison.OrdinalIgnoreCase))
                return _app;

            var app = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                .WithDefaultRedirectUri()
                .Build();

            var cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GarageKept.OutlookAlarm.O365");
            Directory.CreateDirectory(cacheDirectory);

            var storageProperties = new StorageCreationPropertiesBuilder("msal_cache.bin3", cacheDirectory).Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
            cacheHelper.VerifyPersistence();
            cacheHelper.RegisterCache(app.UserTokenCache);

            _app = app;
            _configurationKey = key;
            return app;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
