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
/// MCP Tools for creating and updating timesheets.
/// </summary>
[McpServerToolType]
public class TimesheetManagementTools
{
    private readonly ITimeProService _timeProService;
    private readonly IConfirmationService _confirmationService;
    private readonly TimeProSettings _settings;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TimesheetManagementTools(
        ITimeProService timeProService,
        IConfirmationService confirmationService,
        IOptions<TimeProSettings> settings)
    {
        _timeProService = timeProService;
        _confirmationService = confirmationService;
        _settings = settings.Value;
    }

    /// <summary>
    /// Check if write operations should be blocked.
    /// </summary>
    private string? CheckWritePermission()
    {
        if (_settings.IsProduction)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "BLOCKED: Write operations are not allowed on production environment. Use local or staging for testing.",
                environment = "production",
                baseUrl = _settings.BaseUrl,
                hint = "Dry-run operations are allowed - they create confirmations that cannot be executed on production."
            }, _jsonOptions);
        }
        return null;
    }

    /// <summary>
    /// Search for clients.
    /// </summary>
    [McpServerTool]
    [Description("Search for clients to use in a timesheet. Returns client ID and name.")]
    public async Task<string> SearchClients(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Search text for client name")] string searchText,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clients = await _timeProService.SearchClientsAsync(
                employeeId, searchText, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = clients.Count,
                clients
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
    /// Get projects for a client.
    /// </summary>
    [McpServerTool]
    [Description("Get available projects for a specific client. Use SearchClients first to get the client ID.")]
    public async Task<string> GetProjectsForClient(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("The client ID (from SearchClients)")] string clientId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projects = await _timeProService.GetProjectsForClientAsync(
                employeeId, clientId, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = projects.Count,
                clientId,
                projects = projects.Select(p => new
                {
                    projectId = p.Value,
                    name = p.DisplayText,
                    useIteration = p.UseIteration,
                    isGeneral = p.IsGeneral,
                    isLeave = p.IsLeave
                })
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
    /// Get the employee's billable rate for a client.
    /// </summary>
    [McpServerTool]
    [Description("Get the employee's billable rate for a specific client. Useful for determining sellPrice when creating billable timesheets.")]
    public async Task<string> GetClientRate(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("The client ID")] string clientId,
        [Description("Date for rate lookup (yyyy-MM-dd). Defaults to today.")] string? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dateOnly = string.IsNullOrEmpty(date) ? (DateOnly?)null : DateOnly.Parse(date);
            var rate = await _timeProService.GetClientRateAsync(
                employeeId, clientId, dateOnly, cancellationToken);

            if (rate == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"No rate found for employee {employeeId} with client {clientId}"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                employeeId = rate.EmpId,
                clientId = rate.ClientId,
                rate = rate.Rate,
                prepaidRate = rate.PrepaidRate,
                employeeName = rate.EmployeeName,
                clientName = rate.ClientName,
                expiryDate = rate.ExpiryDate?.ToString("yyyy-MM-dd"),
                notes = rate.Notes
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
    /// Create a new timesheet.
    /// </summary>
    [McpServerTool]
    [Description("Create a new timesheet entry. Use GetTimesheetReferenceData first to get valid IDs. With dryRun=true, creates a confirmation that can be executed later.")]
    public async Task<string> CreateTimesheet(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Client ID")] string clientId,
        [Description("Project ID")] string projectId,
        [Description("Date (yyyy-MM-dd)")] string date,
        [Description("Start time (HH:mm)")] string startTime,
        [Description("End time (HH:mm)")] string endTime,
        [Description("Category ID")] string categoryId,
        [Description("Location ID")] string locationId,
        [Description("Notes/description (optional)")] string? notes = null,
        [Description("Time less in hours (optional, default: 0)")] decimal timeLess = 0,
        [Description("Billable ID (optional, default: 'BILLABLE'). Use 'W' for internal work.")] string billableId = "BILLABLE",
        [Description("Sell price per hour (optional). Required for billable work, use 0 for internal.")] decimal? sellPrice = null,
        [Description("Iteration ID (optional)")] int? iterationId = null,
        [Description("Dry run - create confirmation without saving (default: true)")] bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dateTime = DateTime.Parse(date);
            var startDateTime = DateTime.Parse($"{date} {startTime}");
            var endDateTime = DateTime.Parse($"{date} {endTime}");

            var request = new TimesheetRequest
            {
                EmpId = employeeId,
                ClientId = clientId,
                ProjectId = projectId,
                DateCreated = dateTime.ToString("yyyy-MM-ddT00:00:00"),
                TimeStart = startDateTime.ToString("yyyy-MM-ddTHH:mm:00"),
                TimeEnd = endDateTime.ToString("yyyy-MM-ddTHH:mm:00"),
                CategoryId = categoryId,
                LocationId = locationId,
                Note = notes,
                TimeLess = timeLess,
                BillableId = billableId,
                SellPrice = sellPrice,
                IterationId = iterationId
            };
            
            if (dryRun)
            {
                var hours = (endDateTime - startDateTime).TotalHours - (double)timeLess;
                var preview = $"Create timesheet for {employeeId} on {date}: {clientId}/{projectId} ({startTime}-{endTime}, {hours:F1}h)";

                var confirmation = await _confirmationService.CreateConfirmationAsync(
                    ConfirmationOperationType.CreateTimesheet,
                    $"Create timesheet for {employeeId} on {date}",
                    preview,
                    request,
                    cancellationToken: cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    dryRun = true,
                    confirmationId = confirmation.Id,
                    message = $"Confirmation created. Use ConfirmOperation with ID '{confirmation.Id}' to execute.",
                    preview,
                    expiresAt = confirmation.ExpiresAt,
                    request,
                    isProduction = _settings.IsProduction,
                    productionWarning = _settings.IsProduction ? "This confirmation CANNOT be executed on production." : null
                }, _jsonOptions);
            }

            // Block direct writes to production
            var blocked = CheckWritePermission();
            if (blocked != null) return blocked;

            var response = await _timeProService.CreateTimesheetAsync(request, dryRun: false, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = response.Success,
                message = response.Message,
                timesheetId = response.TimesheetId,
                dryRun = false,
                request
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
    /// Update an existing timesheet.
    /// </summary>
    [McpServerTool]
    [Description("Update an existing timesheet entry. Use GetTimesheetById first to get current values.")]
    public async Task<string> UpdateTimesheet(
        [Description("The timesheet ID to update")] int timesheetId,
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Client ID")] string clientId,
        [Description("Project ID")] string projectId,
        [Description("Date (yyyy-MM-dd)")] string date,
        [Description("Start time (HH:mm)")] string startTime,
        [Description("End time (HH:mm)")] string endTime,
        [Description("Category ID")] string categoryId,
        [Description("Location ID")] string locationId,
        [Description("Notes/description (optional)")] string? notes = null,
        [Description("Time less in hours (optional, default: 0)")] decimal timeLess = 0,
        [Description("Billable ID (optional, default: 'BILLABLE')")] string billableId = "BILLABLE",
        [Description("Sell price per hour (optional). Required for billable work, use 0 for internal.")] decimal? sellPrice = null,
        [Description("Iteration ID (optional)")] int? iterationId = null,
        [Description("Dry run - validate without saving (default: false)")] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dateTime = DateTime.Parse(date);
            var startDateTime = DateTime.Parse($"{date} {startTime}");
            var endDateTime = DateTime.Parse($"{date} {endTime}");
            
            var request = new EditTimesheetRequest
            {
                TimeId = timesheetId,
                EmpId = employeeId,
                ClientId = clientId,
                ProjectId = projectId,
                DateCreated = dateTime.ToString("yyyy-MM-ddT00:00:00"),
                TimeStart = startDateTime.ToString("yyyy-MM-ddTHH:mm:00"),
                TimeEnd = endDateTime.ToString("yyyy-MM-ddTHH:mm:00"),
                CategoryId = categoryId,
                LocationId = locationId,
                Note = notes,
                TimeLess = timeLess,
                BillableId = billableId,
                SellPrice = sellPrice,
                IterationId = iterationId
            };


            // Block direct writes to production (unless dry run)
            if (!dryRun)
            {
                var blocked = CheckWritePermission();
                if (blocked != null) return blocked;
            }

            var response = await _timeProService.UpdateTimesheetAsync(request, dryRun, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = response.Success,
                message = response.Message,
                timesheetId = response.TimesheetId,
                dryRun,
                request
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
    /// Delete a timesheet.
    /// </summary>
    [McpServerTool]
    [Description("Delete a timesheet entry. With dryRun=true, creates a confirmation that can be executed later.")]
    public async Task<string> DeleteTimesheet(
        [Description("The timesheet ID to delete")] int timesheetId,
        [Description("Dry run - create confirmation without deleting (default: true)")] bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (dryRun)
            {
                var preview = $"Delete timesheet ID: {timesheetId}";

                var confirmation = await _confirmationService.CreateConfirmationAsync(
                    ConfirmationOperationType.DeleteTimesheet,
                    $"Delete timesheet {timesheetId}",
                    preview,
                    new { TimesheetId = timesheetId },
                    cancellationToken: cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    dryRun = true,
                    confirmationId = confirmation.Id,
                    message = $"Confirmation created. Use ConfirmOperation with ID '{confirmation.Id}' to execute.",
                    preview,
                    expiresAt = confirmation.ExpiresAt,
                    timesheetId,
                    isProduction = _settings.IsProduction,
                    productionWarning = _settings.IsProduction ? "This confirmation CANNOT be executed on production." : null
                }, _jsonOptions);
            }

            // Block direct writes to production
            var blocked = CheckWritePermission();
            if (blocked != null) return blocked;

            var response = await _timeProService.DeleteTimesheetAsync(
                timesheetId, dryRun: false, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = response.Success,
                message = response.Message,
                timesheetId,
                dryRun = false
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
}

