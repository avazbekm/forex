namespace Forex.Application.Features.Supplies.Queries;

using AutoMapper;
using Forex.Application.Common.Interfaces;
using Forex.Application.Features.Supplies.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetAllSuppliesQuery : IRequest<IReadOnlyCollection<SupplyDto>>;

public sealed class GetAllSuppliesQueryHandler(IAppDbContext context, IMapper mapper)
    : IRequestHandler<GetAllSuppliesQuery, IReadOnlyCollection<SupplyDto>>
{
    public async Task<IReadOnlyCollection<SupplyDto>> Handle(GetAllSuppliesQuery request, CancellationToken ct)
    {
        var supplies = await context.Supplies
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Include(s => s.User)
            .Include(s => s.Currency)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Id)
            .ToListAsync(ct);

        return mapper.Map<IReadOnlyCollection<SupplyDto>>(supplies);
    }
}
