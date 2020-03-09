namespace ExpirationScanner
{
    public static class Constants
    {
        public const int DefaultExpiry = 60;

        public const string KeyVaultExpirySchedule = "0 0 8 * * *";

        public const string AppRegistrationCredentialExpirySchedule = "0 0 8 * * *";
    }
}
