using System.Text;

using FluentValidation.AspNetCore;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using Profiqo.Api.Health;
using Profiqo.Api.Middleware;
using Profiqo.Api.Options;
using Profiqo.Api.RateLimiting;
using Profiqo.Api.Security;
using Profiqo.Api.Tenancy;
using Profiqo.Application;
using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Security;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Integrations.Ikas;
using Profiqo.Infrastructure.Persistence;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration);
    lc.Enrich.FromLogContext();
    lc.WriteTo.Console();
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

builder.Services.AddFluentValidationAutoValidation();

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Profiqo:Auth"));
builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection("Profiqo:Tenancy"));
builder.Services.Configure<ObservabilityOptions>(builder.Configuration.GetSection("Profiqo:Observability"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("Profiqo:RateLimit"));

var authOpts = builder.Configuration.GetSection("Profiqo:Auth").Get<AuthOptions>() ?? new AuthOptions();
if (string.IsNullOrWhiteSpace(authOpts.JwtSigningKey) || authOpts.JwtSigningKey.Length < 32)
    throw new InvalidOperationException("Profiqo:Auth:JwtSigningKey must be set (min 32 chars).");

builder.Services.AddScoped<ITenantContext>(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>();
    var opt = builder.Configuration.GetSection("Profiqo:Tenancy").Get<TenancyOptions>() ?? new TenancyOptions();
    return new HttpTenantContext(http, opt);
});

builder.Services.AddScoped<CurrentUserContext>();

builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ISecretProtector, AesGcmSecretProtector>();

builder.Services.AddScoped<ExceptionHandlingMiddleware>();
builder.Services.AddScoped<CorrelationIdMiddleware>();

builder.Services.AddProfiqoApplication();
builder.Services.AddProfiqoPersistence(builder.Configuration);
builder.Services.AddIkasIntegration(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Profiqo API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Authorization header. Example: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // .NET 10 / Microsoft.OpenApi 2.x - Delegate pattern
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOpts.Issuer,
            ValidateAudience = true,
            ValidAudience = authOpts.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOpts.JwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var rl = builder.Configuration.GetSection("Profiqo:RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();

builder.Services.AddRateLimiter(o =>
{
    var opts = RateLimitPolicies.Create(rl.GlobalRps);
    o.GlobalLimiter = opts.GlobalLimiter;
    o.OnRejected = (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        return ValueTask.CompletedTask;
    };
});

var obs = builder.Configuration.GetSection("Profiqo:Observability").Get<ObservabilityOptions>() ?? new ObservabilityOptions();

builder.Services.AddOpenTelemetry()
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
        t.AddHttpClientInstrumentation();
        t.AddOtlpExporter(o => o.Endpoint = new Uri(obs.OtlpEndpoint));
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation();
        m.AddHttpClientInstrumentation();
        m.AddOtlpExporter(o => o.Endpoint = new Uri(obs.OtlpEndpoint));
    });

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
HealthChecks.Map(app);

app.Run();
