Outlook alarm / Microsoft 365 / Office 365 / Microsoft Graph / Reminder / Meeting
# OutlookAlarm O365 (Microsoft Graph)

A Windows desktop alarm application based on [GarageKept/OutlookAlarm](https://github.com/GarageKept/OutlookAlarm). This fork reads the signed-in user's Microsoft 365 calendar through Microsoft Graph. Classic Outlook and New Outlook do not need to be installed or running.

## Features

- Alarms based on upcoming calendar events
- Customizable sounds and interface colors
- Interactive Microsoft 365 sign-in

## Microsoft 365 connection

On first launch, Settings opens on the Microsoft 365 tab. The included Client ID and Tenant ID are used by default. Change them before selecting **Connect** if you use a different Microsoft Entra application or tenant. The app requests the delegated Microsoft Graph `Calendars.Read` permission; a user must sign in and may need tenant administrator consent.

When Microsoft requires another interactive sign-in, the Microsoft 365 Settings tab opens again. No `appsettings.json` file is required. User settings are stored in `%APPDATA%\GarageKept\OutlookAlarmO365\settings.json`, and the MSAL token cache is stored under `%LOCALAPPDATA%\GarageKept.OutlookAlarm.O365`.

## Register your own Microsoft Entra application

The included IDs target the preconfigured tenant. To use another tenant or your own app registration:

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com/) in the tenant whose calendars you want to use. Open **Entra ID > App registrations > New registration**. For an organization-only setup, choose **Accounts in this organizational directory only**, then register the app.
2. On the app's **Overview** page, copy **Application (client) ID** and **Directory (tenant) ID**.
3. Open **Authentication > Add a platform > Mobile and desktop applications**. Add the redirect URI `http://localhost` for the system-browser sign-in used by this app. Under **Advanced settings**, set **Allow public client flows** to **Yes** and save.
4. Open **API permissions > Add a permission > Microsoft Graph > Delegated permissions**. Find **Calendars.Read**, select it, and choose **Add permissions**. Select **Delegated permissions**, not **Application permissions**: the app reads the signed-in user's calendar.
5. If your organization allows user consent, the user can approve this permission during sign-in. If its policy blocks user consent, an administrator must use **Grant admin consent for [tenant]** on the **API permissions** page.
6. In OutlookAlarm, open **Settings > Microsoft 365**, enter the client and tenant IDs from step 2, select **Connect**, and complete the sign-in.

Do not create or enter a client secret for this desktop app. See Microsoft's [desktop app configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration), [Graph permission setup](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-configure-app-access-web-apis), and [Calendars.Read definition](https://learn.microsoft.com/en-us/graph/permissions-reference#calendarsread).

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
