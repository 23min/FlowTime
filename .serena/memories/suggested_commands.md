# FlowTime Suggested Commands

## Build and Test
```bash
# Build solution
dotnet build

# Run tests
dotnet test --nologo

# Run specific test project
dotnet test tests/FlowTime.Core.Tests
dotnet test tests/FlowTime.API.Tests
```

## Run API
```bash
# Start API (port 8080)
export ASPNETCORE_URLS='http://0.0.0.0:8080'
dotnet run --project src/FlowTime.API

# Or use VS Code task: "start-api"
```

## Run CLI
```bash
# Example CLI run
dotnet run --project src/FlowTime.Cli -- [args]
```

## Git Commands (Linux)
```bash
# Standard git operations
git status
git add .
git commit -m "feat(scope): description"
git push origin branch-name

# Create tag
git tag -a vX.Y.Z -m "Release vX.Y.Z - description"
git push origin vX.Y.Z
```

## Process Management
```bash
# Kill API by process name (SAFE)
pkill -f 'FlowTime.API' || echo 'No API process found'

# Kill by port (SAFE)
lsof -ti:8080 | xargs -r kill -TERM

# NEVER use bare PIDs like: kill 8080
```

## File Operations (Linux)
```bash
# List files
ls -la
find . -name "*.cs"

# Search content
grep -r "pattern" .
grep -n "pattern" file.txt

# File info
wc -l file.txt
cat file.txt
head -n 20 file.txt
tail -n 20 file.txt
```
