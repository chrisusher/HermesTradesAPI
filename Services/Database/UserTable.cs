using Shared.Database;
using Shared.DTOs.Users;

namespace Services.Database;

/// <summary>
/// Cosmos DB table for storing user information
/// </summary>
public class UserTable : CosmosTable
{
    public string Username { get; set; } = string.Empty;

    public CurrencyCode CurrencyCode { get; set; } = CurrencyCode.Unknown;

    /// <summary>
    /// Converts the database table to a User DTO
    /// </summary>
    /// <returns>User DTO</returns>
    public User ToUser() => new()
    {
        UserId = Guid.Parse(PartitionKey),
        Username = Username,
        CurrencyCode = CurrencyCode,
        Created = Created,
        Updated = Updated,
        Status = Status
    };
}