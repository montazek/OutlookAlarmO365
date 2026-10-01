# OutlookAlarmO365

**OutlookAlarmO365** is a Windows desktop alarm application for **Microsoft 365 / Office 365 calendars**.

It connects directly to Microsoft 365 using the **Microsoft Graph API** and creates visible desktop alarms for upcoming calendar events.

**Microsoft Outlook does not need to be installed or running.**

## Features

* Reads calendar events directly from Microsoft 365
* Uses Microsoft Graph
* Works independently of Classic Outlook and New Outlook
* Desktop alarm notifications for upcoming meetings and events
* Microsoft 365 sign-in through Microsoft identity platform
* Supports Microsoft Entra ID multi-tenant organizations
* No Microsoft 365 password is stored by the application
* Windows desktop application

## Download

Download the latest version from the **Releases** section:

https://github.com/montazek/OutlookAlarmO365/releases/latest

Two builds may be available:

* **Self-contained** – recommended for most users; no separate .NET installation required
* **Framework-dependent** – smaller download, requires the appropriate .NET runtime

## Microsoft 365 sign-in

OutlookAlarmO365 uses Microsoft authentication and requests delegated access to the signed-in user's calendar.

The application requests:

* `Calendars.Read`

This permission allows OutlookAlarmO365 to read calendar events belonging to the signed-in user so that it can generate alarms.

The application does **not** use application-level access to read calendars without a signed-in user.

### Administrator approval

Whether administrator approval is required depends on the Microsoft Entra policies configured by your organization.

In organizations that allow user consent for the requested permissions, users may be able to sign in and start using OutlookAlarmO365 directly.

Organizations with more restrictive consent policies may require a Microsoft 365 / Entra administrator to approve the application before it can access calendar data.

## Privacy

Calendar data is retrieved directly from Microsoft 365 through Microsoft Graph and is used by OutlookAlarmO365 to provide its alarm functionality.

See the full privacy statement:

[PRIVACY.md](PRIVACY.md)

## Terms of use

See:

[TERMS.md](TERMS.md)

## Origin

OutlookAlarmO365 is derived from the original **GarageKept/OutlookAlarm** project:

https://github.com/GarageKept/OutlookAlarm

The original project states in its README that it is licensed under the **MIT License**.

OutlookAlarmO365 has subsequently been modified to work with Microsoft 365 / Office 365 and Microsoft Graph and is maintained independently from the original project.

Original authorship and Git commit history are preserved.

This project is **not affiliated with or endorsed by Microsoft**.

## Building from source

Clone the repository:

```text
git clone https://github.com/montazek/OutlookAlarmO365.git
```

Open the solution in Visual Studio and build the application using the appropriate .NET workload.

## Contributing

Bug reports, improvements and pull requests are welcome.

If you encounter a problem, please open an Issue and include enough information to reproduce it.

## Disclaimer

This software is provided without warranty. Use it at your own risk.

Microsoft, Microsoft 365, Office 365, Outlook, Microsoft Graph and Microsoft Entra are trademarks of Microsoft Corporation.
