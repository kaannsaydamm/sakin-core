# GitHub Actions CI/CD Workflows

## CI Workflow

The `ci.yml` workflow provides continuous integration for the Sakin Security Platform.

### Triggers

The workflow runs on:
- **Push** to `main`, `develop`, `feature/**`, or `bugfix/**` branches
- **Pull requests** targeting `main` or `develop` branches

### Steps

1. **Checkout code** - Uses `actions/checkout@v4`
2. **Setup .NET 8 SDK** - Uses `actions/setup-dotnet@v4`
3. **Cache NuGet packages** - Caches `~/.nuget/packages` based on `*.csproj` hash
4. **Restore dependencies** - Runs `dotnet restore` on the solution
5. **Build solution** - Builds all projects in Release configuration
6. **Run unit tests** - Executes all test projects with TRX logging
7. **Upload test results** - Saves test results as artifacts (runs even on failure)
8. **Check code formatting** - Verifies code formatting with `dotnet format`

### Workflow Features

- ✅ Multi-project solution support
- ✅ NuGet package caching for faster builds
- ✅ Test results uploaded as artifacts
- ✅ Code formatting verification
- ✅ Build and test failures block merge

### Local Development

Before pushing code, ensure your changes pass all checks:

```bash
# Restore dependencies
dotnet restore SAKINCore-CS.sln

# Build the solution
dotnet build SAKINCore-CS.sln --configuration Release

# Run tests
dotnet test SAKINCore-CS.sln --configuration Release

# Check formatting (and auto-fix if needed)
dotnet format SAKINCore-CS.sln
```

### Badges

CI status badges are displayed in the main README:
- **CI Status**: Shows if the latest build passed or failed
- **.NET Version**: Indicates the .NET SDK version in use
- **License**: Shows the project license

### Troubleshooting

**Build fails on CI but works locally?**
- Ensure you're using .NET 8 SDK
- Try `dotnet clean` followed by `dotnet restore`
- Check for uncommitted changes

**Format check fails?**
- Run `dotnet format SAKINCore-CS.sln` locally to auto-fix formatting
- Commit the formatting changes

**Tests fail on CI?**
- Tests run in Release configuration on CI
- Ensure tests don't depend on local configuration or files
- Check test output in the uploaded artifacts

### Branch Protection

To enforce CI checks before merging, configure branch protection rules in GitHub:
1. Go to Settings → Branches → Branch protection rules
2. Add rule for `main` and `develop` branches
3. Enable "Require status checks to pass before merging"
4. Select "Build and Test" as required status check
