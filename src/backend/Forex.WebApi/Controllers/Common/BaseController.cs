namespace Forex.WebApi.Controllers.Common;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public abstract class BaseController : ControllerBase
{
    private IMediator? mediator;

    protected IMediator Mediator
        => mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    protected CancellationToken Ct
        => HttpContext.RequestAborted;
}
