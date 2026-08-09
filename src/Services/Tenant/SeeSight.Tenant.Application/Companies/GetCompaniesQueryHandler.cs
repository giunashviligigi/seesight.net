using MediatR;
using Microsoft.EntityFrameworkCore;
using SeeSight.Shared.Common;
using SeeSight.Tenant.Application.Abstractions;

namespace SeeSight.Tenant.Application.Companies;

public sealed class GetCompaniesQueryHandler(ITenantDbContext dbContext)
    : IRequestHandler<GetCompaniesQuery, PagedResult<CompanyDto>>
{
    public async Task<PagedResult<CompanyDto>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // EF.Functions.Like (core, provider-agnostic) rather than the
            // Npgsql-specific ILike — Application must stay free of provider
            // packages (docs/ProjectReferenceDiagram.md §6). c.Name.ToLower()
            // is translated to a SQL lower() call, never executed in the CLR,
            // so CA1304/CA1311's "varies by current culture" warning is a false
            // positive here — and EF Core's SQL translator only recognizes the
            // parameterless ToLower() overload, so a CultureInfo overload isn't
            // an option.
            var search = request.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311
            query = query.Where(c => EF.Functions.Like(c.Name.ToLower(), $"%{search}%"));
#pragma warning restore CA1304, CA1311
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var entities = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(CompanyDto.FromDomain).ToList();
        return new PagedResult<CompanyDto>(items, total, page, pageSize);
    }
}
