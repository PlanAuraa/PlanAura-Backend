using Microsoft.AspNetCore.Mvc;
using Planura.Apis.Controller;
using Planura.Apis.MiddleWares;
using Planura.Infrastructure.Persistence.Extensions;
using Planura.Shared.Errors.Response;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().ConfigureApiBehaviorOptions((option) =>
{
    option.SuppressModelStateInvalidFilter = false;
    option.InvalidModelStateResponseFactory = (action) =>
    {
        var errors = action.ModelState.
        Where(p => p.Value!.Errors.Count > 0)
        .SelectMany(e => e.Value!.Errors).Select(e => e.ErrorMessage);

        return new BadRequestObjectResult(new ApiValidationErrorResponse() { Erroes = errors });
    };
}).AddApplicationPart(typeof(AssemblyInformation).Assembly);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();


app.UseMiddleware<ExeptionHandlerMiddleware>();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStatusCodePagesWithReExecute("/Errors/{0}");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();
