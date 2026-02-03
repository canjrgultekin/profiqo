using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Integrations.Whatsapp;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Whatsapp.Worker;
using Profiqo.Whatsapp.Worker.Security;
using Profiqo.Whatsapp.Worker.Tenancy;

using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg.WriteTo.Console());
builder.Services.AddScoped<AmbientTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
builder.Services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<AmbientTenantContext>());

builder.Services.AddScoped<ISecretProtector, AesGcmSecretProtector>();

builder.Services.AddProfiqoPersistence(builder.Configuration);
builder.Services.AddWhatsappIntegration(builder.Configuration);

builder.Services.Configure<WhatsappSendWorkerOptions>(builder.Configuration.GetSection("Profiqo:WhatsappSendWorker"));
builder.Services.AddHostedService<WhatsappSendWorker>();

var host = builder.Build();
await host.RunAsync();