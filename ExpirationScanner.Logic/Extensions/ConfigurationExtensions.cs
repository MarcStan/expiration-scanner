using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace ExpirationScanner.Logic.Extensions
{
    public static class ConfigurationExtensions
    {
        public static T GetRequiredValue<T>(this IConfiguration configuration, string variable)
            => configuration.GetValue<T>(variable) ?? throw new KeyNotFoundException($"Missing configuration, no variable found for key '{variable}'");
    }
}
