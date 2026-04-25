namespace marketplace_app.Tests.Services;

using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using marketplace_app.Services;
using marketplace_app.Entities;
using Xunit;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<HttpContext> _contextMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
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

        _authService = new AuthService(_userManagerMock.Object, _signInManagerMock.Object, _httpContextAccessorMock.Object);
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

        // Act
        var result = await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        _userManagerMock.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(u => u.UserName == username && u.Email == email),
            password), Times.Once);
        _userManagerMock.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Candidate"), Times.Once);
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
    public async Task RegisterAsync_OnSuccess_SignsInUser()
    {
        // Arrange
        var username = "testuser";
        var email = "test@example.com";
        var password = "Password123!";
        ApplicationUser? signedInUser = null;

        _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Candidate"))
            .ReturnsAsync(IdentityResult.Success);
        _signInManagerMock.Setup(s => s.SignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Callback<ApplicationUser, bool, string>((user, persistent, method) => { signedInUser = user; })
            .Returns(Task.CompletedTask);

        // Act
        await _authService.RegisterAsync(username, email, password);

        // Assert
        Assert.NotNull(signedInUser);
        _signInManagerMock.Verify(s => s.SignInAsync(It.IsAny<ApplicationUser>(), false), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var username = "testuser";
        var password = "Password123!";
        var user = new ApplicationUser { UserName = username, Email = "test@example.com" };

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
        var user = new ApplicationUser { UserName = username, Email = "test@example.com" };

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
        var user = new ApplicationUser { UserName = username, Email = "test@example.com" };

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
}
