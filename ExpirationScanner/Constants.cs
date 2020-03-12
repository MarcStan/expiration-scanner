namespace ExpirationScanner
{
    public static class Constants
    {
        /// <summary>
        /// Days before actual expiration from which on notifications will be issued.
        /// </summary>
        public const int ExpiryWarningThresholdInDays = 60;

        public const string KeyVaultExpirySchedule = "0 0 8 * * *";

        public const string AppRegistrationCredentialExpirySchedule = "0 0 8 * * *";
    }
}
