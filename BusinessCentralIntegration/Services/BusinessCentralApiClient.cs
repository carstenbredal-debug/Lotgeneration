using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BusinessCentralIntegration.Configuration;
using BusinessCentralIntegration.Models;

namespace BusinessCentralIntegration.Services;

public class BusinessCentralApiClient
{
    private readonly HttpClient _httpClient;
    private readonly BusinessCentralAuthService _authService;
    private readonly BusinessCentralOptions _options;
    private readonly ILogger<BusinessCentralApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BusinessCentralApiClient(
        HttpClient httpClient,
        BusinessCentralAuthService authService,
        IOptions<BusinessCentralOptions> options,
        ILogger<BusinessCentralApiClient> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
        _logger = logger;
    }

    // ── Companies ──────────────────────────────────────────────

    public async Task<List<BcCompany>> GetCompaniesAsync()
    {
        var url = $"{_options.BaseUrl}/companies";
        return await GetListAsync<BcCompany>(url);
    }

    public async Task<Guid> ResolveCompanyIdAsync()
    {
        if (Guid.TryParse(_options.CompanyId, out var configured))
            return configured;

        var companies = await GetCompaniesAsync();
        if (companies.Count == 0)
            throw new InvalidOperationException("No companies found in Business Central.");

        var company = companies[0];
        _logger.LogInformation("Using company '{Name}' ({Id})", company.DisplayName, company.Id);
        return company.Id;
    }

    // ── Customers ──────────────────────────────────────────────

    public async Task<List<BcCustomer>> GetCustomersAsync(Guid companyId, int top = 100)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/customers?$top={top}";
        return await GetListAsync<BcCustomer>(url);
    }

    public async Task<BcCustomer?> GetCustomerAsync(Guid companyId, Guid customerId)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/customers({customerId})";
        return await GetSingleAsync<BcCustomer>(url);
    }

    public async Task<BcCustomer?> GetCustomerByNumberAsync(Guid companyId, string number)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/customers?$filter=number eq '{number}'";
        var items = await GetListAsync<BcCustomer>(url);
        return items.FirstOrDefault();
    }

    public async Task<BcCustomer> CreateCustomerAsync(Guid companyId, BcCustomer customer)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/customers";
        return await PostAsync<BcCustomer>(url, customer);
    }

    public async Task<BcCustomer> UpdateCustomerAsync(Guid companyId, BcCustomer customer)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/customers({customer.Id})";
        return await PatchAsync<BcCustomer>(url, customer, customer.ETag);
    }

    // ── Items ──────────────────────────────────────────────────

    public async Task<List<BcItem>> GetItemsAsync(Guid companyId, int top = 100)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/items?$top={top}";
        return await GetListAsync<BcItem>(url);
    }

    public async Task<BcItem?> GetItemAsync(Guid companyId, Guid itemId)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/items({itemId})";
        return await GetSingleAsync<BcItem>(url);
    }

    public async Task<BcItem?> GetItemByNumberAsync(Guid companyId, string number)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/items?$filter=number eq '{number}'";
        var items = await GetListAsync<BcItem>(url);
        return items.FirstOrDefault();
    }

    public async Task<BcItem> CreateItemAsync(Guid companyId, BcItem item)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/items";
        return await PostAsync<BcItem>(url, item);
    }

    public async Task<BcItem> UpdateItemAsync(Guid companyId, BcItem item)
    {
        var url = $"{_options.BaseUrl}/companies({companyId})/items({item.Id})";
        return await PatchAsync<BcItem>(url, item, item.ETag);
    }

    // ── HTTP helpers ───────────────────────────────────────────

    private async Task SetAuthHeaderAsync()
    {
        var token = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<List<T>> GetListAsync<T>(string url)
    {
        await SetAuthHeaderAsync();
        _logger.LogInformation("GET {Url}", url);

        var response = await _httpClient.GetAsync(url);
        await EnsureSuccessAsync(response);

        var odata = await response.Content.ReadFromJsonAsync<ODataResponse<T>>(JsonOptions);
        return odata?.Value ?? new List<T>();
    }

    private async Task<T?> GetSingleAsync<T>(string url) where T : class
    {
        await SetAuthHeaderAsync();
        _logger.LogInformation("GET {Url}", url);

        var response = await _httpClient.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task<T> PostAsync<T>(string url, T payload)
    {
        await SetAuthHeaderAsync();
        _logger.LogInformation("POST {Url}", url);

        var response = await _httpClient.PostAsJsonAsync(url, payload, JsonOptions);
        await EnsureSuccessAsync(response);

        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<T> PatchAsync<T>(string url, T payload, string? etag)
    {
        await SetAuthHeaderAsync();
        _logger.LogInformation("PATCH {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        if (!string.IsNullOrEmpty(etag))
            request.Headers.Add("If-Match", etag);

        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("BC API error {Status}: {Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }
    }
}
