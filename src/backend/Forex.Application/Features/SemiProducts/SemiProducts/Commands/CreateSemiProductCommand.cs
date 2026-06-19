namespace Forex.Application.Features.SemiProducts.SemiProducts.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Extensions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities.SemiProducts;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record CreateSemiProductCommand(string Name, long UnitMeasureId) : IRequest<long>;

public sealed class CreateSemiProductCommandHandler(IAppDbContext context)
    : IRequestHandler<CreateSemiProductCommand, long>
{
    public async Task<long> Handle(CreateSemiProductCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppException("Yarim tayyor mahsulot nomi kiritilishi shart.");

        var normalizedName = request.Name.ToNormalized();

        var existing = await context.SemiProducts
            .FirstOrDefaultAsync(s => s.NormalizedName == normalizedName && !s.IsDeleted, ct);

        if (existing is not null)
            return existing.Id;

        var unitMeasure = await context.UnitMeasures
            .FirstOrDefaultAsync(u => u.Id == request.UnitMeasureId, ct)
            ?? throw new NotFoundException("UnitMeasure", nameof(request.UnitMeasureId), request.UnitMeasureId);

        var semiProduct = new SemiProduct
        {
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            UnitMeasureId = unitMeasure.Id,
            UnitMeasure = unitMeasure
        };

        context.SemiProducts.Add(semiProduct);
        await context.SaveAsync(ct);

        return semiProduct.Id;
    }
}
