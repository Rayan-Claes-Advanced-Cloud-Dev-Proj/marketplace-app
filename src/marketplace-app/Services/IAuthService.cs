namespace marketplace_app.Services;

public record AuthResult(bool Succeeded, IEnumerable<string> Errors);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string username, string email, string password);
    Task<AuthResult> LoginAsync(string username, string password);
    Task LogoutAsync();
    string? CurrentUsername { get; }
    bool IsAuthenticated { get; }
}
