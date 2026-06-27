namespace Forex.Application.Features.Returns.Mappers;

using AutoMapper;
using Forex.Application.Common.Extensions;
using Forex.Application.Features.Returns.Commands;
using Forex.Application.Features.Returns.DTOs;
using Forex.Domain.Entities.Sales;

public class ReturnMappingProfile : Profile
{
    public ReturnMappingProfile()
    {
        CreateMap<CreateReturnCommand, Return>()
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Date.ToUtcSafe()));

        CreateMap<UpdateReturnCommand, Return>()
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Date.ToUtcSafe()));

        CreateMap<Return, ReturnDto>()
            .ForMember(d => d.CurrencyCode, o => o.MapFrom(s => s.Currency != null ? s.Currency.Code : null));
    }
}
