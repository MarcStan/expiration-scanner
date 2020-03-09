using Microsoft.Graph;
using System.Collections.Generic;

namespace ExpirationScanner.Endpoints.ServicePrincipalCheck
{
    public class ServicePrincipalWarning
    {
        private readonly Application app;

        public ServicePrincipalWarning(Application app)
        {
            this.app = app;
        }
        public IEnumerable<PasswordCredential> ExpiringSecrets { get; set; }
        public IEnumerable<KeyCredential> ExpiringCertificates { get; set; }
    }
}