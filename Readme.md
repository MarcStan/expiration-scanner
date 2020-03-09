# Expiration scanner

Automation to check credential expiration and issue warnings before they do.

Azure functions that run on a schedule to check for:

- client secret expiration on app registrations
- secret/certificate expiration (based on keyvault metadata within the current subscription)

## KeyVault Expiration

Uses the managed identity of the function to access keyvaults (reads all keyvaults in the subscription by default). The function needs `list` access for both secrets & certificates in all keyvaults (does not grant access to the secret value but only the metadata).

### Configuration (environment variables):

- `SubscriptionId`: (automatically set by ARM template when running in Azure) The subscription where the keyvaults  are located

See also [Shared configuration (environment variables)](#Shared-configuration-(environment-variables)).

## App registration secret expiration

The function MSI must be granted `Application.Read.All` permission on the graph API to read all existing applications. Alternatively you can setup a regular application and grant it permission.

TODO: MSI-> graph api setup script

### Configuration (environment variables):

- `ApplicationFilter`: (optional), comma separated list of applications to be included. Supports filtering with \*. E.g. "My-app,Company-*,*-DEV" will return "My-app", all apps starting with "Company-" and all apps ending with "-DEV". If blank will check all applications.

# Shared configuration (environment variables)

These configuration values are used by both functions:

## Notifications

One of these **must** be set (multiple **can** be set). Each configured target will receive the same message:

- `Notification_Sendgrid_Key`: If set (must be a key that has `mail send` permission) will cause the alerts to be sent as emails. Note that each function will agreggate all outputs into one message but aggregation does not happen across function (i.e. keyvault & app registration secret expiration cause two separate emails)

- `Notification_Slack_Webhook`: If set will cause alerts to be sent to a slack channel. You must setup a [slack app](https://api.slack.com/messaging/webhooks).