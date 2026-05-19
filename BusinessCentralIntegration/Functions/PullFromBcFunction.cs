using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BusinessCentralIntegration.Services;

namespace BusinessCentralIntegration.Functions;

public class PullFromBcFunction
{
    private readonly BusinessCentralApiClient _bcClient;
    private readonly ILogger<PullFromBcFunction> _logger;

    public PullFromBcFunction(
        BusinessCentralApiClient bcClient,
        ILogger<PullFromBcFunction> logger)
    {
        _bcClient = bcClient;
        _logger = logger;
    }

    [Function("GetCompanies")]
    public async Task<HttpResponseData> GetCompanies(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Fetching companies from Business Central");

        var companies = await _bcClient.GetCompaniesAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(companies));
        return response;
    }

    [Function("GetCustomers")]
    public async Task<HttpResponseData> GetCustomers(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Pulling customers from Business Central");

        var companyId = await _bcClient.ResolveCompanyIdAsync();
        var customers = await _bcClient.GetCustomersAsync(companyId);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(customers));
        return response;
    }

    [Function("GetCustomer")]
    public async Task<HttpResponseData> GetCustomer(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetCustomer/{customerId}")] HttpRequestData req,
        string customerId)
    {
        _logger.LogInformation("Fetching customer {Id} from Business Central", customerId);

        var companyId = await _bcClient.ResolveCompanyIdAsync();
        var customer = await _bcClient.GetCustomerAsync(companyId, Guid.Parse(customerId));

        if (customer is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"Customer {customerId} not found.");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(customer));
        return response;
    }

    [Function("GetItems")]
    public async Task<HttpResponseData> GetItems(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Pulling items from Business Central");

        var companyId = await _bcClient.ResolveCompanyIdAsync();
        var items = await _bcClient.GetItemsAsync(companyId);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(items));
        return response;
    }

    [Function("GetItem")]
    public async Task<HttpResponseData> GetItem(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetItem/{itemId}")] HttpRequestData req,
        string itemId)
    {
        _logger.LogInformation("Fetching item {Id} from Business Central", itemId);

        var companyId = await _bcClient.ResolveCompanyIdAsync();
        var item = await _bcClient.GetItemAsync(companyId, Guid.Parse(itemId));

        if (item is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"Item {itemId} not found.");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(item));
        return response;
    }
}
