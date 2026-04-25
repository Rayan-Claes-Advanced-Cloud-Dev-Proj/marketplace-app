namespace marketplace_app.Services;

using Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CurrentUsername => _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public async Task<AuthResult> RegisterAsync(string username, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, ["Username and password are required."]);
        }

        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true,
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new AuthResult(false, result.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(user, "Candidate");
        await _signInManager.SignInAsync(user, isPersistent: false);

        return new AuthResult(true, []);
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, ["Username and password are required."]);
        }

        var user = await _userManager.FindByNameAsync(username);
        if (user is null)
        {
            return new AuthResult(false, ["Invalid username or password."]);
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                return new AuthResult(false, ["Account has been locked out."]);
            }
            return new AuthResult(false, ["Invalid username or password."]);
        }

        return new AuthResult(true, []);
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }
}
