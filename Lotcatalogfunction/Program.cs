using LotCatalogFunction.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<LotGenerationService>();
        services.AddSingleton<CatalogBuildService>();
    })
    .Build();

host.Run();
