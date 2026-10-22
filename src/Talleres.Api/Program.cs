using Talleres.Api.Features.Workshops;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.Configure<RouteHandlerOptions>(options =>
{
    options.ThrowOnBadRequest = false;
});
builder.Services.AddSingleton<ReservationStore>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapWorkshopEndpoints();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.Run();

public partial class Program;
