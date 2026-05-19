using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BusinessCentralIntegration.Configuration;
using BusinessCentralIntegration.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.Configure<BusinessCentralOptions>(opts =>
        {
            opts.TenantId = Environment.GetEnvironmentVariable("BC_TENANT_ID") ?? "";
            opts.ClientId = Environment.GetEnvironmentVariable("BC_CLIENT_ID") ?? "";
            opts.ClientSecret = Environment.GetEnvironmentVariable("BC_CLIENT_SECRET") ?? "";
            opts.Environment = Environment.GetEnvironmentVariable("BC_ENVIRONMENT") ?? "sandbox";
            opts.CompanyId = Environment.GetEnvironmentVariable("BC_COMPANY_ID") ?? "";
        });

        services.AddSingleton<BusinessCentralAuthService>();
        services.AddHttpClient<BusinessCentralApiClient>();
        services.AddSingleton<SyncService>();
    })
    .Build();

host.Run();
