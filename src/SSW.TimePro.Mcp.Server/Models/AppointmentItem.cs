using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Models;

/// <summary>
/// Represents a CRM appointment from TimePro.
/// </summary>
public class AppointmentItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;
    
    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;
    
    [JsonPropertyName("allDay")]
    public bool AllDay { get; set; }
    
    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }
    
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }
    
    [JsonPropertyName("iterationId")]
    public string? IterationId { get; set; }
    
    [JsonPropertyName("editable")]
    public bool Editable { get; set; }
    
    [JsonPropertyName("timeZoneOffsetInMinutes")]
    public int TimeZoneOffsetInMinutes { get; set; }
}
