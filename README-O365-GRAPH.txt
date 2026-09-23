GarageKept Outlook Alarm - Microsoft 365 / Graph edition
========================================================

This edition reads the signed-in user's calendar directly through Microsoft Graph.
Classic Outlook and New Outlook do not need to be installed or running.

Microsoft 365 connection
------------------------
- On first launch, Settings opens directly on the Microsoft 365 tab.
- The included Client ID and Tenant ID are used by default.
- Change those values before clicking Connect only when using a different Entra app or tenant.
- The app requests the delegated Microsoft Graph Calendars.Read permission.
- The MSAL token cache is stored under LocalAppData/GarageKept.OutlookAlarm.O365.
- When Microsoft requires a new interactive sign-in, the Microsoft 365 Settings tab opens again.

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
