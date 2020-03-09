using Microsoft.Graph;

namespace ExpirationScanner.Logic
{
    public class ServicePrincipalWarning
    {
        public PasswordCredential[] ExpiringSecrets { get; set; }

        public KeyCredential[] ExpiringCertificates { get; set; }
    }
}