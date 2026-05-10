using Marketplace.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Marketplace.Business;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEmailSender<ApplicationUser> _emailSender;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IHttpContextAccessor httpContextAccessor,
        IEmailSender<ApplicationUser> emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _httpContextAccessor = httpContextAccessor;
        _emailSender = emailSender;
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
            EmailConfirmed = false,
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new AuthResult(false, result.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(user, "Candidate");

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _emailSender.SendConfirmationLinkAsync(user, user.Email!, token);

        return new AuthResult(true, [], user.Id);
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

        if (!user.EmailConfirmed)
        {
            return new AuthResult(false, ["Please confirm your email before logging in."]);
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

    public async Task<AuthResult> ConfirmEmailAsync(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new AuthResult(false, ["User not found."]);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return new AuthResult(false, result.Errors.Select(e => e.Description));
        }

        return new AuthResult(true, []);
    }

    public async Task<string> GenerateEmailConfirmationTokenAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new ArgumentException("User not found.");
        }

        return await _userManager.GenerateEmailConfirmationTokenAsync(user);
    }
}
