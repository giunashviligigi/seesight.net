using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeeSight.Tenant.Api.Contracts.Internal;
using SeeSight.Tenant.Application.Employees;

namespace SeeSight.Tenant.Api.Controllers;

/// <summary>
/// Internal-only — not Gateway-routed (see docs/APIContracts.md). Reachable
/// exclusively by other backend services over the private network, guarded by
/// <c>InternalServiceTokenMiddleware</c> (docs/adr/0006-internal-service-to-service-authentication.md).
/// Consumed by Trip Service starting M5 (docs/ImplementationRoadmap.md).
/// </summary>
[ApiController]
[Route("internal/employees")]
public sealed class InternalEmployeesController(ISender sender) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<ActionResult<ValidateEmployeesResponse>> Validate(ValidateEmployeesRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ValidateEmployeesQuery(request.CompanyId, request.EmployeeIds), cancellationToken).ConfigureAwait(false);
        return Ok(new ValidateEmployeesResponse(result.ValidEmployeeIds, result.InvalidEmployeeIds));
    }
}
