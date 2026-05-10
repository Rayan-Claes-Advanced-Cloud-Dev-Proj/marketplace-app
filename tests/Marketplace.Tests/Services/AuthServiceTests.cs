namespace Marketplace.Tests.Services;

using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Marketplace.Business;
using Marketplace.Data;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<HttpContext> _contextMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<IEmailSender<ApplicationUser>> _emailSenderMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(userStore.Object, null, null, null, null, null, null, null, null);

        _contextMock = new Mock<HttpContext>();
        var responseMock = new Mock<HttpResponse>();
        var requestMock = new Mock<HttpRequest>();
        var headersMock = new Mock<IHeaderDictionary>();

        _contextMock.Setup(c => c.Response).Returns(responseMock.Object);
        _contextMock.Setup(c => c.Request).Returns(requestMock.Object);
        _contextMock.Setup(c => c.Request.Headers).Returns(headersMock.Object);

        var claimsIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "testuser") }, "Test");
        _contextMock.Setup(c => c.User).Returns(new ClaimsPrincipal(claimsIdentity));

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(_contextMock.Object);

        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var identityOptions = Options.Create(new IdentityOptions());
        var logger = new Mock<ILogger<SignInManager<ApplicationUser>>>();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var confirmation = new Mock<IUserConfirmation<ApplicationUser>>();

        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object,
            _httpContextAccessorMock.Object,
            claimsFactory.Object,
            identityOptions,
            logger.Object,
            schemes.Object,
            confirmation.Object);

        _emailSenderMock = new Mock<IEmailSender<ApplicationUser>>();

        _authService = new AuthService(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _httpContextAccessorMock.Object,
            _emailSenderMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesUserAndReturnsSuccess()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "Password123!";

        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Candidate"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("test_token");

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        _userManagerMock.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(u => u.UserName == username && u.Email == email),
            password), Times.Once);
        _userManagerMock.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Candidate"), Times.Once);
        _emailSenderMock.Verify(
            e => e.SendConfirmationLinkAsync(
                It.Is<ApplicationUser>(u => u.UserName == username && u.Email == email),
                email,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithEmptyUsername_ReturnsFailureWithError()
    {
        // Arrange
        var username = "";
        var email = "test@example.com";
        var password = "Password123!";

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
        _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithEmptyPassword_ReturnsFailureWithError()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "";

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
        _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithNullUsername_ReturnsFailureWithError()
    {
        // Arrange
        string username = null!;
        var email = "test@example.com";
        var password = "Password123!";

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateUsername_ReturnsFailureWithErrors()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "Password123!";

        var duplicateError = new IdentityErrorDescriber().DefaultError();
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync(IdentityResult.Failed(duplicateError));

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.NotEmpty(errorList);
        _userManagerMock.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithWeakPassword_ReturnsFailureWithErrors()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "weak";

        var passwordError = new IdentityErrorDescriber().PasswordTooShort(8);
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync(IdentityResult.Failed(passwordError));

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.NotEmpty(errorList);
        _userManagerMock.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_OnSuccess_DoesNotSignInUser()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "Password123!";

        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Candidate"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("test_token");

        // Act
        await _authService.RegisterAsync(username, email, password);

        // Assert - user should NOT be signed in until email is confirmed
        _signInManagerMock.Verify(s => s.SignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var username = "testuser";
        var password = "Password123!";
        var user = new ApplicationUser { UserName = username, Email = "test@example.com", EmailConfirmed = true };

        _userManagerMock.Setup(u => u.FindByNameAsync(username))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.PasswordSignInAsync(user, password, false, false))
            .ReturnsAsync(SignInResult.Success);

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidUsername_ReturnsFailureWithError()
    {
        // Arrange
        var username = "nonexistent";
        var password = "Password123!";

        _userManagerMock.Setup(u => u.FindByNameAsync(username))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Invalid username or password", errorList[0]);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsFailureWithError()
    {
        // Arrange
        var username = "testuser";
        var password = "WrongPassword";
        var user = new ApplicationUser { UserName = username, Email = "test@example.com", EmailConfirmed = true };

        _userManagerMock.Setup(u => u.FindByNameAsync(username))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.PasswordSignInAsync(user, password, false, false))
            .ReturnsAsync(SignInResult.NotAllowed);

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Invalid username or password", errorList[0]);
    }

    [Fact]
    public async Task LoginAsync_WithEmptyUsername_ReturnsFailureWithError()
    {
        // Arrange
        string username = null!;
        var password = "Password123!";

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
        _userManagerMock.Verify(u => u.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithEmptyPassword_ReturnsFailureWithError()
    {
        // Arrange
        var username = "testuser";
        string password = null!;

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
    }

    [Fact]
    public async Task LoginAsync_WithLockedAccount_ReturnsFailureWithLockoutError()
    {
        // Arrange
        var username = "testuser";
        var password = "Password123!";
        var user = new ApplicationUser { UserName = username, Email = "test@example.com", EmailConfirmed = true };

        _userManagerMock.Setup(u => u.FindByNameAsync(username))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.PasswordSignInAsync(user, password, false, false))
            .ReturnsAsync(SignInResult.LockedOut);

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("locked out", errorList[0], System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogoutAsync_CallsSignOut()
    {
        // Arrange
        _signInManagerMock.Setup(s => s.SignOutAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _authService.LogoutAsync();

        // Assert
        _signInManagerMock.Verify(s => s.SignOutAsync(), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithWhitespaceUsername_ReturnsFailureWithError()
    {
        // Arrange
        var username = "   ";
        var email = "test@example.com";
        var password = "Password123!";

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
        _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithWhitespacePassword_ReturnsFailureWithError()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "   ";

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
        _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithMultipleIdentityErrors_ReturnsAllErrors()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "Password123!";

        var errors = new[]
        {
            new IdentityErrorDescriber().PasswordTooShort(8),
            new IdentityErrorDescriber().PasswordRequiresNonAlphanumeric(),
        };
        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Equal(2, errorList.Count);
        _userManagerMock.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithWhitespaceUsername_ReturnsFailureWithError()
    {
        // Arrange
        var username = "   ";
        var password = "Password123!";

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
        _userManagerMock.Verify(u => u.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithWhitespacePassword_ReturnsFailureWithError()
    {
        // Arrange
        var username = "testuser";
        var password = "   ";

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Username and password are required", errorList[0]);
    }

    [Fact]
    public async Task LoginAsync_WithTwoFactorEnabled_ReturnsFailureWithError()
    {
        // Arrange
        var username = "testuser";
        var password = "Password123!";
        var user = new ApplicationUser { UserName = username, Email = "test@example.com", EmailConfirmed = true };

        _userManagerMock.Setup(u => u.FindByNameAsync(username))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(s => s.PasswordSignInAsync(user, password, false, false))
            .ReturnsAsync(SignInResult.TwoFactorRequired);

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        var errorList = result.Errors.ToList();
        Assert.Single(errorList);
        Assert.Contains("Invalid username or password", errorList[0]);
    }

    [Fact]
    public void CurrentUsername_ReturnsUsername_WhenUserIsAuthenticated()
    {
        // Arrange is in constructor with ClaimTypes.Name = "testuser"

        // Act & Assert
        Assert.Equal("testuser", _authService.CurrentUsername);
    }

    [Fact]
    public void CurrentUsername_ReturnsNull_WhenHttpContextIsNull()
    {
        // Arrange
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(userStore.Object, null, null, null, null, null, null, null, null);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(h => h.HttpContext).Returns((HttpContext?)null);

        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var identityOptions = Options.Create(new IdentityOptions());
        var logger = new Mock<ILogger<SignInManager<ApplicationUser>>>();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var confirmation = new Mock<IUserConfirmation<ApplicationUser>>();
        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            httpContextAccessor.Object,
            claimsFactory.Object,
            identityOptions,
            logger.Object,
            schemes.Object,
            confirmation.Object);

        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = new AuthService(userManager.Object, signInManager.Object, httpContextAccessor.Object, emailSender.Object);

        // Act & Assert
        Assert.Null(service.CurrentUsername);
    }

    [Fact]
    public void IsAuthenticated_ReturnsTrue_WhenUserIsAuthenticated()
    {
        // Arrange is in constructor with authenticated ClaimsIdentity

        // Act & Assert
        Assert.True(_authService.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenHttpContextIsNull()
    {
        // Arrange
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(userStore.Object, null, null, null, null, null, null, null, null);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(h => h.HttpContext).Returns((HttpContext?)null);

        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var identityOptions = Options.Create(new IdentityOptions());
        var logger = new Mock<ILogger<SignInManager<ApplicationUser>>>();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var confirmation = new Mock<IUserConfirmation<ApplicationUser>>();
        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            httpContextAccessor.Object,
            claimsFactory.Object,
            identityOptions,
            logger.Object,
            schemes.Object,
            confirmation.Object);

        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = new AuthService(userManager.Object, signInManager.Object, httpContextAccessor.Object, emailSender.Object);

        // Act & Assert
        Assert.False(service.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenUserIsNotAuthenticated()
    {
        // Arrange
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(userStore.Object, null, null, null, null, null, null, null, null);

        var context = new Mock<HttpContext>();
        var responseMock = new Mock<HttpResponse>();
        var requestMock = new Mock<HttpRequest>();
        context.Setup(c => c.Response).Returns(responseMock.Object);
        context.Setup(c => c.Request).Returns(requestMock.Object);

        var anonymousIdentity = new ClaimsIdentity();
        context.Setup(c => c.User).Returns(new ClaimsPrincipal(anonymousIdentity));

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(h => h.HttpContext).Returns(context.Object);

        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var identityOptions = Options.Create(new IdentityOptions());
        var logger = new Mock<ILogger<SignInManager<ApplicationUser>>>();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        var confirmation = new Mock<IUserConfirmation<ApplicationUser>>();
        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            httpContextAccessor.Object,
            claimsFactory.Object,
            identityOptions,
            logger.Object,
            schemes.Object,
            confirmation.Object);

        var emailSender = new Mock<IEmailSender<ApplicationUser>>();
        var service = new AuthService(userManager.Object, signInManager.Object, httpContextAccessor.Object, emailSender.Object);

        // Act & Assert
        Assert.False(service.IsAuthenticated);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithValidToken_ConfirmsEmail()
    {
        // Arrange
        var userId = "user123";
        var token = "validtoken";
        var user = new ApplicationUser { Id = userId, UserName = "testuser", Email = "test@example.com" };

        _userManagerMock.Setup(u => u.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(u => u.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.ConfirmEmailAsync(userId, token);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithInvalidToken_ReturnsError()
    {
        // Arrange
        var userId = "user123";
        var token = "badtoken";
        var user = new ApplicationUser { Id = userId, UserName = "testuser", Email = "test@example.com" };

        _userManagerMock.Setup(u => u.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(u => u.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

        // Act
        var result = await _authService.ConfirmEmailAsync(userId, token);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Invalid token"));
    }

    [Fact]
    public async Task RegisterAsync_ReturnsUserIdInResult()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "Password123!";
        var expectedId = "abc123";

        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync((ApplicationUser user, string pwd) =>
            {
                user.Id = expectedId;
                return IdentityResult.Success;
            });
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Candidate"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("test_token");

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(expectedId, result.UserId);
    }

    [Fact]
    public async Task RegisterAsync_CreatesUserWithEmailNotConfirmed()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "Password123!";
        ApplicationUser? createdUser = null;

        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync((ApplicationUser user, string pwd) =>
            {
                createdUser = user;
                return IdentityResult.Success;
            });
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Candidate"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("test_token");

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(createdUser);
        Assert.False(createdUser.EmailConfirmed);
    }

    [Fact]
    public async Task LoginAsync_RejectsUserWithUnconfirmedEmail()
    {
        // Arrange
        var username = "testuser";
        var password = "Password123!";
        var user = new ApplicationUser { UserName = username, Email = "test@example.com", EmailConfirmed = false };

        _userManagerMock.Setup(u => u.FindByNameAsync(username))
            .ReturnsAsync(user);

        // Act
        var result = await _authService.LoginAsync(username, password);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("confirm"));
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithNonExistentUserId_ReturnsFailureWithError()
    {
        // Arrange
        var userId = "nonexistent";
        var token = "validtoken";

        _userManagerMock.Setup(u => u.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _authService.ConfirmEmailAsync(userId, token);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("not found"));
    }

    [Fact]
    public async Task GenerateEmailConfirmationTokenAsync_WithValidUserId_ReturnsToken()
    {
        // Arrange
        var userId = "user123";
        var expectedToken = "generated_token";
        var user = new ApplicationUser { Id = userId, UserName = "testuser", Email = "test@example.com" };

        _userManagerMock.Setup(u => u.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(user))
            .ReturnsAsync(expectedToken);

        // Act
        var token = await _authService.GenerateEmailConfirmationTokenAsync(userId);

        // Assert
        Assert.Equal(expectedToken, token);
    }

    [Fact]
    public async Task GenerateEmailConfirmationTokenAsync_WithNonExistentUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = "nonexistent";
        var token = "validtoken";

        _userManagerMock.Setup(u => u.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _authService.GenerateEmailConfirmationTokenAsync(userId));
    }

    [Fact]
    public void CurrentUsername_ReturnsNull_WhenUserIsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = null!;
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        var authService = new AuthService(_userManagerMock.Object, _signInManagerMock.Object, _httpContextAccessorMock.Object, _emailSenderMock.Object);

        // Act & Assert
        Assert.Null(authService.CurrentUsername);
    }

    [Fact]
    public void CurrentUsername_ReturnsNull_WhenIdentityNameIsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, "test@example.com") }, "cookie");
        httpContext.User = new ClaimsPrincipal(identity);
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        var authService = new AuthService(_userManagerMock.Object, _signInManagerMock.Object, _httpContextAccessorMock.Object, _emailSenderMock.Object);

        // Act & Assert
        Assert.Null(authService.CurrentUsername);
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenUserIsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = null!;
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        var authService = new AuthService(_userManagerMock.Object, _signInManagerMock.Object, _httpContextAccessorMock.Object, _emailSenderMock.Object);

        // Act & Assert
        Assert.False(authService.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalse_WhenIdentityNotAuthenticated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity();
        httpContext.User = new ClaimsPrincipal(identity);
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        var authService = new AuthService(_userManagerMock.Object, _signInManagerMock.Object, _httpContextAccessorMock.Object, _emailSenderMock.Object);

        // Act & Assert
        Assert.False(authService.IsAuthenticated);
    }
}
