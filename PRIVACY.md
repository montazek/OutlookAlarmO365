# Privacy Policy

**OutlookAlarmO365** is a desktop application that connects to Microsoft 365 using Microsoft Graph in order to read the signed-in user's calendar and display local alarms/reminders.

## Data accessed

The application requests delegated Microsoft Graph permissions required to read the signed-in user's calendar, currently:

- `Calendars.Read`

The application may also receive basic account information needed by Microsoft authentication to identify the signed-in account.

## How data is used

Calendar data is used only to provide the application's alarm and reminder functionality.

OutlookAlarmO365 does not intentionally transmit calendar contents, credentials, access tokens, or personal data to the developer or to any developer-operated server.

Authentication is handled through Microsoft's identity platform. The application uses Microsoft-issued access tokens to communicate directly with Microsoft Graph.

## Local storage

The application may store local configuration and authentication cache data on the user's computer so that settings and sign-in state can be preserved between launches.

Users can remove local application data or sign out of the application to clear locally stored authentication information, subject to the behavior of the Microsoft authentication libraries used by the application.

## Third-party services

OutlookAlarmO365 relies on Microsoft 365, Microsoft Entra ID, and Microsoft Graph. Use of those services is governed by Microsoft's own terms and privacy policies.

## Data sharing

The developer does not sell, rent, or share user calendar data with third parties.

## Changes

This privacy policy may be updated if the application's functionality or data handling changes. The current version will be published in this repository.

## Contact

For questions or issues regarding privacy, please use the GitHub repository issue tracker:

https://github.com/montazek/OutlookAlarmO365/issues
