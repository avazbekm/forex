namespace Forex.Application.Features.Sales.Mappers;

using AutoMapper;
using Forex.Application.Common.Extensions;
using Forex.Application.Features.Sales.Commands;
using Forex.Application.Features.Sales.DTOs;
using Forex.Domain.Entities.Sales;

public class SaleMappingProfile : Profile
{
    public SaleMappingProfile()
    {
        CreateMap<CreateSaleCommand, Sale>()
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Date.ToUtcSafe()));

        CreateMap<UpdateSaleCommand, Sale>()
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Date.ToUtcSafe()));

        CreateMap<Sale, SaleDto>()
            .ForMember(d => d.CurrencyCode, o => o.MapFrom(s => s.Currency != null ? s.Currency.Code : null));
        CreateMap<Sale, SaleForSaleItemDto>()
            .ForMember(d => d.CurrencyCode, o => o.MapFrom(s => s.Currency != null ? s.Currency.Code : null));
        CreateMap<Sale, SaleForUserDto>()
            .ForMember(d => d.CurrencyCode, o => o.MapFrom(s => s.Currency != null ? s.Currency.Code : null));
    }
}
