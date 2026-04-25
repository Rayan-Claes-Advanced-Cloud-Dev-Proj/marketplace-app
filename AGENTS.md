# marketplace-app

ASP.NET Core 10.0 MVC marketplace app. No solution file -- projects are standalone.

## Project structure

- `src/marketplace-app/` -- main web app (entrypoint: `Program.cs`)
- `tests/marketplace-app.Tests/` -- xUnit test project
- No `.sln` file exists. Reference projects directly by path.

## Commands

- Build main project: `dotnet build src/marketplace-app/marketplace-app.csproj`
- Build tests: `dotnet build tests/marketplace-app.Tests/marketplace-app.Tests.csproj`
- Run tests: `dotnet test tests/marketplace-app.Tests/marketplace-app.Tests.csproj`
- Run single test: `dotnet test tests/marketplace-app.Tests/marketplace-app.Tests.csproj --filter "FullyQualifiedName=<test-name>"`

## Current state (as of session 2026-04-23)

- Branch: `feature/RCACDP-1-As-a-seller-I-want-to-register-and-log-in-so-that-I-can-list-items-for-sale`
- `AuthService.cs` has 2 compilation errors on lines 19 and 21:
  - Line 19: `GetUserNameAsync` requires a `user` argument, cannot be called without one. Use `IHttpContextAccessor` + `UserManager.GetUserIdAsync()` / `GetUserAsync()` instead.
  - Line 21: `HttpContext.Current` does not exist in ASP.NET Core. Inject `IHttpContextAccessor` and use `_httpContextAccessor.HttpContext`.
- Identity is not yet configured in `Program.cs` -- needs `AddIdentity<ApplicationUser, IdentityRole>`, `AddEntityFrameworkStores`, and `UseAuthentication`/`UseAuthorization` middleware.
- The `Microsoft.AspNetCore.Identity` (v2.3.9) package is stale and unnecessary on .NET 10 -- Identity is built-in. Consider removing it.

## Conventions

- **TDD first** -- write tests in `tests/` before implementation code.
- **xUnit** with AAA pattern (Arrange-Act-Assert). Test project references Moq for mocking.
- **ASP.NET Core Identity** for auth. New users get "Candidate" role by default.
- **Clean Code / SOLID** -- service layer interfaces in `Services/`, entities in `Entities/`.
- No emojis in code or commits unless explicitly requested.

## Course Exercises Reference

**IMPORTANT:** When developing this app, reference and implement the techniques described in the course exercises at:
https://cloud-dev-25.educ8.se/exercises/

Key exercise categories relevant to this project:
- **Webapp Development** (`/exercises/10-webapp-development/`) -- presentation layer, service layer, data layer (repository pattern, MongoDB, CosmosDB, Blob Storage), authentication/authorization, Identity
- **Deployment** (`/exercises/3-deployment/`) -- GitHub Actions CI/CD, Azure Key Vault, Azure Monitor
- **Docker** (`/exercises/20-docker/`) -- containerization, Docker Compose
- **Cloud Databases** (`/exercises/5-cloud-databases/`) -- CosmosDB, Blob Storage
- **Code Collaboration** (`/exercises/15-code-collaboration/`) -- Git workflow, PRs, Jira integration

More exercise pages will be added over time with additional techniques. Aim to implement a **majority** of the techniques across the finished app, but not necessarily all of them. Prioritize techniques that fit naturally with the current user story and architecture.

## Testing

- Framework: xUnit 2.9.3 + Moq 4.20.72
- Mock `UserManager<ApplicationUser>` and `SignInManager<ApplicationUser>` with Moq
- `UserManager` requires `IUserStore<ApplicationUser>` mock in constructor
- `SignInManager` requires `IHttpContextAccessor` mock, `IOptions<SignInManager<>>`, and `ILogger<SignInManager<>>` mocks
- See `tests/marketplace-app.Tests/Services/AuthServiceTests.cs` for mock setup pattern
