using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Configuration;
using SSW.TimePro.Mcp.Server.Models;
using SSW.TimePro.Mcp.Server.Services;
using SSW.TimePro.Mcp.Server.Services.Confirmation;

namespace SSW.TimePro.Mcp.Server.Tools;

/// <summary>
/// MCP Tools for managing confirmations (dry-run operations).
/// </summary>
[McpServerToolType]
public class ConfirmationTools
{
    private readonly IConfirmationService _confirmationService;
    private readonly ITimeProService _timeProService;
    private readonly TimeProSettings _settings;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfirmationTools(
        IConfirmationService confirmationService,
        ITimeProService timeProService,
        IOptions<TimeProSettings> settings)
    {
        _confirmationService = confirmationService;
        _timeProService = timeProService;
        _settings = settings.Value;
    }

    /// <summary>
    /// List all pending confirmations.
    /// </summary>
    [McpServerTool]
    [Description("List all pending confirmations from dry-run operations. Use this to see what operations are waiting for confirmation.")]
    public async Task<string> ListPendingConfirmations(
        [Description("Filter by status: 'Pending', 'Confirmed', 'Expired', 'Failed', 'Cancelled'. Leave empty for all.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ConfirmationStatus? statusFilter = null;
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ConfirmationStatus>(status, true, out var parsed))
            {
                statusFilter = parsed;
            }
            
            var confirmations = await _confirmationService.ListConfirmationsAsync(statusFilter, cancellationToken);
            var summaries = confirmations.Select(ConfirmationSummary.FromPendingConfirmation).ToList();
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                count = summaries.Count,
                confirmations = summaries
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Get details of a specific confirmation.
    /// </summary>
    [McpServerTool]
    [Description("Get full details of a pending confirmation including the payload that will be executed.")]
    public async Task<string> GetConfirmation(
        [Description("The confirmation ID to retrieve")] string confirmationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var confirmation = await _confirmationService.GetConfirmationAsync(confirmationId, cancellationToken);
            
            if (confirmation == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Confirmation '{confirmationId}' not found."
                }, _jsonOptions);
            }
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                confirmation
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Execute a pending confirmation.
    /// </summary>
    [McpServerTool]
    [Description("Execute a pending confirmation. This will perform the actual operation (create/update/delete timesheet). If a confirmation passphrase is configured, you must provide it. Production environment is READ-ONLY.")]
    public async Task<string> ConfirmOperation(
        [Description("The confirmation ID to execute")] string confirmationId,
        [Description("Confirmation passphrase (required if TIMEPRO_CONFIRM_PHRASE is set). e.g., 'let's do it!'")] string? passphrase = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Block production writes
            if (_settings.IsProduction)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "BLOCKED: Write operations are not allowed on production environment. Use local or staging for testing.",
                    environment = "production",
                    baseUrl = _settings.BaseUrl
                }, _jsonOptions);
            }

            // Validate passphrase if configured
            if (!string.IsNullOrEmpty(_settings.ConfirmPhrase))
            {
                if (string.IsNullOrEmpty(passphrase))
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "Confirmation passphrase required. Set via TIMEPRO_CONFIRM_PHRASE environment variable.",
                        hint = "Provide the passphrase parameter to confirm this operation."
                    }, _jsonOptions);
                }

                if (!string.Equals(passphrase, _settings.ConfirmPhrase, StringComparison.Ordinal))
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "Invalid confirmation passphrase."
                    }, _jsonOptions);
                }
            }

            var confirmation = await _confirmationService.GetConfirmationAsync(confirmationId, cancellationToken);

            if (confirmation == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Confirmation '{confirmationId}' not found."
                }, _jsonOptions);
            }

            var result = await _confirmationService.ExecuteConfirmationAsync(
                confirmationId,
                async (payload, ct) => await ExecuteOperationAsync(confirmation.OperationType, payload, ct),
                cancellationToken);

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Cancel a pending confirmation.
    /// </summary>
    [McpServerTool]
    [Description("Cancel a pending confirmation. The operation will not be executed.")]
    public async Task<string> CancelConfirmation(
        [Description("The confirmation ID to cancel")] string confirmationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _confirmationService.CancelConfirmationAsync(confirmationId, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success,
                message = success 
                    ? $"Confirmation '{confirmationId}' cancelled successfully."
                    : $"Failed to cancel confirmation '{confirmationId}'. It may not exist or is not pending."
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Clean up expired and old confirmations.
    /// </summary>
    [McpServerTool]
    [Description("Clean up expired confirmations and old completed confirmations (older than 7 days).")]
    public async Task<string> CleanupConfirmations(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cleaned = await _confirmationService.CleanupExpiredAsync(cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Cleaned up {cleaned} confirmation(s).",
                cleanedCount = cleaned
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }
    
    private async Task<object> ExecuteOperationAsync(
        ConfirmationOperationType operationType,
        object? payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        
        return operationType switch
        {
            ConfirmationOperationType.CreateTimesheet => 
                await ExecuteCreateTimesheetAsync(json, cancellationToken),
            
            ConfirmationOperationType.UpdateTimesheet => 
                await ExecuteUpdateTimesheetAsync(json, cancellationToken),
            
            ConfirmationOperationType.DeleteTimesheet => 
                await ExecuteDeleteTimesheetAsync(json, cancellationToken),
            
            ConfirmationOperationType.DeleteSuggestedTimesheet => 
                await ExecuteDeleteSuggestedTimesheetAsync(json, cancellationToken),
            
            ConfirmationOperationType.AcceptSuggestedTimesheet => 
                await ExecuteAcceptSuggestedTimesheetAsync(json, cancellationToken),
                
            ConfirmationOperationType.BulkCreateTimesheets =>
                await ExecuteBulkCreateTimesheetsAsync(json, cancellationToken),
            
            _ => throw new NotSupportedException($"Operation type '{operationType}' is not supported.")
        };
    }
    
    private async Task<object> ExecuteCreateTimesheetAsync(string json, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<TimesheetRequest>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid payload for create timesheet.");
        
        return await _timeProService.CreateTimesheetAsync(request, dryRun: false, cancellationToken);
    }
    
    private async Task<object> ExecuteUpdateTimesheetAsync(string json, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<EditTimesheetRequest>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid payload for update timesheet.");
        
        return await _timeProService.UpdateTimesheetAsync(request, dryRun: false, cancellationToken);
    }
    
    private async Task<object> ExecuteDeleteTimesheetAsync(string json, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<DeleteTimesheetPayload>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid payload for delete timesheet.");
        
        return await _timeProService.DeleteTimesheetAsync(payload.TimesheetId, dryRun: false, cancellationToken);
    }
    
    private async Task<object> ExecuteDeleteSuggestedTimesheetAsync(string json, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<DeleteSuggestedTimesheetPayload>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid payload for delete suggested timesheet.");
        
        return await _timeProService.DeleteSuggestedTimesheetAsync(payload.SuggestedTimesheetId, dryRun: false, cancellationToken);
    }
    
    private async Task<object> ExecuteAcceptSuggestedTimesheetAsync(string json, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<AcceptSuggestedTimesheetPayload>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid payload for accept suggested timesheet.");
        
        return await _timeProService.AcceptSuggestedTimesheetAsync(
            payload.SuggestedTimesheetId, payload.NewSellPrice, payload.Notes, payload.Location, cancellationToken);
    }
    
    private async Task<object> ExecuteBulkCreateTimesheetsAsync(string json, CancellationToken cancellationToken)
    {
        var payloads = JsonSerializer.Deserialize<List<TimesheetRequest>>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid payload for bulk create timesheets.");
        
        var results = new List<TimesheetResponse>();
        foreach (var request in payloads)
        {
            var result = await _timeProService.CreateTimesheetAsync(request, dryRun: false, cancellationToken);
            results.Add(result);
        }
        
        return new
        {
            totalCount = results.Count,
            successCount = results.Count(r => r.Success),
            failedCount = results.Count(r => !r.Success),
            results
        };
    }
}

// Payload classes for deserialization
file record DeleteTimesheetPayload(int TimesheetId);
file record DeleteSuggestedTimesheetPayload(int SuggestedTimesheetId);
file record AcceptSuggestedTimesheetPayload(int SuggestedTimesheetId, decimal? NewSellPrice, string? Notes = null, string? Location = null);
