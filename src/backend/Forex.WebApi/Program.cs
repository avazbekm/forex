using Forex.Application.Common.Extensions;
using Forex.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Service registrations
builder.Services.AddDependencies(builder.Configuration);

BarcodeGenerator.Prefix = builder.Configuration.GetValue<string>("Barcode:Prefix") ?? "FRX";

var app = builder.Build();

// Middleware pipeline
app.UseInfrastructure(); // HTTPS, CORS, Auth
app.UseOpenApiDocumentation(); // Scalar UI

await app.UseSeedData();
app.MapControllers();

app.Run();
