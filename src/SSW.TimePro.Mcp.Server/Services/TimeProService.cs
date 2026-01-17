using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SSW.TimePro.Mcp.Server.Configuration;
using SSW.TimePro.Mcp.Server.Models;

namespace SSW.TimePro.Mcp.Server.Services;

/// <summary>
/// Interface for TimePro API operations.
/// </summary>
public interface ITimeProService
{
    /// <summary>
    /// Get timesheets for a specific employee and date range.
    /// </summary>
    Task<List<TimesheetItem>> GetTimesheetsAsync(
        string employeeId, 
        DateOnly startDate, 
        DateOnly endDate, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get timesheets for X days from today.
    /// </summary>
    Task<List<TimesheetItem>> GetTimesheetsByDaysAsync(
        string employeeId,
        int takeDays = 7,
        int skipDays = 0,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get suggested timesheets for a specific date.
    /// </summary>
    Task<List<TimesheetItem>> GetSuggestedTimesheetsAsync(
        string employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get CRM appointments for a date range.
    /// </summary>
    Task<List<AppointmentItem>> GetAppointmentsAsync(
        string employeeId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Refresh suggested timesheets for a specific date.
    /// </summary>
    Task<bool> RefreshSuggestedTimesheetsAsync(
        string employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get add timesheet view data.
    /// </summary>
    Task<AddTimesheetViewDto?> GetAddTimesheetViewAsync(
        string employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get timesheet categories.
    /// </summary>
    Task<List<TimesheetCategory>> GetTimesheetCategoriesAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get timesheet locations.
    /// </summary>
    Task<List<TimesheetLocation>> GetTimesheetLocationsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get timesheet billable types.
    /// </summary>
    Task<List<TimesheetBillableType>> GetTimesheetBillableTypesAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search clients.
    /// </summary>
    Task<List<ClientSearchResult>> SearchClientsAsync(
        string employeeId,
        string searchText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get projects for a client.
    /// </summary>
    Task<List<ProjectForSelect>> GetProjectsForClientAsync(
        string employeeId,
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the employee's rate for a client.
    /// </summary>
    Task<ClientRateDto?> GetClientRateAsync(
        string employeeId,
        string clientId,
        DateOnly? date = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new timesheet.
    /// </summary>
    Task<TimesheetResponse> CreateTimesheetAsync(
        TimesheetRequest request,
        bool dryRun = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing timesheet.
    /// </summary>
    Task<TimesheetResponse> UpdateTimesheetAsync(
        EditTimesheetRequest request,
        bool dryRun = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Accept a suggested timesheet.
    /// </summary>
    Task<TimesheetResponse> AcceptSuggestedTimesheetAsync(
        int suggestedTimesheetId,
        decimal? newSellPrice = null,
        string? notes = null,
        string? location = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a timesheet.
    /// </summary>
    Task<TimesheetResponse> DeleteTimesheetAsync(
        int timesheetId,
        bool dryRun = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a suggested timesheet.
    /// </summary>
    Task<TimesheetResponse> DeleteSuggestedTimesheetAsync(
        int suggestedTimesheetId,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get employee settings (start/end times).
    /// </summary>
    Task<EmployeeSettings> GetEmployeeSettingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent projects for an employee from the API.
    /// </summary>
    Task<List<RecentProjectDto>> GetRecentProjectsAsync(
        string employeeId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the TimePro API service.
/// </summary>
public class TimeProService : ITimeProService
{
    private readonly HttpClient _httpClient;
    private readonly TimeProSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private const string TimesheetApiPath = "api/Timesheets";
    private const string CrmApiPath = "Crm";

    public TimeProService(HttpClient httpClient, IOptions<TimeProSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        ConfigureHttpClient();
    }
    
    private void ConfigureHttpClient()
    {
        // Use BaseUrl directly without normalization to support custom subdomains (e.g., ssw.sswtimepro.com)
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.EndsWith('/') ? _settings.BaseUrl : _settings.BaseUrl + "/");
        _httpClient.DefaultRequestHeaders.Add("x-timepro-tenant-id", _settings.TenantId);
        _httpClient.DefaultRequestHeaders.Add("x-timepro-api-key", _settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("x-timepro-api-name", _settings.AppName);
    }

    public async Task<List<TimesheetItem>> GetTimesheetsAsync(
        string employeeId, 
        DateOnly startDate, 
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        // The API GetTimesheetListViewModel seems to fetch by single 'date' parameter.
        // Loops through the date range to fetch all timesheets.
        var allTimesheets = new List<TimesheetItem>();
        
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var url = $"{TimesheetApiPath}/GetTimesheetListViewModel?employeeID={employeeId}&date={date:yyyy-MM-dd}";
            
            try 
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var timesheets = await response.Content.ReadFromJsonAsync<List<TimesheetItem>>(_jsonOptions, cancellationToken);
                if (timesheets != null)
                {
                    allTimesheets.AddRange(timesheets);
                }
            }
            catch
            {
                // Ignore errors for individual days, strictly speaking we should probably fail but 
                // for agenda generation partial data is better than none.
            }
        }
        
        return allTimesheets;
    }
    
    public async Task<List<TimesheetItem>> GetTimesheetsByDaysAsync(
        string employeeId,
        int takeDays = 7,
        int skipDays = 0,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var startDate = today.AddDays(-skipDays - takeDays + 1);
        var endDate = today.AddDays(-skipDays);
        
        return await GetTimesheetsAsync(employeeId, startDate, endDate, cancellationToken);
    }
    
    public async Task<List<TimesheetItem>> GetSuggestedTimesheetsAsync(
        string employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        // Get timesheets for the date and filter to suggested only
        var timesheets = await GetTimesheetsAsync(employeeId, date, date, cancellationToken);
        return timesheets.Where(t => t.IsSuggested).ToList();
    }
    
    public async Task<List<AppointmentItem>> GetAppointmentsAsync(
        string employeeId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var startEpoch = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue)).ToUnixTimeSeconds();
        var endEpoch = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MaxValue)).ToUnixTimeSeconds();
        
        var url = $"{CrmApiPath}/Appointments?employeeID={employeeId}&start={startEpoch}&end={endEpoch}";
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentItem>>(_jsonOptions, cancellationToken);
        return appointments ?? [];
    }
    
    public async Task<bool> RefreshSuggestedTimesheetsAsync(
        string employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var dateStr = date.ToString("yyyy-MM-ddT00:00:00");
        var url = $"{TimesheetApiPath}/RefreshSuggestedTimesheets?employeeID={employeeId}&timesheetDate={dateStr}";
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        return response.IsSuccessStatusCode;
    }
    
    public async Task<AddTimesheetViewDto?> GetAddTimesheetViewAsync(
        string employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var url = $"{TimesheetApiPath}/GetAddTimesheetsView?empID={employeeId}&date={date:yyyy-MM-dd}";
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<AddTimesheetViewDto>(_jsonOptions, cancellationToken);
    }
    
    public async Task<List<TimesheetCategory>> GetTimesheetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"{TimesheetApiPath}/GetTimesheetCategories";
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var categories = await response.Content.ReadFromJsonAsync<List<TimesheetCategory>>(_jsonOptions, cancellationToken);
        return categories ?? [];
    }
    
    public async Task<List<TimesheetLocation>> GetTimesheetLocationsAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"{TimesheetApiPath}/GetTimesheetLocation";
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var locations = await response.Content.ReadFromJsonAsync<List<TimesheetLocation>>(_jsonOptions, cancellationToken);
        return locations ?? [];
    }
    
    public async Task<List<TimesheetBillableType>> GetTimesheetBillableTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"{TimesheetApiPath}/GetTimesheetBillableType";
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var types = await response.Content.ReadFromJsonAsync<List<TimesheetBillableType>>(_jsonOptions, cancellationToken);
        return types ?? [];
    }
    
    public async Task<List<ClientSearchResult>> SearchClientsAsync(
        string employeeId,
        string searchText,
        CancellationToken cancellationToken = default)
    {
        var url = $"{TimesheetApiPath}/GetClientListForAddTimesheet?empID={employeeId}&searchText={Uri.EscapeDataString(searchText)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var clients = await response.Content.ReadFromJsonAsync<List<ClientSearchResult>>(_jsonOptions, cancellationToken);
        return clients ?? [];
    }

    public async Task<List<ProjectForSelect>> GetProjectsForClientAsync(
        string employeeId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{TimesheetApiPath}/GetProjectsForClient?empID={employeeId}&clientID={Uri.EscapeDataString(clientId)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var projects = await response.Content.ReadFromJsonAsync<List<ProjectForSelect>>(_jsonOptions, cancellationToken);
        return projects ?? [];
    }

    public async Task<ClientRateDto?> GetClientRateAsync(
        string employeeId,
        string clientId,
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var dateStr = (date ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd");
        var url = $"{TimesheetApiPath}/GetClientRate?empID={employeeId}&clientID={Uri.EscapeDataString(clientId)}&timesheetDateCreated={dateStr}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ClientRateDto>(_jsonOptions, cancellationToken);
    }

    public async Task<TimesheetResponse> CreateTimesheetAsync(
        TimesheetRequest request,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (dryRun)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = "[DRY RUN] Timesheet would be created with the provided data.",
                TimesheetId = null
            };
        }
        
        var url = $"{TimesheetApiPath}/SaveTimesheet?isEdit=false&isSuggested=false";
        
        var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = "Timesheet created successfully."
            };
        }
        
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return new TimesheetResponse
        {
            Success = false,
            Message = $"Failed to create timesheet: {errorContent}"
        };
    }
    
    public async Task<TimesheetResponse> UpdateTimesheetAsync(
        EditTimesheetRequest request,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (dryRun)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = $"[DRY RUN] Timesheet {request.TimeId} would be updated with the provided data.",
                TimesheetId = request.TimeId
            };
        }
        
        var url = $"{TimesheetApiPath}/SaveTimesheet?isEdit=true&isSuggested=false";
        
        var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = "Timesheet updated successfully.",
                TimesheetId = request.TimeId
            };
        }
        
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return new TimesheetResponse
        {
            Success = false,
            Message = $"Failed to update timesheet: {errorContent}"
        };
    }
    
    public async Task<TimesheetResponse> AcceptSuggestedTimesheetAsync(
        int suggestedTimesheetId,
        decimal? newSellPrice = null,
        string? notes = null,
        string? location = null,
        CancellationToken cancellationToken = default)
    {
        var url = newSellPrice.HasValue
            ? $"{TimesheetApiPath}/AcceptSuggestedTimesheet?id={suggestedTimesheetId}&newSellPrice={newSellPrice.Value}"
            : $"{TimesheetApiPath}/AcceptSuggestedTimesheet?id={suggestedTimesheetId}";

        // Send modifications as request body (location and notes)
        var modifications = new { location = location ?? "", notes = notes ?? "" };
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(modifications),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = "Suggested timesheet accepted successfully.",
                TimesheetId = suggestedTimesheetId
            };
        }
        
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return new TimesheetResponse
        {
            Success = false,
            Message = $"Failed to accept suggested timesheet: {errorContent}"
        };
    }
    
    public async Task<TimesheetResponse> DeleteTimesheetAsync(
        int timesheetId,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (dryRun)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = $"[DRY RUN] Timesheet {timesheetId} would be deleted.",
                TimesheetId = timesheetId
            };
        }
        
        var url = $"{TimesheetApiPath}/DeleteTimesheet/{timesheetId}";
        
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = $"Timesheet {timesheetId} deleted successfully.",
                TimesheetId = timesheetId
            };
        }
        
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return new TimesheetResponse
        {
            Success = false,
            Message = $"Failed to delete timesheet: {errorContent}"
        };
    }
    
    public async Task<TimesheetResponse> DeleteSuggestedTimesheetAsync(
        int suggestedTimesheetId,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (dryRun)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = $"[DRY RUN] Suggested timesheet {suggestedTimesheetId} would be deleted.",
                TimesheetId = suggestedTimesheetId
            };
        }
        
        var url = $"{TimesheetApiPath}/DeleteSuggestedTimesheet/{suggestedTimesheetId}";
        
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return new TimesheetResponse
            {
                Success = true,
                Message = $"Suggested timesheet {suggestedTimesheetId} deleted successfully.",
                TimesheetId = suggestedTimesheetId
            };
        }
        
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return new TimesheetResponse
        {
            Success = false,
            Message = $"Failed to delete suggested timesheet: {errorContent}"
        };
    }
    public async Task<EmployeeSettings> GetEmployeeSettingsAsync(CancellationToken cancellationToken = default)
    {
        var url = "api/employees/getSettingsDetails";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var settings = await response.Content.ReadFromJsonAsync<EmployeeSettings>(_jsonOptions, cancellationToken);
        return settings ?? new EmployeeSettings();
    }

    public async Task<List<RecentProjectDto>> GetRecentProjectsAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/Projects/GetRecentProjects?empId={Uri.EscapeDataString(employeeId)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var projects = await response.Content.ReadFromJsonAsync<List<RecentProjectDto>>(_jsonOptions, cancellationToken);
        return projects ?? [];
    }
}
