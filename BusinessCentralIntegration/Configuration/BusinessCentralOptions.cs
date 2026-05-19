namespace BusinessCentralIntegration.Configuration;

public class BusinessCentralOptions
{
    public const string SectionName = "BusinessCentral";

    /// <summary>Azure AD tenant ID (GUID or domain).</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>App-registration client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>App-registration client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>BC environment name, e.g. "sandbox" or "production".</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>Optional: target a specific company by ID. If empty, the first company is used.</summary>
    public string CompanyId { get; set; } = string.Empty;

    public string BaseUrl =>
        $"https://api.businesscentral.dynamics.com/v2.0/{TenantId}/{Environment}/api/v2.0";
}
