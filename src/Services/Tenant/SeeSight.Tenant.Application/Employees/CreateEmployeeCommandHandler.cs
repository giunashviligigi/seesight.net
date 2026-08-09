using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.SharedKernel.Tenancy;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Common;
using SeeSight.Tenant.Application.Exceptions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Employees;

/// <summary>
/// Implements the two-service write from docs/TenantArchitecture.md §6:
/// Identity Service's <c>User</c> is created first (when <c>createLogin</c> is
/// set), then the local <c>Employee</c> row references it. If the local save
/// fails after the remote user was already created, the just-created user is
/// deleted (compensating action) before the error propagates — never leaving
/// an orphaned Identity Service account.
/// </summary>
public sealed class CreateEmployeeCommandHandler(
    ITenantDbContext dbContext,
    ITenantContext tenantContext,
    ITenantResolver tenantResolver,
    IIdentityServiceClient identityServiceClient,
    TimeProvider timeProvider) : IRequestHandler<CreateEmployeeCommand, CreateEmployeeResult>
{
    public async Task<CreateEmployeeResult> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var companyId = tenantResolver.Resolve(tenantContext, request.CompanyId);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(e => e.CompanyId == companyId && e.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (emailTaken)
        {
            throw new DuplicateEmployeeEmailException();
        }

        var now = timeProvider.GetUtcNow();
        Guid? userId = null;
        string? tempPassword = null;

        if (request.CreateLogin)
        {
            var provisioned = await identityServiceClient
                .ProvisionEmployeeUserAsync(normalizedEmail, request.FirstName, request.LastName, companyId, cancellationToken)
                .ConfigureAwait(false);
            userId = provisioned.UserId;
            tempPassword = provisioned.TempPassword;
        }

        var employee = Employee.Create(
            companyId, request.DepartmentId, userId, normalizedEmail, request.FirstName, request.LastName,
            request.JobTitle, request.Phone, request.Nationality, request.PassportNumber, request.PreferredAirport, now);
        dbContext.Employees.Add(employee);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            if (userId is not null)
            {
                await identityServiceClient.DeleteUserAsync(userId.Value, cancellationToken).ConfigureAwait(false);
            }

            throw new DuplicateEmployeeEmailException();
        }
        catch (Exception) when (userId is not null)
        {
            await identityServiceClient.DeleteUserAsync(userId.Value, cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new CreateEmployeeResult(EmployeeDto.FromDomain(employee), tempPassword);
    }
}
