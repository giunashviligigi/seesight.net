using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SeeSight.SharedKernel.InternalAuth;

/// <summary>
/// Fails startup (via ValidateOnStart) if the internal-service token is
/// missing outside Development — same convention as Identity Service's JWT
/// signing key check, per docs/adr/0006-internal-service-to-service-authentication.md.
/// </summary>
public sealed class InternalServiceTokenOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<InternalServiceTokenOptions>
{
    public ValidateOptionsResult Validate(string? name, InternalServiceTokenOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceToken) && !environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail(
                $"{InternalServiceTokenOptions.SectionName}:{nameof(InternalServiceTokenOptions.ServiceToken)} is required outside the Development environment.");
        }

        return ValidateOptionsResult.Success;
    }
}
