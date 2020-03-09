using Microsoft.Graph;
using System.Collections.Generic;

namespace ExpirationScanner.Logic
{
    public class ServicePrincipalWarning
    {
        public IEnumerable<PasswordCredential> ExpiringSecrets { get; set; }
        public IEnumerable<KeyCredential> ExpiringCertificates { get; set; }
    }
}