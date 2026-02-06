using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Application.Customers.IdentityResolution;
using Profiqo.Application.Integrations.Ikas;
using Profiqo.Infrastructure.Integrations.Ikas;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Worker.IntegrationJobs;
using Profiqo.Worker.Security;
using Profiqo.Worker.Tenancy;

using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg.WriteTo.Console());

// Tenant context (ambient)
builder.Services.AddScoped<AmbientTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
builder.Services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<AmbientTenantContext>());

// Secrets
builder.Services.AddScoped<ISecretProtector, AesGcmSecretProtector>();

// Infra + Ikas
builder.Services.AddProfiqoPersistence(builder.Configuration);
builder.Services.AddIkasIntegration(builder.Configuration);

// ✅ Application core services (without MediatR)
builder.Services.AddIdentityResolution();

// Sync processor (no MediatR)
builder.Services.AddScoped<IIkasSyncProcessor, IkasSyncProcessor>();

builder.Services.AddHostedService<IntegrationJobWorker>();
builder.Services.AddHostedService<Profiqo.Worker.SyncAutomation.SyncAutomationSchedulerWorker>();

var host = builder.Build();
await host.RunAsync();