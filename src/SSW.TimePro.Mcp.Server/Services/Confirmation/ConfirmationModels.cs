using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Services.Confirmation;

/// <summary>
/// Types of operations that can be confirmed.
/// </summary>
public enum ConfirmationOperationType
{
    CreateTimesheet,
    UpdateTimesheet,
    DeleteTimesheet,
    DeleteSuggestedTimesheet,
    AcceptSuggestedTimesheet,
    BulkCreateTimesheets,
    BulkUpdateTimesheets
}

/// <summary>
/// Status of a confirmation request.
/// </summary>
public enum ConfirmationStatus
{
    Pending,
    Confirmed,
    Expired,
    Failed,
    Cancelled
}

/// <summary>
/// A pending confirmation that can be executed.
/// </summary>
public class PendingConfirmation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("operationType")]
    public ConfirmationOperationType OperationType { get; set; }
    
    [JsonPropertyName("status")]
    public ConfirmationStatus Status { get; set; } = ConfirmationStatus.Pending;
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("preview")]
    public string Preview { get; set; } = string.Empty;
    
    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
    
    [JsonPropertyName("result")]
    public object? Result { get; set; }
    
    [JsonPropertyName("executedAt")]
    public DateTime? ExecutedAt { get; set; }
    
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Check if the confirmation has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    
    /// <summary>
    /// Check if the confirmation can be executed.
    /// </summary>
    public bool CanExecute => Status == ConfirmationStatus.Pending && !IsExpired;
}

/// <summary>
/// Result of a confirmation execution.
/// </summary>
public class ConfirmationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("confirmationId")]
    public string ConfirmationId { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("result")]
    public object? Result { get; set; }
}

/// <summary>
/// Summary of a pending confirmation for display.
/// </summary>
public class ConfirmationSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
    
    [JsonPropertyName("canExecute")]
    public bool CanExecute { get; set; }
    
    public static ConfirmationSummary FromPendingConfirmation(PendingConfirmation pc) => new()
    {
        Id = pc.Id,
        OperationType = pc.OperationType.ToString(),
        Status = pc.Status.ToString(),
        Description = pc.Description,
        CreatedAt = pc.CreatedAt,
        ExpiresAt = pc.ExpiresAt,
        CanExecute = pc.CanExecute
    };
}
