namespace Forex.WebApi.Controllers;

using Forex.Application.Features.Supplies.Commands;
using Forex.Application.Features.Supplies.Queries;
using Forex.WebApi.Controllers.Common;
using Forex.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

public sealed class SuppliesController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(new Response { Data = await Mediator.Send(new GetAllSuppliesQuery(), Ct) });

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplyCommand command)
        => Ok(new Response { Data = await Mediator.Send(command, Ct) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSupplyCommand command)
        => Ok(new Response { Data = await Mediator.Send(command with { Id = id }, Ct) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => Ok(new Response { Data = await Mediator.Send(new DeleteSupplyCommand(id), Ct) });
}
