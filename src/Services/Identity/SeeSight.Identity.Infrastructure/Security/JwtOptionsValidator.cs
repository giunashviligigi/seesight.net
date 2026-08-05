using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SeeSight.Identity.Infrastructure.Security;

/// <summary>
/// Fails startup (via ValidateOnStart) if the JWT signing key is missing outside
/// Development — closes the original system's "silent default signing secret"
/// gap, per docs/Authentication.md §5.
/// </summary>
public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKeyPem) && !environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKeyPem)} is required outside the Development environment.");
        }

        return ValidateOptionsResult.Success;
    }
}
