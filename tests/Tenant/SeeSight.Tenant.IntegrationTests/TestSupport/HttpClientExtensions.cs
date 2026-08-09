using System.Net.Http.Json;
using SeeSight.SharedKernel.Http;

namespace SeeSight.Tenant.IntegrationTests.TestSupport;

/// <summary>
/// Simulates what the Gateway forwards after validating a JWT — the identity
/// headers set directly on each request, since these integration tests hit
/// Tenant.Api directly (no Gateway in the loop), the same technique
/// SeeSight.Identity.IntegrationTests already uses.
/// </summary>
internal static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> SendAsUserAsync(
        this HttpClient client, HttpMethod method, string url, Guid userId, string role, Guid? companyId, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(ForwardedIdentityHeaders.UserId, userId.ToString());
        request.Headers.Add(ForwardedIdentityHeaders.UserRole, role);
        if (companyId is not null)
        {
            request.Headers.Add(ForwardedIdentityHeaders.CompanyId, companyId.Value.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request).ConfigureAwait(false);
    }
}
