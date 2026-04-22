using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SalesforceBulkApi.Models;

/// <summary>
/// Bulk API v2 ingest operation types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationType
{
    /// <summary>Insert new records.</summary>
    [EnumMember(Value = "insert")]
    Insert,

    /// <summary>Update existing records by Id.</summary>
    [EnumMember(Value = "update")]
    Update,

    /// <summary>Insert or update records based on an external ID field.</summary>
    [EnumMember(Value = "upsert")]
    Upsert,

    /// <summary>Delete records by Id.</summary>
    [EnumMember(Value = "delete")]
    Delete,

    /// <summary>Hard delete records (does not place in recycle bin).</summary>
    [EnumMember(Value = "hardDelete")]
    HardDelete
}
