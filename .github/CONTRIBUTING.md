# Contributing to Sakin Security Platform

Thank you for considering contributing to the Sakin Security Platform!

## Development Workflow

### Prerequisites

- .NET 8.0 SDK or later
- Git
- PostgreSQL (for running the network sensor)

### Getting Started

1. Fork and clone the repository:
   ```bash
   git clone https://github.com/kaannsaydamm/sakin-core.git
   cd sakin-core
   ```

2. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. Make your changes and test locally

4. Submit a pull request

## Pre-Commit Checklist

Before committing your changes, ensure all CI checks will pass:

### 1. Restore Dependencies
```bash
dotnet restore SAKINCore-CS.sln
```

### 2. Build the Solution
```bash
dotnet build SAKINCore-CS.sln --configuration Release
```

### 3. Run All Tests
```bash
dotnet test SAKINCore-CS.sln --configuration Release
```

### 4. Format Code
```bash
# Auto-fix formatting issues
dotnet format SAKINCore-CS.sln

# Verify no formatting issues remain
dotnet format SAKINCore-CS.sln --verify-no-changes
```

### Quick Test Script

Run all checks at once:

```bash
dotnet clean SAKINCore-CS.sln && \
dotnet restore SAKINCore-CS.sln && \
dotnet build SAKINCore-CS.sln --configuration Release --no-restore && \
dotnet test SAKINCore-CS.sln --configuration Release --no-build && \
dotnet format SAKINCore-CS.sln --verify-no-changes
```

## Continuous Integration

All pull requests are automatically tested via GitHub Actions. The CI workflow:

- ✅ Builds all projects in Release configuration
- ✅ Runs all unit tests
- ✅ Verifies code formatting
- ✅ Checks for build warnings

Pull requests cannot be merged until all CI checks pass.

## Code Style

This project uses the default .NET code style enforced by `dotnet format`.

### Key Guidelines

- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Follow existing patterns in the codebase
- Keep methods focused and concise
- Write unit tests for new functionality

### Formatting

Code formatting is automatically checked by CI. Run this before committing:

```bash
dotnet format SAKINCore-CS.sln
```

## Testing

### Running Tests

```bash
# Run all tests
dotnet test SAKINCore-CS.sln

# Run tests with detailed output
dotnet test SAKINCore-CS.sln --verbosity detailed

# Run tests and generate coverage (if configured)
dotnet test SAKINCore-CS.sln --collect:"XPlat Code Coverage"
```

### Writing Tests

- Use xUnit as the test framework
- Follow the Arrange-Act-Assert pattern
- Test both success and failure scenarios
- Use meaningful test names that describe what is being tested

Example test structure:
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var sut = new SystemUnderTest();
    
    // Act
    var result = sut.Method();
    
    // Assert
    Assert.NotNull(result);
}
```

## Commit Messages

Use clear and descriptive commit messages:

```
feat: Add support for IPv6 packet parsing
fix: Correct database connection string format
docs: Update README with deployment instructions
test: Add tests for TLS parser
refactor: Simplify packet inspection logic
```

### Commit Message Format

```
<type>: <subject>

<body>

<footer>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `test`: Adding or updating tests
- `refactor`: Code refactoring
- `perf`: Performance improvement
- `chore`: Maintenance tasks

## Pull Request Process

1. Ensure all tests pass locally
2. Update documentation if needed
3. Add tests for new functionality
4. Ensure code is properly formatted
5. Submit PR with clear description
6. Wait for CI checks to complete
7. Address review feedback
8. Merge once approved

## Getting Help

- Check existing issues and documentation
- Ask questions in pull request comments
- Contact project maintainers

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
