namespace Shared.DTOs;

public class Entity
{
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("updated")]
    public DateTime? Updated { get; set; }

    [JsonPropertyName("status")]
    public StatusType Status { get; set; } = StatusType.Active;
}
