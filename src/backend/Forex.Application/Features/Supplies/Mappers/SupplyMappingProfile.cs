namespace Forex.Application.Features.Supplies.Mappers;

using AutoMapper;
using Forex.Application.Features.Supplies.DTOs;
using Forex.Domain.Entities;

public sealed class SupplyMappingProfile : Profile
{
    public SupplyMappingProfile()
    {
        CreateMap<Supply, SupplyDto>()
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.Date, DateTimeKind.Utc)));
    }
}
