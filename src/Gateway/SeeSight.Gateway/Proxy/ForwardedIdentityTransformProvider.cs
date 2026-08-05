using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SeeSight.SharedKernel.Http;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace SeeSight.Gateway.Proxy;

/// <summary>
/// Stamps the Gateway-validated identity onto every proxied request as trusted
/// headers (docs/Authorization.md §2) — applied to every route. Always strips
/// any client-supplied values for these headers first, then re-adds trusted
/// values only if the request is authenticated: defense-in-depth against a
/// client attempting to spoof its own identity headers.
/// </summary>
public sealed class ForwardedIdentityTransformProvider : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context)
    {
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
    }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var request = transformContext.ProxyRequest;
            request.Headers.Remove(ForwardedIdentityHeaders.UserId);
            request.Headers.Remove(ForwardedIdentityHeaders.UserRole);
            request.Headers.Remove(ForwardedIdentityHeaders.CompanyId);

            var user = transformContext.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                if (user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { Length: > 0 } userId)
                {
                    request.Headers.Add(ForwardedIdentityHeaders.UserId, userId);
                }

                if (user.FindFirst(ClaimTypes.Role)?.Value is { Length: > 0 } role)
                {
                    request.Headers.Add(ForwardedIdentityHeaders.UserRole, role);
                }

                if (user.FindFirst("companyId")?.Value is { Length: > 0 } companyId)
                {
                    request.Headers.Add(ForwardedIdentityHeaders.CompanyId, companyId);
                }
            }

            return ValueTask.CompletedTask;
        });
    }
}
