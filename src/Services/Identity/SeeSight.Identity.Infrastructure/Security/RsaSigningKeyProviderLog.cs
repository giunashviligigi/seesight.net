using Microsoft.Extensions.Logging;

namespace SeeSight.Identity.Infrastructure.Security;

internal static partial class RsaSigningKeyProviderLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No JWT signing key configured — generating an ephemeral RSA key pair for Development. Tokens will not survive a restart of this process.")]
    public static partial void EphemeralKeyGenerated(ILogger logger);
}
