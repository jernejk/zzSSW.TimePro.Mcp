namespace SSW.TimePro.Mcp.Server.Configuration;

/// <summary>
/// Configuration settings for the TimePro API connection.
/// </summary>
public class TimeProSettings
{
    public const string SectionName = "TimePro";

    /// <summary>
    /// Production API URL - writes are blocked here for safety.
    /// </summary>
    public const string ProductionUrl = "https://api.sswtimepro.com/";

    /// <summary>
    /// Base URL for the TimePro API.
    /// Defaults to LOCAL environment for safety - production writes are blocked.
    /// SSL certificate validation is bypassed for local-* hostnames.
    /// Supported formats:
    /// - https://api.local-sswtimepro.com:7107/ (local HTTPS - full access)
    /// - https://api.staging-sswtimepro.com/ (staging - full access)
    /// - https://api.sswtimepro.com/ (production - READ ONLY)
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.local-sswtimepro.com:7107/";

    /// <summary>
    /// Primary tenant ID (e.g., 'ssw', 'jk_mcp')
    /// Note: Subdomain can define tenant but x-timepro-tenant-id header overrides it.
    /// </summary>
    public string TenantId { get; set; } = "ssw";

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Fixed application name for API requests.
    /// </summary>
    public string AppName { get; } = "SSW-TimePro-MCP";

    /// <summary>
    /// Confirmation passphrase required to execute write operations.
    /// Set via TIMEPRO_CONFIRM_PHRASE environment variable.
    /// If empty, confirmation ID is sufficient.
    /// </summary>
    public string ConfirmPhrase { get; set; } = string.Empty;

    /// <summary>
    /// Default employee ID for tools (e.g., 'JEK').
    /// When set, tools will use this as the default employee instead of requiring it.
    /// </summary>
    public string DefaultEmployeeId { get; set; } = string.Empty;

    /// <summary>
    /// Returns true if currently connected to production environment.
    /// </summary>
    public bool IsProduction => BaseUrl.Contains("api.sswtimepro.com") &&
                                !BaseUrl.Contains("local-") &&
                                !BaseUrl.Contains("staging-");
    
    /// <summary>
    /// Gets the normalized API base URL (ensures 'api' subdomain and trailing slash).
    /// </summary>
    public string NormalizedApiUrl => NormalizeApiUrl(BaseUrl);
    
    /// <summary>
    /// Normalizes the API URL to ensure it uses the 'api' subdomain.
    /// </summary>
    /// <param name="url">The input URL.</param>
    /// <returns>Normalized URL with 'api' subdomain.</returns>
    public static string NormalizeApiUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;
            
        var uri = new Uri(url);
        var host = uri.Host;
        
        // Parse subdomain (e.g., 'ssw' from 'ssw.local-sswtimepro.com')
        var hostParts = host.Split('.');
        
        if (hostParts.Length >= 2)
        {
            // Replace subdomain with 'api'
            hostParts[0] = "api";
            host = string.Join(".", hostParts);
        }
        
        var builder = new UriBuilder(uri)
        {
            Host = host
        };
        
        var result = builder.Uri.ToString();
        
        // Ensure trailing slash
        if (!result.EndsWith('/'))
            result += '/';
            
        return result;
    }
}
