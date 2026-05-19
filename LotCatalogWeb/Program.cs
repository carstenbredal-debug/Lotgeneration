using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LotCatalogWeb;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var baseUri = new Uri(builder.HostEnvironment.BaseAddress);
var cleanBase = new UriBuilder(baseUri) { UserName = "", Password = "" }.Uri;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = cleanBase });

await builder.Build().RunAsync();
