Outlook alarm / Microsoft 365 / Office 365 / Microsoft Graph / Reminder / Meeting
# OutlookAlarm O365 (Microsoft Graph)

A Windows desktop alarm application based on [GarageKept/OutlookAlarm](https://github.com/GarageKept/OutlookAlarm). This fork reads the signed-in user's Microsoft 365 calendar through Microsoft Graph. Classic Outlook and New Outlook do not need to be installed or running.

## Features

- Alarms based on upcoming calendar events
- Customizable sounds and interface colors
- Interactive Microsoft 365 sign-in

## Microsoft 365 connection

On first launch, Settings opens on the Microsoft 365 tab. Select **Connect**, sign in with a work or school Microsoft 365 account, and approve access if your organization permits it. The app includes its own multitenant application registration, so normal users do not need to register an app or enter a Client ID or Tenant ID. Personal Outlook.com accounts are not supported in this version.

The app requests the delegated Microsoft Graph `Calendars.Read` permission to read the signed-in user's calendar. Your organization's consent policy may require an administrator to approve this app once. If Microsoft asks for administrator approval, contact your IT team; creating another app registration is not required for normal use.

The selected account is remembered across restarts. This version is intended for one Microsoft 365 account per installation and does not offer account switching or disconnect. When Microsoft requires another interactive sign-in, open **Settings > Microsoft 365** and select **Reconnect**. The app also opens that tab when a new sign-in is required; after you close it, it waits for your next action instead of reopening it on each calendar poll.

No `appsettings.json` file is required. User settings are stored in `%APPDATA%\GarageKept\OutlookAlarmO365\settings.json`, and the protected MSAL token cache is stored under `%LOCALAPPDATA%\GarageKept.OutlookAlarm.O365`. The settings file stores account identifiers, not access or refresh tokens.

## Advanced: use your own Microsoft Entra registration

In **Settings > Microsoft 365**, select **Advanced: use my own app registration** before connecting. Enter its Application (client) ID and optionally a tenant ID or tenant domain. A blank tenant uses `organizations`. Existing installations with a custom registration keep their IDs after upgrading.

For a tenant-specific registration, configure **Authentication > Add a platform > Mobile and desktop applications** with redirect URI `http://localhost`. Add Microsoft Graph **Delegated** `Calendars.Read` under **API permissions**. Do not create a client secret for this desktop app. Depending on your organization's consent policy, an administrator may need to grant permission. See Microsoft's [desktop app configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration) and [Calendars.Read permission](https://learn.microsoft.com/en-us/graph/permissions-reference#calendarsread).

## Publisher setup

The bundled Client ID is `3a1efa33-d7f2-414b-99f4-d40a0d878489`. Its Entra registration must support **Accounts in any organizational directory**, use the desktop redirect URI `http://localhost`, and request Microsoft Graph delegated `Calendars.Read`. The app uses the `organizations` authority for normal sign-in. Test with a non-administrator account in another organization before publishing a new release. That organization's consent policy may still require its administrator to approve the shared app.

## Build

Open `src/GarageKept.OutlookAlarm/GarageKept.OutlookAlarm.O365.sln` in Visual Studio 2022, or use the .NET 8 SDK from the command line. From the repository root, create either Windows x64 single-file build:

```powershell
dotnet publish src/GarageKept.OutlookAlarm/GarageKept.OutlookAlarm.Alarm/GarageKept.OutlookAlarm.Alarm.csproj -p:PublishProfile=FrameworkDependent
dotnet publish src/GarageKept.OutlookAlarm/GarageKept.OutlookAlarm.Alarm/GarageKept.OutlookAlarm.Alarm.csproj -p:PublishProfile=Portable
```

The output is under `src/GarageKept.OutlookAlarm/GarageKept.OutlookAlarm.Alarm/bin/Publish/`:

| Profile | Output | Requirement |
| --- | --- | --- |
| `FrameworkDependent` | `FrameworkDependent/GarageKept.OutlookAlarm.O365.exe` | Microsoft .NET 8 Desktop Runtime (x64) |
| `Portable` | `Portable/GarageKept.OutlookAlarm.O365.exe` | No separate .NET runtime installation |

Build output stays out of Git. Compiled executables can be attached to a GitHub Release.

## Origin and license

This fork is based on [GarageKept/OutlookAlarm](https://github.com/GarageKept/OutlookAlarm). The upstream README says the project is licensed under the MIT License, but the upstream repository does not include the referenced `LICENSE` file. This repository preserves that attribution and does not claim to supply missing license terms on behalf of the upstream author.

See [Code of Conduct](CodeOfConduct.md) for the upstream contribution guidelines.
