using Profiqo.Application.Abstractions.Crypto;
using Profiqo.Application.Abstractions.Tenancy;
using Profiqo.Infrastructure.Integrations.Whatsapp;
using Profiqo.Infrastructure.Persistence;
using Profiqo.Whatsapp.Automation.Worker;
using Profiqo.Whatsapp.Automation.Worker.Security;
using Profiqo.Whatsapp.Automation.Worker.Tenancy;
using Profiqo.Whatsapp.Automation.Worker.Workers;

using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg.WriteTo.Console());

// Tenant context
builder.Services.AddScoped<AmbientTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
builder.Services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<AmbientTenantContext>());

// Crypto (API ile aynı master key)
builder.Services.AddScoped<ISecretProtector, AesGcmSecretProtector>();

// Persistence
builder.Services.AddProfiqoPersistence(builder.Configuration);

// WhatsApp integration options + httpclient (validator burada kalabilir)
builder.Services.AddWhatsappIntegration(builder.Configuration);

// Sender http client
builder.Services.AddHttpClient<WhatsappCloudSender>();

builder.Services.Configure<WhatsappAutomationOptions>(builder.Configuration.GetSection("Profiqo:WhatsappAutomation"));

builder.Services.AddHostedService<WhatsappSchedulerWorker>();
builder.Services.AddHostedService<WhatsappSenderWorker>();

var host = builder.Build();
await host.RunAsync();