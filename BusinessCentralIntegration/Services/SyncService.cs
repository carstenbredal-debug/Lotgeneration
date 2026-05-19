using Microsoft.Extensions.Logging;
using BusinessCentralIntegration.Models;

namespace BusinessCentralIntegration.Services;

public class SyncService
{
    private readonly BusinessCentralApiClient _bcClient;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        BusinessCentralApiClient bcClient,
        ILogger<SyncService> logger)
    {
        _bcClient = bcClient;
        _logger = logger;
    }

    /// <summary>
    /// Pull all customers from Business Central.
    /// </summary>
    public async Task<SyncResult> PullCustomersAsync()
    {
        var result = new SyncResult
        {
            Direction = "Pull",
            EntityType = "Customer"
        };

        try
        {
            var companyId = await _bcClient.ResolveCompanyIdAsync();
            var customers = await _bcClient.GetCustomersAsync(companyId);

            result.TotalProcessed = customers.Count;
            _logger.LogInformation("Pulled {Count} customers from Business Central", customers.Count);
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
            result.Failed++;
            _logger.LogError(ex, "Failed to pull customers");
        }

        return result;
    }

    /// <summary>
    /// Pull all items from Business Central.
    /// </summary>
    public async Task<SyncResult> PullItemsAsync()
    {
        var result = new SyncResult
        {
            Direction = "Pull",
            EntityType = "Item"
        };

        try
        {
            var companyId = await _bcClient.ResolveCompanyIdAsync();
            var items = await _bcClient.GetItemsAsync(companyId);

            result.TotalProcessed = items.Count;
            _logger.LogInformation("Pulled {Count} items from Business Central", items.Count);
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
            result.Failed++;
            _logger.LogError(ex, "Failed to pull items");
        }

        return result;
    }

    /// <summary>
    /// Push a list of customers to Business Central (create or update).
    /// Matches by customer number — creates if not found, updates if exists.
    /// </summary>
    public async Task<SyncResult> PushCustomersAsync(List<BcCustomer> customers)
    {
        var result = new SyncResult
        {
            Direction = "Push",
            EntityType = "Customer",
            TotalProcessed = customers.Count
        };

        var companyId = await _bcClient.ResolveCompanyIdAsync();

        foreach (var customer in customers)
        {
            try
            {
                var existing = await _bcClient.GetCustomerByNumberAsync(companyId, customer.Number);

                if (existing is null)
                {
                    await _bcClient.CreateCustomerAsync(companyId, customer);
                    result.Created++;
                    _logger.LogInformation("Created customer {Number}", customer.Number);
                }
                else
                {
                    customer.Id = existing.Id;
                    customer.ETag = existing.ETag;
                    await _bcClient.UpdateCustomerAsync(companyId, customer);
                    result.Updated++;
                    _logger.LogInformation("Updated customer {Number}", customer.Number);
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Customer {customer.Number}: {ex.Message}");
                _logger.LogError(ex, "Failed to sync customer {Number}", customer.Number);
            }
        }

        return result;
    }

    /// <summary>
    /// Push a list of items to Business Central (create or update).
    /// Matches by item number — creates if not found, updates if exists.
    /// </summary>
    public async Task<SyncResult> PushItemsAsync(List<BcItem> items)
    {
        var result = new SyncResult
        {
            Direction = "Push",
            EntityType = "Item",
            TotalProcessed = items.Count
        };

        var companyId = await _bcClient.ResolveCompanyIdAsync();

        foreach (var item in items)
        {
            try
            {
                var existing = await _bcClient.GetItemByNumberAsync(companyId, item.Number);

                if (existing is null)
                {
                    await _bcClient.CreateItemAsync(companyId, item);
                    result.Created++;
                    _logger.LogInformation("Created item {Number}", item.Number);
                }
                else
                {
                    item.Id = existing.Id;
                    item.ETag = existing.ETag;
                    await _bcClient.UpdateItemAsync(companyId, item);
                    result.Updated++;
                    _logger.LogInformation("Updated item {Number}", item.Number);
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Item {item.Number}: {ex.Message}");
                _logger.LogError(ex, "Failed to sync item {Number}", item.Number);
            }
        }

        return result;
    }

    /// <summary>
    /// Full bidirectional sync: pull from BC, then push sample data back.
    /// In a real scenario, the pull data would be transformed and reconciled.
    /// </summary>
    public async Task<List<SyncResult>> RunFullSyncAsync()
    {
        var results = new List<SyncResult>();

        _logger.LogInformation("Starting full bidirectional sync...");

        var pullCustomers = await PullCustomersAsync();
        results.Add(pullCustomers);

        var pullItems = await PullItemsAsync();
        results.Add(pullItems);

        _logger.LogInformation(
            "Full sync complete. Customers pulled: {Customers}, Items pulled: {Items}",
            pullCustomers.TotalProcessed,
            pullItems.TotalProcessed);

        return results;
    }
}
