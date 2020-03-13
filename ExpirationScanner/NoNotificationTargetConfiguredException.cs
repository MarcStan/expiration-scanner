using System;

namespace ExpirationScanner
{
    [Serializable]
    public class NoNotificationTargetConfiguredException : Exception
    {
        public NoNotificationTargetConfiguredException() : base()
        {
        }

        public NoNotificationTargetConfiguredException(string message) : base(message)
        {
        }

        public NoNotificationTargetConfiguredException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
