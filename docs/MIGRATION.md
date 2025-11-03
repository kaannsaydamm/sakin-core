# Mono-Repo Migration

This document describes the migration of SAKINCore-CS from a single-project structure to a mono-repo architecture.

## Migration Date
November 2025

## Changes Made

### Directory Structure

#### Before
```
/
├── SAKINCore-CS/
│   ├── Handlers/
│   │   └── Database.cs
│   ├── Utils/
│   │   ├── PackageInspector.cs
│   │   └── TLSParser.cs
│   ├── Program.cs
│   ├── SAKINCore-CS.csproj
│   └── GlobalSuppressions.cs
└── SAKINCore-CS.sln
```

#### After
```
/
├── sakin-core/
│   └── services/
│       └── network-sensor/          (formerly SAKINCore-CS/)
│           ├── Handlers/
│           │   └── Database.cs
│           ├── Utils/
│           │   ├── PackageInspector.cs
│           │   └── TLSParser.cs
│           ├── Program.cs
│           ├── SAKINCore-CS.csproj
│           ├── GlobalSuppressions.cs
│           └── README.md
├── sakin-collectors/               (new)
├── sakin-ingest/                   (new)
├── sakin-msgbridge/                (new)
├── sakin-correlation/              (new)
├── sakin-soar/                     (new)
├── sakin-panel/                    (new)
├── sakin-utils/                    (new)
├── deployments/                    (new)
├── docs/                           (new)
└── SAKINCore-CS.sln                (updated)
```

### Files Modified

#### SAKINCore-CS.sln
Updated project path reference from:
```
"SAKINCore-CS\SAKINCore-CS.csproj"
```
to:
```
"sakin-core\services\network-sensor\SAKINCore-CS.csproj"
```

#### README.md
Completely rewritten to include:
- Mono-repo structure overview
- Component descriptions
- Updated build and installation instructions
- Directory tree visualization

### New Files Added

#### README.md files
Each top-level directory now contains a README.md with:
- Module purpose and overview
- Current status (implemented or placeholder)
- Future plans

#### Documentation
- `docs/ARCHITECTURE.md`: Comprehensive architecture documentation
- `docs/MIGRATION.md`: This migration document
- `docs/README.md`: Documentation directory overview
- `sakin-core/services/network-sensor/README.md`: Network sensor specific documentation

## Breaking Changes

### Build Path Changes
- Output binaries are now located at: `sakin-core/services/network-sensor/bin/Debug/net8.0/`
- Previously: `SAKINCore-CS/bin/Debug/net8.0/`

### Source Code Paths
- Source code is now at: `sakin-core/services/network-sensor/`
- Previously: `SAKINCore-CS/`

### No Code Changes
- No C# source code was modified
- All functionality remains identical
- Namespace remains: `SAKINCore`, `SAKINCore.Handlers`, `SAKINCore.Utils`

## Compatibility

### Building
```bash
# From repository root
dotnet restore
dotnet build

# Run network sensor
cd sakin-core/services/network-sensor
dotnet run
```

### IDE Support
- Visual Studio: Open SAKINCore-CS.sln from repository root
- Visual Studio Code: Open repository root folder
- JetBrains Rider: Open SAKINCore-CS.sln from repository root

## Future Integration

The mono-repo structure is designed to accommodate future services:

1. **sakin-collectors**: Will contain data collection agents for various sources
2. **sakin-ingest**: Data ingestion and normalization pipeline
3. **sakin-msgbridge**: Message broker for inter-service communication
4. **sakin-correlation**: Event correlation and threat detection
5. **sakin-soar**: Security orchestration and automation
6. **sakin-panel**: Web UI (currently in separate repo)
7. **sakin-utils**: Shared libraries and utilities

## Rollback Instructions

If rollback is needed:

```bash
# Move network sensor back to root
mv sakin-core/services/network-sensor SAKINCore-CS

# Remove new directories
rm -rf sakin-core sakin-collectors sakin-ingest sakin-msgbridge \
       sakin-correlation sakin-soar sakin-panel sakin-utils \
       deployments docs

# Restore original solution file
git restore SAKINCore-CS.sln README.md
```

## Testing

After migration, the following was verified:
- ✅ Solution restores successfully
- ✅ Solution builds without errors
- ✅ All source files are in correct locations
- ✅ README files present in all directories
- ✅ Documentation is comprehensive
- ✅ .gitignore is appropriate

## References

- Original project: [sakin-csharp](https://github.com/kaannsaydamm/sakin-csharp)
- Panel repository: [sakin-panel](https://github.com/kaannsaydamm/sakin-panel)
- Architecture documentation: `docs/ARCHITECTURE.md`
