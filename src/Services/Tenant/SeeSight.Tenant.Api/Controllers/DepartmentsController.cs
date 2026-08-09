using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.Api.Authorization;
using SeeSight.Tenant.Api.Contracts.Departments;
using SeeSight.Tenant.Application.Departments;

namespace SeeSight.Tenant.Api.Controllers;

[ApiController]
[Route("departments")]
public sealed class DepartmentsController(ISender sender, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        var command = new CreateDepartmentCommand(request.CompanyId, request.Name, request.Code);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetAll), null, result);
    }

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] Guid? companyId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDepartmentsQuery(companyId), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        var result = await sender.Send(new UpdateDepartmentCommand(id, request.Name, request.Code), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        await sender.Send(new DeleteDepartmentCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
