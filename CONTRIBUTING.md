# Contributing to S.A.K.I.N.

We welcome contributions to the S.A.K.I.N. project! This document provides guidelines and procedures for contributing code, documentation, rules, and agent implementations.

## Code of Conduct

Please be respectful and professional in all interactions. We are committed to providing a welcoming and inclusive environment.

## Getting Started

### Prerequisites
- .NET 8 SDK
- Git
- Docker & Docker Compose (for local testing)
- Node.js 18+ (for UI development)

### Development Setup

```bash
# 1. Fork and clone the repository
git clone https://github.com/YOUR_USERNAME/sakin-core.git
cd sakin-core

# 2. Create a feature branch
git checkout -b feature/your-feature-name

# 3. Set up development environment
cd deployments
docker compose -f docker-compose.dev.yml up -d

# 4. Build the solution
dotnet build SAKINCore-CS.sln

# 5. Run tests
dotnet test SAKINCore-CS.sln
```

## Types of Contributions

### Bug Fixes

1. Search existing issues to avoid duplicates
2. Create an issue describing the bug
3. Create a branch: `bugfix/issue-number-description`
4. Fix the bug with tests
5. Submit a pull request

### Feature Development

1. Discuss feature in an issue first (for major features)
2. Create a branch: `feature/feature-name`
3. Implement the feature with tests
4. Update documentation as needed
5. Submit a pull request for review

### Documentation Improvements

1. Documentation issues are always welcome
2. Create a branch: `docs/description`
3. Update relevant documentation files
4. Submit a pull request

### Rule Submissions

Contribute security detection rules:

1. Create a branch: `rule/rule-name`
2. Add rule JSON to `sakin-correlation/rules/`
3. Include tests and documentation
4. Submit a pull request with rule details

### Agent Implementations

Contribute new collectors or agents:

1. Create a branch: `agent/agent-name`
2. Implement agent in appropriate directory
3. Include configuration examples
4. Add tests and documentation
5. Submit a pull request

## Development Workflow

### Branch Naming

Use descriptive branch names:
- `feature/alert-deduplication` — New feature
- `bugfix/correlation-memory-leak` — Bug fix
- `docs/api-reference-update` — Documentation
- `rule/brute-force-detection` — Security rule
- `agent/linux-auditd` — New agent

### Commits

- Use clear, descriptive commit messages
- Reference issues when applicable: `Fixes #123`
- Keep commits focused and atomic
- Example: `Add anomaly detection service with Z-score calculation`

### Pull Requests

1. **Before submitting:**
   - Run full test suite: `dotnet test`
   - Run code formatter: `dotnet format`
   - Update CHANGELOG.md with your changes
   - Update documentation as needed

2. **PR Description:**
   ```markdown
   ## Description
   Brief description of changes
   
   ## Related Issue
   Fixes #123
   
   ## Type of Change
   - [ ] Bug fix
   - [ ] New feature
   - [ ] Documentation update
   - [ ] Rule submission
   - [ ] Agent implementation
   
   ## How Has This Been Tested?
   Describe testing approach
   
   ## Checklist
   - [ ] Tests pass locally
   - [ ] Code is formatted
   - [ ] Documentation is updated
   - [ ] CHANGELOG.md is updated
   ```

3. **Review Process:**
   - Address reviewer feedback
   - Ensure CI checks pass
   - Maintainers will merge when approved

## Code Standards

### C# Style Guide

**Naming Conventions:**
- Classes/Namespaces: `PascalCase`
- Methods/Properties: `PascalCase`
- Local variables/parameters: `camelCase`
- Private fields: `_camelCase`
- Constants: `UPPER_CASE`

**Examples:**
```csharp
public class AlertDeduplicationService
{
    private readonly IRedisClient _redis;
    private const int DEFAULT_WINDOW_SECONDS = 300;
    
    public async Task<bool> IsDuplicateAsync(Alert alert)
    {
        // Implementation
    }
    
    private string GenerateDuplicateKey(string ruleId, string hostname)
    {
        // Implementation
    }
}
```

**Best Practices:**
- Use dependency injection for services
- Use async/await for I/O operations
- Throw specific exceptions, not base `Exception`
- Document public APIs with XML comments
- Use nullable reference types (`#nullable enable`)
- Use modern C# features (records, init-only properties, patterns)

### TypeScript/React Style Guide

**File Naming:**
- Components: `PascalCase.tsx`
- Utilities: `camelCase.ts`
- Hooks: `useCamelCase.ts`

**Examples:**
```typescript
// Component
export const AlertList: React.FC<AlertListProps> = ({ alerts }) => {
  return <div>{/* Component content */}</div>;
};

// Hook
export const useAlertFiltering = (alerts: Alert[]) => {
  // Hook implementation
};

// Utility
export const formatTimestamp = (date: Date): string => {
  // Implementation
};
```

### Documentation Standards

- Use clear, concise language
- Provide code examples where applicable
- Include configuration examples
- Document error conditions and troubleshooting
- Use consistent formatting and structure
- Link to related documentation

### Rule Development Standards

**Rule Structure:**
```json
{
  "Id": "rule-unique-id",
  "Name": "Clear Rule Name",
  "Description": "Detailed description of what this rule detects",
  "Severity": "High",
  "RuleType": "Stateless|Stateful",
  "Conditions": [
    {
      "Field": "eventField",
      "Operator": "Equals|Contains|GreaterThan|etc",
      "Value": "value"
    }
  ]
}
```

**Rule Naming:**
- Use kebab-case: `rule-brute-force-ssh`
- Be specific: `rule-privilege-escalation-via-sudo`
- Include detection type: `rule-network-anomaly-port-scan`

## Testing

### Unit Tests

```bash
dotnet test tests/Sakin.Service.Tests/
```

### Integration Tests

```bash
# Start infrastructure
cd deployments
docker compose -f docker-compose.dev.yml up -d

# Run integration tests
dotnet test tests/Sakin.Integration.Tests/
```

### Test Coverage

- Aim for >80% code coverage
- Test happy paths and error cases
- Test edge cases and boundary conditions
- Use meaningful test names: `TestMethodName_Scenario_ExpectedResult`

**Example:**
```csharp
[Fact]
public async Task IsDuplicateAsync_WithinWindow_ReturnsTrueAsync()
{
    // Arrange
    var alert = new Alert { RuleId = "rule-1", Hostname = "server1" };
    
    // Act
    var result = await _service.IsDuplicateAsync(alert);
    
    // Assert
    Assert.True(result);
}
```

## Documentation Requirements

### For New Features

1. **API Documentation**: Update `/docs/api-reference.md`
2. **Configuration**: Document new config options in `/docs/configuration.md`
3. **Architecture**: Update `/docs/architecture.md` if design changes
4. **User Guide**: Add relevant guide if user-facing feature
5. **Examples**: Provide configuration or usage examples

### For New Services

1. Create `/service-name/README.md` with:
   - Overview and purpose
   - Key features
   - Architecture diagram
   - Getting started guide
   - Configuration options
   - Monitoring and troubleshooting

2. Update root `/README.md` with service mention

### For Rules

1. Document rule purpose and conditions
2. Provide example alert output
3. List trigger scenarios
4. Include false positive mitigation strategies
5. Add to `docs/rules-reference.md`

### For Agents

1. Create agent-specific README
2. Document configuration options
3. Provide deployment examples
4. List supported platforms
5. Include troubleshooting guide

## Commit Message Guidelines

Use the following format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Type:**
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Build process, dependencies, tooling

**Scope:**
- `correlation`: Correlation engine
- `ingest`: Ingest service
- `soar`: SOAR service
- `panel`: Panel API/UI
- `analytics`: Analytics services
- `docs`: Documentation
- `config`: Configuration

**Subject:**
- Use imperative mood ("add" not "added")
- Don't capitalize first letter
- No period at the end
- Max 50 characters

**Example:**
```
feat(correlation): add anomaly detection with z-score calculation

- Implement Z-score based anomaly detection service
- Add BaselineCalculatorService for statistical analysis
- Integrate with RiskScoringService for boost calculation
- Add comprehensive tests and documentation

Fixes #456
```

## Running Checks Before Submission

```bash
# 1. Format code
dotnet format SAKINCore-CS.sln

# 2. Run tests
dotnet test SAKINCore-CS.sln

# 3. Check for style issues
dotnet format --verify-no-changes SAKINCore-CS.sln

# 4. Build for production
dotnet build SAKINCore-CS.sln -c Release

# 5. Verify all services start
cd deployments
docker compose -f docker-compose.dev.yml up -d
./scripts/verify-services.sh
```

## Common Development Tasks

### Adding a New Service

1. Create service directory: `mkdir -p sakin-service/Sakin.Service`
2. Add project file: `sakin-service/Sakin.Service/Sakin.Service.csproj`
3. Implement service using WebApplication builder pattern
4. Add to `SAKINCore-CS.sln`
5. Create `/sakin-service/README.md`
6. Add Docker Compose entry in `deployments/docker-compose.dev.yml`
7. Update root `README.md` with service mention

### Adding a New Parser

1. Create parser class implementing `IParser`
2. Add to `ParserRegistry` in ingest service
3. Add unit tests
4. Update documentation

### Adding a Detection Rule

1. Create JSON rule in `sakin-correlation/rules/`
2. Add unit test for rule
3. Add documentation to `docs/rules-reference.md`
4. Test with sample events

### Adding a Playbook

1. Create JSON playbook in `sakin-soar/playbooks/`
2. Define steps (notifications, agent commands, conditions)
3. Add documentation
4. Test with sample alerts

## Getting Help

- **Questions**: Create a GitHub Discussion
- **Bugs**: Create a GitHub Issue
- **Feature Ideas**: Create a GitHub Issue with enhancement label
- **Pull Request Questions**: Comment on the PR

## Recognition

Contributors will be recognized in:
- Project CONTRIBUTORS file
- Release notes
- GitHub commit history

## License

By contributing to S.A.K.I.N., you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to S.A.K.I.N.! 🙏
