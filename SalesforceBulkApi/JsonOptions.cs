using System.Text.Json;
using System.Text.Json.Serialization;

namespace SalesforceBulkApi;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used throughout the library.
/// </summary>
internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
