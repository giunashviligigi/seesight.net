using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.Api.Authorization;
using SeeSight.Tenant.Api.Contracts.Employees;
using SeeSight.Tenant.Application.Employees;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Api.Controllers;

[ApiController]
[Route("employees")]
public sealed class EmployeesController(ISender sender, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateEmployeeResponse>> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        var command = new CreateEmployeeCommand(
            request.CompanyId, request.DepartmentId, request.Email, request.FirstName, request.LastName,
            request.JobTitle, request.Phone, request.Nationality, request.PassportNumber, request.PreferredAirport, request.CreateLogin);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        var response = CreateEmployeeResponse.FromResult(result);
        return CreatedAtAction(nameof(GetById), new { id = response.Employee.Id }, response);
    }

    [HttpGet]
    public async Task<ActionResult> GetAll(
        [FromQuery] Guid? companyId,
        [FromQuery] string? search,
        [FromQuery] Guid? departmentId,
        [FromQuery] EmployeeStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        var result = await sender.Send(new GetEmployeesQuery(companyId, search, departmentId, status, page, pageSize), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<EmployeeDto>> GetMine(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyEmployeeQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEmployeeByIdQuery(id), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        var command = new UpdateEmployeeCommand(
            id, request.FirstName, request.LastName, request.DepartmentId, request.JobTitle,
            request.Phone, request.Nationality, request.PassportNumber, request.PreferredAirport);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        await sender.Send(new DeactivateEmployeeCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        await sender.Send(new ActivateEmployeeCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        currentUser.RequireRole(SeeSightRoles.SuperAdmin, SeeSightRoles.CompanyAdmin);

        await sender.Send(new DeleteEmployeeCommand(id), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
