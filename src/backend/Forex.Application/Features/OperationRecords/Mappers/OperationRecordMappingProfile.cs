namespace Forex.Application.Features.OperationRecords.Mappers;

using AutoMapper;
using Forex.Application.Features.OperationRecords.DTOs;
using Forex.Application.Features.Sales.DTOs;
using Forex.Application.Features.Transactions.DTOs;
using Forex.Domain.Entities;
using Forex.Domain.Entities.Sales;

public class OperationRecordMappingProfile : Profile
{
    public OperationRecordMappingProfile()
    {
        CreateMap<OperationRecord, OperationRecordDto>()
            .ForMember(d => d.Description, o => o.MapFrom(s => s.Description ?? ""))
            .ForMember(d => d.CurrencyCode, o => o.MapFrom(s => s.Currency != null ? s.Currency.Code : null))
            .ForMember(d => d.SettlementAmount, o => o.MapFrom(s => s.Amount * s.Rate));

        CreateMap<Sale, SaleForOperationDto>();

        CreateMap<Transaction, TransactionForOperationDto>();
        CreateMap<Supply, SupplyForOperationDto>();
    }
}
