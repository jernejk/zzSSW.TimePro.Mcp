using System.Text.Json;

namespace SSW.TimePro.Mcp.Server.Services.Confirmation;

/// <summary>
/// Service for managing confirmation requests.
/// Dry-run operations are saved to files and can be confirmed later.
/// </summary>
public interface IConfirmationService
{
    /// <summary>
    /// Create a pending confirmation for an operation.
    /// </summary>
    Task<PendingConfirmation> CreateConfirmationAsync(
        ConfirmationOperationType operationType,
        string description,
        string preview,
        object payload,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a pending confirmation by ID.
    /// </summary>
    Task<PendingConfirmation?> GetConfirmationAsync(
        string confirmationId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List all pending confirmations.
    /// </summary>
    Task<List<PendingConfirmation>> ListConfirmationsAsync(
        ConfirmationStatus? status = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute a confirmation and update its status.
    /// </summary>
    Task<ConfirmationResult> ExecuteConfirmationAsync(
        string confirmationId,
        Func<object?, CancellationToken, Task<object>> executeAction,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancel a pending confirmation.
    /// </summary>
    Task<bool> CancelConfirmationAsync(
        string confirmationId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clean up expired confirmations.
    /// </summary>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// File-based implementation of the confirmation service.
/// Stores confirmations in the dry-run directory.
/// </summary>
public class FileConfirmationService : IConfirmationService
{
    private readonly string _dryRunDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromHours(24);
    
    public FileConfirmationService(string? baseDirectory = null)
    {
        _dryRunDirectory = Path.Combine(
            baseDirectory ?? Directory.GetCurrentDirectory(), 
            "dry-run");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        EnsureDirectoryExists();
    }
    
    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_dryRunDirectory))
        {
            Directory.CreateDirectory(_dryRunDirectory);
        }
    }
    
    private string GetFilePath(string confirmationId) => 
        Path.Combine(_dryRunDirectory, $"{confirmationId}.json");
    
    private static string GenerateId() => 
        $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
    
    public async Task<PendingConfirmation> CreateConfirmationAsync(
        ConfirmationOperationType operationType,
        string description,
        string preview,
        object payload,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var confirmation = new PendingConfirmation
        {
            Id = GenerateId(),
            OperationType = operationType,
            Description = description,
            Preview = preview,
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiration ?? _defaultExpiration),
            Status = ConfirmationStatus.Pending
        };
        
        var filePath = GetFilePath(confirmation.Id);
        var json = JsonSerializer.Serialize(confirmation, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        
        return confirmation;
    }
    
    public async Task<PendingConfirmation?> GetConfirmationAsync(
        string confirmationId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(confirmationId);
        
        if (!File.Exists(filePath))
            return null;
        
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<PendingConfirmation>(json, _jsonOptions);
    }
    
    public async Task<List<PendingConfirmation>> ListConfirmationsAsync(
        ConfirmationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var confirmations = new List<PendingConfirmation>();
        
        if (!Directory.Exists(_dryRunDirectory))
            return confirmations;
        
        var files = Directory.GetFiles(_dryRunDirectory, "*.json");
        
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var confirmation = JsonSerializer.Deserialize<PendingConfirmation>(json, _jsonOptions);
                
                if (confirmation != null)
                {
                    // Update status if expired
                    if (confirmation.Status == ConfirmationStatus.Pending && confirmation.IsExpired)
                    {
                        confirmation.Status = ConfirmationStatus.Expired;
                        await SaveConfirmationAsync(confirmation, cancellationToken);
                    }
                    
                    if (status == null || confirmation.Status == status)
                    {
                        confirmations.Add(confirmation);
                    }
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return confirmations.OrderByDescending(c => c.CreatedAt).ToList();
    }
    
    public async Task<ConfirmationResult> ExecuteConfirmationAsync(
        string confirmationId,
        Func<object?, CancellationToken, Task<object>> executeAction,
        CancellationToken cancellationToken = default)
    {
        var confirmation = await GetConfirmationAsync(confirmationId, cancellationToken);
        
        if (confirmation == null)
        {
            return new ConfirmationResult
            {
                Success = false,
                ConfirmationId = confirmationId,
                Message = $"Confirmation '{confirmationId}' not found."
            };
        }
        
        if (confirmation.Status == ConfirmationStatus.Confirmed)
        {
            return new ConfirmationResult
            {
                Success = false,
                ConfirmationId = confirmationId,
                Message = $"Confirmation '{confirmationId}' has already been executed.",
                Result = confirmation.Result
            };
        }
        
        if (confirmation.IsExpired)
        {
            confirmation.Status = ConfirmationStatus.Expired;
            await SaveConfirmationAsync(confirmation, cancellationToken);
            
            return new ConfirmationResult
            {
                Success = false,
                ConfirmationId = confirmationId,
                Message = $"Confirmation '{confirmationId}' has expired."
            };
        }
        
        if (!confirmation.CanExecute)
        {
            return new ConfirmationResult
            {
                Success = false,
                ConfirmationId = confirmationId,
                Message = $"Confirmation '{confirmationId}' cannot be executed. Status: {confirmation.Status}"
            };
        }
        
        try
        {
            var result = await executeAction(confirmation.Payload, cancellationToken);
            
            confirmation.Status = ConfirmationStatus.Confirmed;
            confirmation.ExecutedAt = DateTime.UtcNow;
            confirmation.Result = result;
            
            await SaveConfirmationAsync(confirmation, cancellationToken);
            
            return new ConfirmationResult
            {
                Success = true,
                ConfirmationId = confirmationId,
                Message = "Operation executed successfully.",
                Result = result
            };
        }
        catch (Exception ex)
        {
            confirmation.Status = ConfirmationStatus.Failed;
            confirmation.ErrorMessage = ex.Message;
            
            await SaveConfirmationAsync(confirmation, cancellationToken);
            
            return new ConfirmationResult
            {
                Success = false,
                ConfirmationId = confirmationId,
                Message = $"Operation failed: {ex.Message}"
            };
        }
    }
    
    public async Task<bool> CancelConfirmationAsync(
        string confirmationId,
        CancellationToken cancellationToken = default)
    {
        var confirmation = await GetConfirmationAsync(confirmationId, cancellationToken);
        
        if (confirmation == null || confirmation.Status != ConfirmationStatus.Pending)
            return false;
        
        confirmation.Status = ConfirmationStatus.Cancelled;
        await SaveConfirmationAsync(confirmation, cancellationToken);
        
        return true;
    }
    
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var confirmations = await ListConfirmationsAsync(cancellationToken: cancellationToken);
        var cleaned = 0;
        
        foreach (var confirmation in confirmations)
        {
            if (confirmation.Status == ConfirmationStatus.Expired || 
                (confirmation.Status == ConfirmationStatus.Confirmed && 
                 confirmation.ExecutedAt < DateTime.UtcNow.AddDays(-7)))
            {
                var filePath = GetFilePath(confirmation.Id);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    cleaned++;
                }
            }
        }
        
        return cleaned;
    }
    
    private async Task SaveConfirmationAsync(
        PendingConfirmation confirmation,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(confirmation.Id);
        var json = JsonSerializer.Serialize(confirmation, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
