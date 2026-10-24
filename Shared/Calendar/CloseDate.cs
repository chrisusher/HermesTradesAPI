namespace Shared.Calendar;

public class CloseDate
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;        
}