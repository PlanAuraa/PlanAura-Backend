using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Planura.Apis.Controller;
using Planura.Apis.MiddleWares;
using Planura.Core.Application.Extensions;
using Planura.Core.Application.Services;
using Planura.Infrastructure.Extensions;
using Planura.Infrastructure.Persistence.Extensions;
using Planura.Infrastructure.Persistence.Seed;
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

builder.Services.AddCors(corsOptions =>
{
    corsOptions.AddPolicy("TalabatPolicy", policyBuilder =>
    {
        policyBuilder.AllowAnyHeader()
                     .AllowAnyMethod()
                     .AllowAnyOrigin(); // Allow all domains
    });
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter a JWT access token (no \"Bearer \" prefix needed).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await IdentityDataSeeder.SeedAsync(scope.ServiceProvider);
}

using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    recurringJobs.AddOrUpdate<IBookingHoldExpiryJob>(
        "booking-hold-expiry",
        job => job.RunAsync(),
        "*/15 * * * *");

    recurringJobs.AddOrUpdate<IPaymentDeadlineJob>(
        "payment-deadline",
        job => job.RunAsync(),
        "*/15 * * * *");

    recurringJobs.AddOrUpdate<IPaymentReminderJob>(
        "payment-reminder",
        job => job.RunAsync(),
        "*/15 * * * *");
}


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
app.UseCors("TalabatPolicy");


app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();
