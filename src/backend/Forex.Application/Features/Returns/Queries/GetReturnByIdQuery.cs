namespace Forex.Application.Features.Returns.Queries;

using AutoMapper;
using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Interfaces;
using Forex.Application.Features.Returns.DTOs;
using Forex.Domain.Entities.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

public record GetReturnByIdQuery(long Id) : IRequest<ReturnDto>;

public class GetReturnByIdQueryHandler(
    IAppDbContext context,
    IMapper mapper) : IRequestHandler<GetReturnByIdQuery, ReturnDto>
{
    public async Task<ReturnDto> Handle(GetReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var @return = await context.Returns
            .Include(r => r.ReturnItems)
                .ThenInclude(i => i.ProductType)
            .Include(r => r.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Return), nameof(request.Id), request.Id);

        return mapper.Map<ReturnDto>(@return);
    }
}
