# FlowTime-Sim CLI Development Guide

## Building the CLI

### Build from source

```bash
# Build just the CLI project
cd /workspaces/flowtime-sim-vnext
dotnet build src/FlowTime.Sim.Cli

# Or build the entire solution
dotnet build
```

The build output goes to:
```
src/FlowTime.Sim.Cli/bin/Debug/net9.0/
  ├── flow-sim              # Native executable (Linux/macOS)
  ├── flow-sim.dll          # .NET assembly
  ├── flow-sim.exe          # Native executable (Windows, if built on Windows)
  └── FlowTime.Sim.Core.dll # Dependencies
```

## Running the CLI

### Option 1: Using `dotnet run` (Recommended for Development)

```bash
cd /workspaces/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Cli -- list templates
dotnet run --project src/FlowTime.Sim.Cli -- show template --id transportation-basic
dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic --out model.yaml
```

**Pros:**
- ✅ Automatically rebuilds if source changed
- ✅ Works from repository root (finds templates automatically)
- ✅ No need to worry about paths

**Cons:**
- ⚠️ Slower startup (build check + JIT)

### Option 2: Running the Native Executable

```bash
# FROM REPOSITORY ROOT (recommended)
cd /workspaces/flowtime-sim-vnext
./src/FlowTime.Sim.Cli/bin/Debug/net9.0/flow-sim list templates

# OR from bin directory with --templates-dir
cd src/FlowTime.Sim.Cli/bin/Debug/net9.0
./flow-sim list templates --templates-dir /workspaces/flowtime-sim-vnext/templates
```

**Pros:**
- ✅ Faster startup
- ✅ Closer to production usage

**Cons:**
- ⚠️ Must specify `./flow-sim` (not just `flow-sim`) in PowerShell/bash
- ⚠️ Need to rebuild manually after code changes
- ⚠️ Must manage working directory or use `--templates-dir`

### Option 3: Using `dotnet` with the DLL

```bash
cd /workspaces/flowtime-sim-vnext
dotnet src/FlowTime.Sim.Cli/bin/Debug/net9.0/flow-sim.dll list templates
```

**Pros:**
- ✅ Cross-platform
- ✅ No rebuild needed if already built

**Cons:**
- ⚠️ More verbose
- ⚠️ Working directory matters for finding templates

### Option 4: Create an Alias (Convenience)

**For bash/zsh:**
```bash
alias flow-sim="dotnet run --project /workspaces/flowtime-sim-vnext/src/FlowTime.Sim.Cli --"

# Then use anywhere:
flow-sim list templates
flow-sim show template --id transportation-basic
```

**For PowerShell:**
```powershell
function flow-sim { dotnet run --project /workspaces/flowtime-sim-vnext/src/FlowTime.Sim.Cli -- @args }

# Then use anywhere:
flow-sim list templates
flow-sim show template --id transportation-basic
```

Add to your shell profile (`.bashrc`, `.zshrc`, or PowerShell profile) to make permanent.

## Understanding Template Discovery

The CLI looks for templates in `./templates` **relative to the current working directory**:

```csharp
// From Program.cs
var templatesDir = opts.TemplatesDir ?? Path.Combine(Directory.GetCurrentDirectory(), "templates");
```

### Where Templates Are Found

| Working Directory | Default Templates Path | Works? |
|------------------|------------------------|--------|
| `/workspaces/flowtime-sim-vnext` | `./templates` → `/workspaces/flowtime-sim-vnext/templates` | ✅ YES |
| `/workspaces/flowtime-sim-vnext/src/FlowTime.Sim.Cli/bin/Debug/net9.0` | `./templates` → `.../bin/Debug/net9.0/templates` | ❌ NO (doesn't exist) |
| Anywhere | `--templates-dir /workspaces/flowtime-sim-vnext/templates` | ✅ YES (explicit path) |

### Solutions for Template Discovery

**1. Run from repository root** (easiest):
```bash
cd /workspaces/flowtime-sim-vnext
./src/FlowTime.Sim.Cli/bin/Debug/net9.0/flow-sim list templates
```

**2. Use `--templates-dir` option**:
```bash
./flow-sim list templates --templates-dir /workspaces/flowtime-sim-vnext/templates
```

**3. Create symlink in bin directory** (for testing as if installed):
```bash
cd /workspaces/flowtime-sim-vnext/src/FlowTime.Sim.Cli/bin/Debug/net9.0
ln -s /workspaces/flowtime-sim-vnext/templates templates
./flow-sim list templates  # Now works from bin directory
```

**4. Use `dotnet run`** (automatic):
```bash
cd /workspaces/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Cli -- list templates  # Always finds templates
```

## Testing the CLI

### Run all tests
```bash
cd /workspaces/flowtime-sim-vnext
dotnet test
```

### Run just CLI-related tests
```bash
dotnet test --filter "FullyQualifiedName~ArgParser"
```

### Manual testing checklist
```bash
# List operations
flow-sim list templates
flow-sim list models

# Show operations
flow-sim show template --id transportation-basic
flow-sim show template --id it-system-microservices --format json

# Generate operations
flow-sim generate --id transportation-basic --out test-model.yaml
flow-sim generate --id transportation-basic  # stdout
cat > overrides.json << 'EOF'
{"bins": 24, "demandPattern": [20, 30, 40, 35, 25, 15]}
EOF
flow-sim generate --id transportation-basic --params overrides.json --out model-custom.yaml

# Validate operations
flow-sim validate --id transportation-basic
flow-sim validate --id transportation-basic --params overrides.json

# List generated models
flow-sim list models
```

## Debugging

### VS Code Launch Configuration

The repository includes launch configurations in `.vscode/launch.json`:

```json
{
  "name": ".NET Launch FlowTime.Cli (hello)",
  "type": "coreclr",
  "request": "launch",
  "program": "${workspaceFolder}/src/FlowTime.Sim.Cli/bin/Debug/net9.0/flow-sim.dll",
  "args": ["list", "templates"],
  "cwd": "${workspaceFolder}",
  "stopAtEntry": false
}
```

**To debug:**
1. Open `src/FlowTime.Sim.Cli/Program.cs`
2. Set breakpoints
3. Press F5 or select "Run > Start Debugging"
4. Or use the Debug panel and select ".NET Launch FlowTime.Cli (hello)"

### Manual debugging with `dotnet run`

```bash
# Add verbose output
dotnet run --project src/FlowTime.Sim.Cli -- list templates --verbose

# Use environment variables for .NET debugging
export DOTNET_ENVIRONMENT=Development
dotnet run --project src/FlowTime.Sim.Cli -- list templates
```

## Common Issues

### Issue: "flow-sim: command not found" or "The term 'flow-sim' is not recognized"

**Cause:** Trying to run executable without path prefix

**Solution:**
```bash
# Wrong:
flow-sim list templates

# Right (PowerShell/bash):
./flow-sim list templates

# Or use full path:
/workspaces/flowtime-sim-vnext/src/FlowTime.Sim.Cli/bin/Debug/net9.0/flow-sim list templates
```

### Issue: "Templates directory not found"

**Cause:** Running from wrong working directory

**Solutions:**
```bash
# Option A: Run from repo root
cd /workspaces/flowtime-sim-vnext
./src/FlowTime.Sim.Cli/bin/Debug/net9.0/flow-sim list templates

# Option B: Specify templates directory
./flow-sim list templates --templates-dir /workspaces/flowtime-sim-vnext/templates

# Option C: Use dotnet run (handles it automatically)
cd /workspaces/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Cli -- list templates
```

### Issue: Changes not reflected in executable

**Cause:** Forgot to rebuild after code changes

**Solution:**
```bash
# Rebuild
dotnet build src/FlowTime.Sim.Cli

# Or use dotnet run (rebuilds automatically)
dotnet run --project src/FlowTime.Sim.Cli -- list templates
```

## Publishing (Creating Standalone Executable)

### For local testing

```bash
# Publish for your current platform
dotnet publish src/FlowTime.Sim.Cli -c Release -o ./publish

# Run published version
./publish/flow-sim list templates --templates-dir ./templates
```

### For distribution

```bash
# Self-contained executable (no .NET runtime needed)
dotnet publish src/FlowTime.Sim.Cli -c Release -r linux-x64 --self-contained -o ./dist/linux-x64
dotnet publish src/FlowTime.Sim.Cli -c Release -r win-x64 --self-contained -o ./dist/win-x64
dotnet publish src/FlowTime.Sim.Cli -c Release -r osx-arm64 --self-contained -o ./dist/osx-arm64

# Framework-dependent (smaller, requires .NET runtime)
dotnet publish src/FlowTime.Sim.Cli -c Release -r linux-x64 -o ./dist/linux-x64-fd
```

## Project Structure

```
src/FlowTime.Sim.Cli/
├── Program.cs              # Main entry point, CLI routing
├── FlowTime.Sim.Cli.csproj # Project file
└── bin/Debug/net9.0/       # Build output
    ├── flow-sim            # Native executable
    └── flow-sim.dll        # .NET assembly

templates/                  # Template files (YAML)
├── transportation-basic.yaml
├── it-system-microservices.yaml
└── ...

tests/FlowTime.Sim.Tests/
└── ArgParserTests.cs       # CLI argument parser tests
```

## CI/CD Considerations

### Build script example

```bash
#!/bin/bash
# scripts/build-cli.sh

set -e

echo "Building FlowTime-Sim CLI..."
dotnet build src/FlowTime.Sim.Cli -c Release

echo "Running tests..."
dotnet test --filter "FullyQualifiedName~ArgParser"

echo "Publishing for Linux..."
dotnet publish src/FlowTime.Sim.Cli -c Release -r linux-x64 -o ./artifacts/linux-x64

echo "Publishing for Windows..."
dotnet publish src/FlowTime.Sim.Cli -c Release -r win-x64 -o ./artifacts/win-x64

echo "Build complete. Artifacts in ./artifacts/"
```

### GitHub Actions workflow example

```yaml
name: Build CLI

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Build
        run: dotnet build src/FlowTime.Sim.Cli
      
      - name: Test
        run: dotnet test --filter "FullyQualifiedName~ArgParser"
      
      - name: Publish
        run: dotnet publish src/FlowTime.Sim.Cli -c Release -r linux-x64 -o ./artifacts
      
      - name: Upload artifacts
        uses: actions/upload-artifact@v3
        with:
          name: flow-sim-cli
          path: ./artifacts/flow-sim
```

## Best Practices for Development

1. **Use `dotnet run` during active development** - automatic rebuilds
2. **Run from repository root** - avoids template path issues
3. **Create shell aliases** - convenience without sacrificing flexibility
4. **Test both ways** - `dotnet run` and native executable
5. **Use `--verbose` for debugging** - see what's happening
6. **Keep tests updated** - especially `ArgParserTests.cs` for CLI changes

## See Also

- [CLI Usage Guide](./usage.md) - User-facing documentation
- [Verb+Noun Refactoring](./verb-noun-refactoring.md) - CLI design decisions
- [Testing Documentation](../testing.md) - Testing strategy
