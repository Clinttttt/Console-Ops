using ConsoleOps.Api.Features.Dashboard;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Api.Middleware;
using ConsoleOps.Application;
using ConsoleOps.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapDashboardEndpoints();
app.MapProjectEndpoints();

app.Run();

public partial class Program;
