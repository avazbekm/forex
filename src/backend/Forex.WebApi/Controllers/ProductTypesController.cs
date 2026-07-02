namespace Forex.WebApi.Controllers;

using Forex.Application.Features.Products.ProductTypes.Commands;
using Forex.Application.Features.Products.ProductTypes.Queries;
using Forex.WebApi.Controllers.Common;
using Forex.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

public class ProductTypesController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new Response { Data = await Mediator.Send(new GetAllProductTypesQuery(), Ct) });

    [HttpPost("filter")]
    public async Task<IActionResult> GetFiltered(ProductTypeFilterQuery query)
        => Ok(new Response { Data = await Mediator.Send(query, Ct) });

    [HttpGet("by-barcode/{code}")]
    public async Task<IActionResult> GetByBarcode(string code)
        => Ok(new Response { Data = await Mediator.Send(new GetProductTypeByBarcodeQuery(code), Ct) });

    [HttpPost("generate-barcodes")]
    public async Task<IActionResult> GenerateBarcodes()
        => Ok(new Response { Data = await Mediator.Send(new GenerateMissingBarcodesCommand(), Ct) });

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => Ok(new Response { Data = await Mediator.Send(new DeleteProductTypeCommand(id), Ct) });
}
