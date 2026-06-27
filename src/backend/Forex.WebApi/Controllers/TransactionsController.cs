namespace Forex.WebApi.Controllers;

using Forex.Application.Features.Transactions.Commands;
using Forex.Application.Features.Transactions.Queries;
using Forex.WebApi.Controllers.Common;
using Forex.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

public class TransactionsController : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateTransactionCommand command)
        => Ok(new Response { Data = await Mediator.Send(command, Ct) });

    [HttpPut]
    public async Task<IActionResult> Update(UpdateTransactionCommand command)
        => Ok(new Response { Data = await Mediator.Send(command, Ct) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => Ok(new Response { Data = await Mediator.Send(new DeleteTransactionCommand(id), Ct) });

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(new Response { Data = await Mediator.Send(new GetAllTransactionsQuery(), Ct) });

    [HttpPost("filter")]
    public async Task<IActionResult> GetFiltered(TransactionFilterQuery query)
        => Ok(new Response { Data = await Mediator.Send(query, Ct) });

    [HttpGet("unlinked")]
    public async Task<IActionResult> GetUnlinked([FromQuery] long userId, [FromQuery] DateTime date, [FromQuery] long? saleId = null)
        => Ok(new Response { Data = await Mediator.Send(new GetUnlinkedPaymentsQuery(userId, date, saleId), Ct) });

    [HttpPost("link-to-sale")]
    public async Task<IActionResult> LinkToSale(LinkPaymentsToSaleCommand command)
        => Ok(new Response { Data = await Mediator.Send(command, Ct) });
}
