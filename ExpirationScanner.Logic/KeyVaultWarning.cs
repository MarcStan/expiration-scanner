using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.KeyVault.Fluent;

namespace ExpirationScanner.Logic
{
    public class KeyVaultWarning
    {
        public KeyVaultWarning(IVault vault)
        {
            Vault = vault;
        }

        public KeyItem[] ExpiringKeys { get; set; }

        public SecretItem[] ExpiringSecrets { get; set; }

        public SecretItem[] ExpiringLegacyCertificates { get; set; }

        public CertificateItem[] ExpiringCertificates { get; set; }

        public IVault Vault { get; }
    }
}