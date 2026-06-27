namespace Forex.Application.Features.Returns.Queries;

using AutoMapper;
using Forex.Application.Common.Extensions;
using Forex.Application.Common.Interfaces;
using Forex.Application.Common.Models;
using Forex.Application.Features.Returns.DTOs;
using MediatR;

public record ReturnFilterQuery : FilteringRequest, IRequest<IReadOnlyCollection<ReturnDto>>;

public class ReturnFilterQueryHandler(
    IAppDbContext context,
    IMapper mapper,
    IPagingMetadataWriter writer)
    : IRequestHandler<ReturnFilterQuery, IReadOnlyCollection<ReturnDto>>
{
    public async Task<IReadOnlyCollection<ReturnDto>> Handle(ReturnFilterQuery request, CancellationToken cancellationToken)
        => mapper.Map<IReadOnlyCollection<ReturnDto>>(await context.Returns
            .ToPagedListAsync(request, writer, cancellationToken));
}
