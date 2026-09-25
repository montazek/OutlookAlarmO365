GarageKept Outlook Alarm - Microsoft 365 / Graph edition
========================================================

This edition reads the signed-in user's calendar directly through Microsoft Graph.
Classic Outlook and New Outlook do not need to be installed or running.

Microsoft 365 connection
------------------------
- On first launch, Settings opens directly on the Microsoft 365 tab.
- Click Connect and sign in with a work or school Microsoft 365 account.
- The included multitenant Client ID is used automatically. No Tenant ID or
  personal app registration is needed for normal use.
- A tenant administrator may still have to approve the app under that
  organization's consent policy. Contact IT if sign-in asks for approval.
- An optional Advanced setting accepts your own app registration and tenant.
- The app requests the delegated Microsoft Graph Calendars.Read permission.
- The MSAL token cache is stored under LocalAppData/GarageKept.OutlookAlarm.O365.
- The app remembers the connected account for later starts. Account switching
  and disconnect are not supported in this version.
- When Microsoft requires a new interactive sign-in, the Microsoft 365 Settings tab opens again.

Publisher registration
----------------------
The included Client ID is 3a1efa33-d7f2-414b-99f4-d40a0d878489. Its Entra
registration must support accounts in any organizational directory (multiple
Entra tenants), have the Mobile and desktop applications redirect URI
http://localhost, and request Microsoft Graph delegated Calendars.Read.
The code uses the organizations authority; personal Outlook.com accounts are
not supported. Test sign-in with a user in another organization before release.

Deployment
----------
Two Windows x64 single-file builds are available under GarageKept.OutlookAlarm.Alarm/bin/Publish:

1. FrameworkDependent/GarageKept.OutlookAlarm.O365.exe
   Small build. Requires the Microsoft .NET 8 Desktop Runtime (x64).

2. Portable/GarageKept.OutlookAlarm.O365.exe
   Larger self-contained build. No separately installed .NET runtime is required.

No appsettings.json file is required. Normal user settings are stored automatically in:
AppData/Roaming/GarageKept/OutlookAlarmO365/settings.json

Building
--------
Open GarageKept.OutlookAlarm.O365.sln in Visual Studio 2022 or use:

dotnet publish GarageKept.OutlookAlarm.Alarm/GarageKept.OutlookAlarm.Alarm.csproj -p:PublishProfile=FrameworkDependent
dotnet publish GarageKept.OutlookAlarm.Alarm/GarageKept.OutlookAlarm.Alarm.csproj -p:PublishProfile=Portable
