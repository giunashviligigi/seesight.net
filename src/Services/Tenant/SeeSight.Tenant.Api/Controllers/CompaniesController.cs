using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.Api.Authorization;
using SeeSight.Tenant.Api.Contracts.Companies;
using SeeSight.Tenant.Application.Companies;

namespace SeeSight.Tenant.Api.Controllers;

[ApiController]
[Route("companies")]
public sealed class CompaniesController(ISender sender, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CompanyDto>> Create(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        var command = new CreateCompanyCommand(request.Name, request.LegalName, request.Country, request.BillingEmail, request.Timezone, request.PolicyJson);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin);

        var result = await sender.Send(new GetCompaniesQuery(search, page, pageSize), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<CompanyDto>> GetMine(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyCompanyQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCompanyByIdQuery(id), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> Update(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        var command = new UpdateCompanyCommand(id, request.Name, request.LegalName, request.Country, request.BillingEmail, request.Timezone, request.PolicyJson);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin);

        await sender.Send(new DeactivateCompanyCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin);

        await sender.Send(new ActivateCompanyCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin);

        await sender.Send(new DeleteCompanyCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/assign-admin")]
    public async Task<IActionResult> AssignAdmin(Guid id, AssignCompanyAdminRequest request, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin);

        await sender.Send(new AssignCompanyAdminCommand(id, request.UserId, request.ReplaceExisting), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
