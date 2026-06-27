namespace Forex.Application.Features.Returns.Queries;

using AutoMapper;
using Forex.Application.Common.Interfaces;
using Forex.Application.Features.Returns.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

public record GetAllReturnsQuery : IRequest<IReadOnlyCollection<ReturnDto>>;

public class GetAllReturnsQueryHandler(
    IAppDbContext context,
    IMapper mapper) : IRequestHandler<GetAllReturnsQuery, IReadOnlyCollection<ReturnDto>>
{
    public async Task<IReadOnlyCollection<ReturnDto>> Handle(GetAllReturnsQuery request, CancellationToken cancellationToken)
       => mapper.Map<IReadOnlyCollection<ReturnDto>>(await context.Returns
        .Include(r => r.Customer)
        .Include(items => items.ReturnItems)
        .ThenInclude(pt => pt.ProductType)
        .ThenInclude(p => p.Product)
        .ThenInclude(m => m.UnitMeasure)
        .AsNoTracking()
        .ToListAsync(cancellationToken));
}
