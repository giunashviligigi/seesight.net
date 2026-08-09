using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeeSight.Identity.Api.Contracts.Internal;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.Api.Controllers;

/// <summary>
/// Internal-only — not Gateway-routed (see docs/APIContracts.md). Reachable
/// exclusively by other backend services over the private network, guarded by
/// <c>InternalServiceTokenMiddleware</c> (docs/adr/0006-internal-service-to-service-authentication.md).
/// Currently consumed by Tenant Service's employee login-provisioning flow
/// (docs/TenantArchitecture.md §6).
/// </summary>
[ApiController]
[Route("internal/users")]
public sealed class InternalUsersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProvisionEmployeeUserResponse>> Create(ProvisionEmployeeUserRequest request, CancellationToken cancellationToken)
    {
        var command = new ProvisionEmployeeUserCommand(request.Email, request.FirstName, request.LastName, request.CompanyId);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        var response = new ProvisionEmployeeUserResponse(result.UserId, result.TempPassword);
        return CreatedAtAction(nameof(Create), response);
    }

    /// <summary>
    /// Hard delete, reserved for the createLogin compensating-rollback path —
    /// see <see cref="DeleteUserCommand"/>.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteUserCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateUserCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new ActivateUserCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(Guid id, PatchInternalUserRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateInternalUserCommand(id, request.FirstName, request.LastName, request.ClearCompanyId, request.CompanyId);
        await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
