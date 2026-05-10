using Marketplace.Business;
using Marketplace.Presentation.Models;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Presentation.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    private async Task<string> GetUserIdAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return userId ?? throw new InvalidOperationException("User ID not found.");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            return View(model);
        }

        var result = await _authService.RegisterAsync(model.Username, model.Email, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
            return View(model);
        }

        TempData["UserId"] = result.UserId;
        return RedirectToAction("RegistrationConfirmed");
    }

    [HttpGet]
    public async Task<IActionResult> RegistrationConfirmed()
    {
        if (TempData.Peek("UserId") is not string userId)
        {
            return RedirectToAction("Register");
        }
        var token = await _authService.GenerateEmailConfirmationTokenAsync(userId);

        return View(new ConfirmEmailViewModel
        {
            UserId = userId,
            Token = token,
        });
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        var result = await _authService.ConfirmEmailAsync(userId, token);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
            return View("ConfirmEmailError");
        }

        return View("ConfirmEmailSuccess");
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("ConfirmEmailError", model);
        }

        var result = await _authService.ConfirmEmailAsync(model.UserId, model.Token);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
            return View("ConfirmEmailError", model);
        }

        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.LoginAsync(model.Username, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
            return View(model);
        }

        return RedirectToAction("Dashboard", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return RedirectToAction("Index", "Home");
    }
}
