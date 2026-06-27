namespace Forex.WebApi.Controllers;

using Forex.Application.Features.Returns.Commands;
using Forex.Application.Features.Returns.Queries;
using Forex.WebApi.Controllers.Common;
using Forex.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

public class ReturnsController : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Entry(CreateReturnCommand command)
        => Ok(new Response { Data = await Mediator.Send(command, Ct) });

    [HttpPut]
    public async Task<IActionResult> Update(UpdateReturnCommand command)
        => Ok(new Response { Data = await Mediator.Send(command, Ct) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => Ok(new Response { Data = await Mediator.Send(new DeleteReturnCommand(id), Ct) });

    [HttpPost("filter")]
    public async Task<IActionResult> GetFiltered(ReturnFilterQuery query)
        => Ok(new Response { Data = await Mediator.Send(query, Ct) });

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(new Response { Data = await Mediator.Send(new GetAllReturnsQuery(), Ct) });
}
