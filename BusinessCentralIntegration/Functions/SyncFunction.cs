using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BusinessCentralIntegration.Services;

namespace BusinessCentralIntegration.Functions;

public class SyncFunction
{
    private readonly SyncService _syncService;
    private readonly ILogger<SyncFunction> _logger;

    public SyncFunction(
        SyncService syncService,
        ILogger<SyncFunction> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [Function("SyncAll")]
    public async Task<HttpResponseData> SyncAll(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Starting full bidirectional sync");

        var results = await _syncService.RunFullSyncAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(results));
        return response;
    }

    [Function("PullCustomers")]
    public async Task<HttpResponseData> PullCustomers(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Pulling customers from Business Central");

        var result = await _syncService.PullCustomersAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(result));
        return response;
    }

    [Function("PullItems")]
    public async Task<HttpResponseData> PullItems(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Pulling items from Business Central");

        var result = await _syncService.PullItemsAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(result));
        return response;
    }
}
