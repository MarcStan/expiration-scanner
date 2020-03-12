# Expiration scanner

Automation to check credential expiration and issue warnings before they do.

Azure functions that run on a schedule to check for:

- client secret expiration on app registrations
- secret/certificate expiration (based on keyvault metadata within the current subscription)

## KeyVault Expiration

Uses the managed identity of the function to access keyvaults (reads all keyvaults in the subscription by default). The function needs `list` access for both secrets & certificates in all keyvaults (does not grant access to the secret value but only the metadata).

To set up these permissions for an entire subscription run [Set-KeyVaultPermissions.ps1](./Set-KeyVaultPermissions.ps1) (must have permission to list/update all keyvaults in the subscription).

Additionally the function must be able to list the keyvaults themselves. To do so it must have RBAC `Reader` on the subscription level (or on every resourcegroup/keyvault).

### App configuration (environment variables):

- `KeyVault_Whitelist`: (optional), comma separated list of keyvaults to be included. Supports filtering with \*. E.g. "My-kv,Company-*,*-DEV" will return "My-kv", all keyvaults starting with "Company-" and all keyvaults ending with "-DEV". If blank will check all keyvaults.
- `KeyVault_Secret_WarningThresholdInDays`: (default: 60). The number of days before secret expiry after which warnings are issued.
- `KeyVault_Certificate_WarningThresholdInDays`: (default: 60). The number of days before certificate expiry after which warnings are issued.

See also [Shared configuration (environment variables)](#Shared-configuration-(environment-variables)).

## App registration secret/certificate expiration

The function MSI must be granted `Application.Read.All` permission on the graph API to read all existing applications. Alternatively you can setup a regular application and grant it permission.

If your company does not allow read all permission for application an alternative permission is `Application.ReadWrite.OwnedBy`. This permission requires you to add the service principal (function MSI) as an owner on every app registration/service principal it should read (unfortunately there is currently no `Application.Read.OwnedBy` permission, but rest assured that the function only performs readonly tasks).

TODO: MSI-> graph api setup script

### App configuration (environment variables):

- `AppRegistration_Whitelist`: (optional), comma separated list of applications to be included. Supports filtering with \*. E.g. "My-app,Company-*,*-DEV" will return "My-app", all apps starting with "Company-" and all apps ending with "-DEV". If blank will check all applications.
- `AppRegistration_Secret_WarningThresholdInDays`: (default: 60). The number of days before secret expiry after which warnings are issued.
- `AppRegistration_Certificate_WarningThresholdInDays`: (default: 60). The number of days before certificate expiry after which warnings are issued.

See also [Shared configuration (environment variables)](#Shared-configuration-(environment-variables)).

# Shared configuration (environment variables)

These configuration values are used by both functions:

- `SubscriptionId`: The subscription where the keyvaults are located (automatically set by ARM template when running in Azure. For local debugging it must be set manually in [local.settings.json](./ExpirationScanner/local.settings.json)).

## Notifications

One of these values **must** be set (multiple **can** be set). Each configured target will receive the same message:

- `Notification_SendGrid_Key`: If set (must be a key that has `mail send` permission) will cause the alerts to be sent as emails via SendGrid. Note that each function will agreggate all outputs into one message but aggregation does not happen across function (i.e. keyvault & app registration secret expiration cause two separate emails). Also requires `Notification_SendGrid_From` and `Notification_SendGrid_To` to be set to email addresses. Optionally `Notification_SendGrid_Subject` can also be set (defaults to `Expiry notification`).

- `Notification_Slack_WebHook`: If set will cause alerts to be sent to a slack channel. You must setup a [slack app](https://api.slack.com/messaging/webhooks).

Additionally the message is also logged via the logger output by default (console/app insights). To disable set `Notificaton_Logger_Disable` to `true`.