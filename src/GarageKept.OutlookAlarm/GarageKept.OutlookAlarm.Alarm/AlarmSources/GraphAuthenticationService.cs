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
        var account = await GetSelectedAccountAsync(app).ConfigureAwait(false);
        if (account is null)
        {
            SignInRequired?.Invoke();
            throw new GraphSignInRequiredException();
        }

        try
        {
            var result = await AcquireTokenSilentAsync(app, account).ConfigureAwait(false);
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
        var accountId = result.Account?.HomeAccountId?.Identifier;
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(result.TenantId))
            throw new InvalidOperationException("Microsoft 365 did not return an account and tenant.");

        var graph = _settings.Graph;
        if (string.Equals(graph.SelectedConfigurationKey, graph.ConfigurationKey,
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(graph.SelectedAccountId) &&
            (!string.Equals(graph.SelectedAccountId, accountId, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(graph.SelectedTenantId, result.TenantId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "This installation is already connected to another Microsoft 365 account. " +
                "Account switching is not supported in this version.");

        graph.SelectAccount(accountId, result.TenantId);
        return result.Account?.Username ?? "Connected";
    }

    public async Task<string?> GetConnectedAccountAsync()
    {
        try
        {
            var app = await GetApplicationAsync().ConfigureAwait(false);
            var account = await GetSelectedAccountAsync(app).ConfigureAwait(false);
            if (account is null) return null;

            await AcquireTokenSilentAsync(app, account).ConfigureAwait(false);
            return account.Username;
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    private async Task<IAccount?> GetSelectedAccountAsync(IPublicClientApplication app)
    {
        var accounts = (await app.GetAccountsAsync().ConfigureAwait(false)).ToList();
        var graph = _settings.Graph;
        if (!string.IsNullOrWhiteSpace(graph.SelectedAccountId))
        {
            if (!string.Equals(graph.SelectedConfigurationKey, graph.ConfigurationKey,
                    StringComparison.OrdinalIgnoreCase)) return null;
            return accounts.FirstOrDefault(account => string.Equals(
                account.HomeAccountId?.Identifier, graph.SelectedAccountId, StringComparison.OrdinalIgnoreCase));
        }

        // Migrate an existing installation only when its cache is unambiguous.
        if (accounts.Count != 1) return null;
        try
        {
            var result = await app.AcquireTokenSilent(Scopes, accounts[0]).ExecuteAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.Account?.HomeAccountId?.Identifier) ||
                string.IsNullOrWhiteSpace(result.TenantId)) return null;
            graph.SelectAccount(result.Account.HomeAccountId.Identifier, result.TenantId);
            return accounts[0];
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    private Task<AuthenticationResult> AcquireTokenSilentAsync(IPublicClientApplication app, IAccount account)
    {
        var builder = app.AcquireTokenSilent(Scopes, account);
        if (!string.IsNullOrWhiteSpace(_settings.Graph.SelectedTenantId))
            builder = builder.WithTenantId(_settings.Graph.SelectedTenantId);
        return builder.ExecuteAsync();
    }

    private async Task<IPublicClientApplication> GetApplicationAsync()
    {
        var clientId = _settings.Graph.EffectiveClientId.Trim();
        var tenantId = _settings.Graph.EffectiveTenantId.Trim();
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
                .WithRedirectUri("http://localhost")
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
