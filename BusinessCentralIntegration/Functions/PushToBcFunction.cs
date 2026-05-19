using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BusinessCentralIntegration.Models;
using BusinessCentralIntegration.Services;

namespace BusinessCentralIntegration.Functions;

public class PushToBcFunction
{
    private readonly SyncService _syncService;
    private readonly ILogger<PushToBcFunction> _logger;

    public PushToBcFunction(
        SyncService syncService,
        ILogger<PushToBcFunction> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [Function("PushCustomers")]
    public async Task<HttpResponseData> PushCustomers(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Pushing customers to Business Central");

        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body must contain a JSON array of customers.");
            return bad;
        }

        var customers = JsonSerializer.Deserialize<List<BcCustomer>>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (customers is null || customers.Count == 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("No customers found in request body.");
            return bad;
        }

        var result = await _syncService.PushCustomersAsync(customers);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(result));
        return response;
    }

    [Function("PushItems")]
    public async Task<HttpResponseData> PushItems(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Pushing items to Business Central");

        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body must contain a JSON array of items.");
            return bad;
        }

        var items = JsonSerializer.Deserialize<List<BcItem>>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (items is null || items.Count == 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("No items found in request body.");
            return bad;
        }

        var result = await _syncService.PushItemsAsync(items);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(result));
        return response;
    }
}
