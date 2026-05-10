# marketplace-app

ASP.NET Core 10.0 MVC marketplace app. Layered architecture with solution file.

## Project structure

- `Marketplace.sln` -- solution file (standard `.sln` format)
- `src/Marketplace.Data/` -- data layer: `ApplicationUser`, `ApplicationDbContext`, EF Core Identity stores
- `src/Marketplace.Business/` -- business layer: `IAuthService`, `AuthService`, `AuthResult`
- `src/Marketplace.Presentation/` -- presentation layer: MVC app, controllers, views, `Program.cs` entrypoint
- `tests/Marketplace.Tests/` -- xUnit test project (references `Marketplace.Business` and `Marketplace.Data`)

## Commands

- Build solution: `dotnet build Marketplace.sln`
- Run app: `dotnet run --project src/Marketplace.Presentation/Marketplace.Presentation.csproj`
- Build tests: `dotnet build tests/Marketplace.Tests/Marketplace.Tests.csproj`
- Run tests: `dotnet test tests/Marketplace.Tests/Marketplace.Tests.csproj`
- Run single test: `dotnet test tests/Marketplace.Tests/Marketplace.Tests.csproj --filter "FullyQualifiedName=<test-name>"`
- Coverage report: `dotnet test tests/Marketplace.Tests/Marketplace.Tests.csproj --logger:"console;verbosity=detailed" /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput="TestResults/coverage.opencover.xml" && reportgenerator -reports:"tests/Marketplace.Tests/TestResults/coverage.opencover.xml" -targetDir:"tests/Marketplace.Tests/CoverageReport"`

## Current state (as of session 2026-05-06)

- Branch: `feature/RCACDP-1-As-a-seller-I-want-to-register-and-log-in-so-that-I-can-list-items-for-sale`
- All 37 unit tests passing
- `AuthService` coverage: 100% line, 88.9% branch (11.1% untestable — `ClaimsPrincipal.Identity` is never null)
- Identity configured with "Candidate" role seeding in `src/Marketplace.Presentation/Program.cs`
- InMemory EF Core provider used for Identity store
- `Microsoft.AspNetCore.Identity` (v2.3.9) package warning NU1510 persists — unnecessary on .NET 10, consider removing
- `SendGrid` (v9.29.3) referenced in Presentation layer for email sending

## Conventions

- **TDD first** -- write tests in `tests/` before implementation code.
- **xUnit** with AAA pattern (Arrange-Act-Assert). Test project references Moq for mocking.
- **ASP.NET Core Identity** for auth. New users get "Candidate" role by default.
- **Layered architecture** -- Data (entities, DbContext) → Business (services) → Presentation (MVC app)
- **Clean Code / SOLID** -- service layer interfaces in `Marketplace.Business/`, entities in `Marketplace.Data/`.
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

- Framework: xUnit 2.9.3 + Moq 4.20.72 + coverlet 2.23.2 + ReportGenerator 5.5.9
- Mock `UserManager<ApplicationUser>` and `SignInManager<ApplicationUser>` with Moq
- `UserManager` requires `IUserStore<ApplicationUser>` mock in constructor
- `SignInManager` requires `IHttpContextAccessor` mock, `IOptions<SignInManager<>>`, and `ILogger<SignInManager<>>` mocks
- See `tests/Marketplace.Tests/Services/AuthServiceTests.cs` for mock setup pattern
- Coverage output: `tests/Marketplace.Tests/TestResults/` (raw), `tests/Marketplace.Tests/CoverageReport/` (HTML report)
