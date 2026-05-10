namespace Marketplace.Business;

public record AuthResult(bool Succeeded, IEnumerable<string> Errors, string? UserId = null);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string username, string email, string password);
    Task<AuthResult> LoginAsync(string username, string password);
    Task LogoutAsync();
    Task<AuthResult> ConfirmEmailAsync(string userId, string token);
    Task<string> GenerateEmailConfirmationTokenAsync(string userId);
    string? CurrentUsername { get; }
    bool IsAuthenticated { get; }
}
