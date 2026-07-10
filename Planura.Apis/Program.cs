using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Planura.Apis.Controller;
using Planura.Apis.MiddleWares;
using Planura.Core.Application.Extensions;
using Planura.Infrastructure.Authorization;
using Planura.Infrastructure.DependencyInjection;
using Planura.Infrastructure.Persistence;
using Planura.Infrastructure.Persistence.DependencyInjection;
using Planura.Infrastructure.Persistence.Extensions;
using Planura.Infrastructure.Persistence.Seed;
using Planura.Shared.Constants;
using Planura.Shared.Errors.Response;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices();
builder.Services.AddApplicationServices();


var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, JwtSecurityTokenHandler silently remaps short claim names ("sub", "email")
        // to long legacy URIs (e.g. ClaimTypes.NameIdentifier) in the resulting ClaimsPrincipal,
        // which breaks lookups keyed on JwtRegisteredClaimNames (see CurrentUserService).
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.ApprovedVendor, policy =>
        policy.Requirements.Add(new ApprovedVendorRequirement()));
});

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
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
}).AddApplicationPart(typeof(AssemblyInformation).Assembly);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSwaggerGen(options =>
{
    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste the access token only — no \"Bearer \" prefix needed.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    if (app.Environment.IsDevelopment())
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    await IdentityDataSeeder.SeedAsync(services);
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

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();
