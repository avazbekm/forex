namespace Forex.WebApi.Controllers;

using Forex.Application.Features.Sales.Commands;
using Forex.Application.Features.Sales.Queries;
using Forex.WebApi.Controllers.Common;
using Forex.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

public class SalesController : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Entry(CreateSaleCommand command)
        => Ok(new Response { Data = await Mediator.Send(command, Ct) });

    [HttpPut]
    public async Task<IActionResult> Update(UpdateSaleCommand command)
        => Ok(new Response { Data = await Mediator.Send(command, Ct) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => Ok(new Response { Data = await Mediator.Send(new DeleteSaleCommand(id), Ct) });

    [HttpPost("filter")]
    public async Task<IActionResult> GetFiltered(SaleFilterQuery query)
        => Ok(new Response { Data = await Mediator.Send(query, Ct) });

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(new Response { Data = await Mediator.Send(new GetAllSalesQuery(), Ct) });

    [HttpGet("{id:long}/document-summary")]
    public async Task<IActionResult> GetDocumentSummary(long id)
        => Ok(new Response { Data = await Mediator.Send(new GetSaleDocumentSummaryQuery(id), Ct) });
}
