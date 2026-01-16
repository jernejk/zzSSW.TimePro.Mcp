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
    /// Get reference data for creating timesheets.
    /// </summary>
    [McpServerTool]
    [Description("Get reference data needed to create a timesheet (categories, locations, billable types). Call this before creating a timesheet.")]
    public async Task<string> GetTimesheetReferenceData(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Date for the timesheet (yyyy-MM-dd). Defaults to today.")] string? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = string.IsNullOrEmpty(date)
                ? DateOnly.FromDateTime(DateTime.Today)
                : DateOnly.Parse(date);
            
            var categoriesTask = _timeProService.GetTimesheetCategoriesAsync(cancellationToken);
            var locationsTask = _timeProService.GetTimesheetLocationsAsync(cancellationToken);
            var billableTypesTask = _timeProService.GetTimesheetBillableTypesAsync(cancellationToken);
            var viewDataTask = _timeProService.GetAddTimesheetViewAsync(employeeId, targetDate, cancellationToken);
            
            await Task.WhenAll(categoriesTask, locationsTask, billableTypesTask, viewDataTask);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                date = targetDate.ToString("yyyy-MM-dd"),
                defaults = await viewDataTask,
                categories = await categoriesTask,
                locations = await locationsTask,
                billableTypes = await billableTypesTask
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
        [Description("Billable ID (optional, default: 'BILLABLE')")] string billableId = "BILLABLE",
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
    /// Accept a suggested timesheet.
    /// </summary>
    [McpServerTool]
    [Description("Accept a suggested timesheet, converting it to a regular timesheet entry. Production is READ-ONLY.")]
    public async Task<string> AcceptSuggestedTimesheet(
        [Description("The suggested timesheet ID to accept")] int suggestedTimesheetId,
        [Description("New sell price (optional, to override the suggested rate)")] decimal? newSellPrice = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Block writes to production
            var blocked = CheckWritePermission();
            if (blocked != null) return blocked;

            var response = await _timeProService.AcceptSuggestedTimesheetAsync(
                suggestedTimesheetId, newSellPrice, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = response.Success,
                message = response.Message,
                suggestedTimesheetId
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

    /// <summary>
    /// Delete a suggested timesheet.
    /// </summary>
    [McpServerTool]
    [Description("Delete/reject a suggested timesheet. Use dry run to preview what would be deleted. Production is READ-ONLY.")]
    public async Task<string> DeleteSuggestedTimesheet(
        [Description("The suggested timesheet ID to delete")] int suggestedTimesheetId,
        [Description("Dry run - preview without actually deleting (default: false)")] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Block direct writes to production (unless dry run)
            if (!dryRun)
            {
                var blocked = CheckWritePermission();
                if (blocked != null) return blocked;
            }

            var response = await _timeProService.DeleteSuggestedTimesheetAsync(
                suggestedTimesheetId, dryRun, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = response.Success,
                message = response.Message,
                suggestedTimesheetId,
                dryRun
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

