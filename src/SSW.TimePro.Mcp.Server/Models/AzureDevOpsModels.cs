using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Models;

/// <summary>
/// Result from Azure DevOps subscription containing commits or work items.
/// </summary>
public class AzureDevOpsSubscriptionResult<T> where T : class
{
    [JsonPropertyName("subscription")]
    public AzureDevOpsSubscription? Subscription { get; set; }

    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = [];

    [JsonPropertyName("errors")]
    public List<AzureDevOpsConnectionError> Errors { get; set; } = [];
}

/// <summary>
/// Azure DevOps subscription info.
/// </summary>
public class AzureDevOpsSubscription
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Git commit/change from Azure DevOps.
/// </summary>
public class AzureDevOpsGitChange
{
    [JsonPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; set; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }
}

/// <summary>
/// Connection error from Azure DevOps integration.
/// </summary>
public class AzureDevOpsConnectionError
{
    [JsonPropertyName("errorType")]
    public string ErrorType { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}
