using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SalesforceBulkApi.Models;

/// <summary>
/// Data content type used when uploading batch data.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentType
{
    /// <summary>Comma-separated values (CSV) format.</summary>
    [EnumMember(Value = "CSV")]
    Csv,

    /// <summary>JSON format.</summary>
    [EnumMember(Value = "JSON")]
    Json
}
