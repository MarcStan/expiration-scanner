# Expiration scanner

Automation to check credential expiration and issue warnings before they do.

Azure functions that run on a schedule to check for:

- client secret expiration on app registrations
- key/secret/certificate expiration (based on keyvault metadata within the current subscription)

## KeyVault Expiration

Uses the managed identity of the function to access keyvaults (reads all keyvaults in the subscription by default). The function needs `list` access for keys, secrets & certificates in all keyvaults (does not grant access to the secret value but only the metadata).

It is possible to exclude keyvaults as well.

To set up these permissions for an entire subscription run [Set-KeyVaultPermissions.ps1](./Set-KeyVaultPermissions.ps1) (must have permission to list/update all keyvaults in the subscription).

Additionally the function must be able to list the keyvaults themselves. To do so it must have RBAC `Reader` on the subscription level (or on every resourcegroup/keyvault).

### App configuration (environment variables):

- `KeyVault:Whitelist`: (optional, defaults to "\*"), comma separated list of keyvaults to be included. Supports filtering with \*. E.g. "My-kv,Company-*,*-DEV" will return "My-kv", all keyvaults starting with "Company-" and all keyvaults ending with "-DEV".
- `KeyVault:Key:WarningThresholdInDays`: (default: 60). The number of days before secret expiry after which warnings are issued.
- `KeyVault:Secret:WarningThresholdInDays`: (default: 60). The number of days before secret expiry after which warnings are issued.
- `KeyVault:Certificate:WarningThresholdInDays`: (default: 60). The number of days before certificate expiry after which warnings are issued.

See also [Shared configuration (environment variables)](#Shared-configuration-(environment-variables)).

## App registration secret/certificate expiration

The function MSI must be granted `Application.Read.All` permission on the Microsoft Graph API to read all existing applications. Alternatively you can setup a regular application and grant it the permission.

If your company does not allow read all permission for application an alternative permission is `Application.ReadWrite.OwnedBy`. This permission requires you to add the service principal (function MSI) as an owner on every app registration/service principal it should read (unfortunately there is currently no `Application.Read.OwnedBy` permission, but rest assured that the function only performs readonly tasks).

To setup the Graph API permission login with `Connect-AzureAD` (install powershell module AzureAD if missing) and then run:

> New-AzureADServiceAppRoleAssignment -Id 9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30 -ObjectId \<MSI object id> -PrincipalId \<MSI object Id> -ResourceId \<graph api object id>

* `9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30` is the id of the `Application.Read.All` graph API permission (same across all tenants)

To get the Graph API objectId run:

> Get-AzureADServicePrincipal -Filter "AppId eq '00000003-0000-0000-c000-000000000000'"

Run this command to list all Microsoft Graph API permissions:

> az ad sp show --id 00000003-0000-0000-c000-000000000000

(Note that all permissions appear twice: once as appRoles and once as oauth scopes. Since we want the app to run without user context you must pick the appRole id).

### App configuration (environment variables):

- `AppRegistration:Whitelist`: (optional), comma separated list of applications to be included. Supports filtering with \*. E.g. "My-app,Company-*,*-DEV" will return "My-app", all apps starting with "Company-" and all apps ending with "-DEV". If blank will check all applications.
- `AppRegistration:Secret:WarningThresholdInDays`: (default: 60). The number of days before secret expiry after which warnings are issued.
- `AppRegistration:Certificate:WarningThresholdInDays`: (default: 60). The number of days before certificate expiry after which warnings are issued.

See also [Shared configuration (environment variables)](#Shared-configuration-(environment-variables)).

# Shared configuration (environment variables)

These configuration values are used by both functions:

- `SubscriptionId`: The subscription where the keyvaults are located (automatically set by ARM template when running in Azure. For local debugging it must be set manually in [local.settings.json](./ExpirationScanner/local.settings.json)).

## Notifications

One of these targets  **must** be configured (multiple **can** be set). Each configured target will receive the same message:

### SendGrid

Deliver an email to one or multiple users. Requires SendGrid account to be set up and configured.

- `Notification:SendGrid:Key`: If set (must be a key that has `'mail send'` permission) will cause the alerts to be sent as emails via SendGrid. Note that each function will agreggate all outputs into one message but aggregation does not happen across function (i.e. keyvault & app registration secret expiration cause two separate emails). Will also send separate messages for the acutal warnings and access issues (e.g. no access to keyvault)

Also requires:

* `Notification:SendGrid:From`: Email from which to send
* `Notification:SendGrid:To`: One or multiple emails (separated by `,`) to which emails should be delivered
* `Notification:SendGrid:Subject` (optional, defaults to `Expiry notification`). If set will be used as email subject.

### Slack

Send messages into a slack channel. You must setup a [slack app](https://api.slack.com/messaging/webhooks).

- `Notification:Slack:WebHook`: If set will cause alerts to be sent to the attached slack channel.

### Logger

Additionally the message is logged via the logger output by default (console/app insights). To disable set `Notificaton:Logger:Disable` to `true`.

Tip: For settings stored in KeyVault replace `:` with `--`. For settings stored as environment variables, replace it with: `__`.