namespace Forex.Application.Features.Returns.ReturnItems.Mappers;

using AutoMapper;
using Forex.Application.Features.Returns.ReturnItems.Commands;
using Forex.Application.Features.Returns.ReturnItems.DTOs;
using Forex.Domain.Entities.Sales;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ReturnItemCommand, ReturnItem>();

        CreateMap<ReturnItem, ReturnItemForReturnDto>();
    }
}
