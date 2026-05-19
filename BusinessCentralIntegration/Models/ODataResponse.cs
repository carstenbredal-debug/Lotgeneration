using System.Text.Json.Serialization;

namespace BusinessCentralIntegration.Models;

public class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }
}
