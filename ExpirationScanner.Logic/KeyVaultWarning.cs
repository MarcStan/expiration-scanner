using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.KeyVault.Fluent;
using System.Collections.Generic;

namespace ExpirationScanner.Logic
{
    public class KeyVaultWarning
    {
        public KeyVaultWarning(IVault vault)
        {
            Vault = vault;
        }

        public IEnumerable<SecretItem> ExpiringSecrets { get; set; }

        public IEnumerable<SecretItem> ExpiringLegacyCertificates { get; set; }

        public IEnumerable<CertificateItem> ExpiringCertificates { get; set; }

        public IVault Vault { get; }
    }
}