# FlowTime Versioning Strategy

## Overview

FlowTime implements a milestone-driven versioning strategy that aligns with project development phases and ensures consistency across all projects in the solution.

## Version Format

```
<Major>.<Minor>.<Patch>[-<PreRelease>]
```

### Semantic Components

- **Patch**: Bug fixes, CLI improvements, documentation updates within milestone scope
- **Minor**: Milestone completion, new capabilities, API additions  
- **Major**: Reserved for fundamental architecture changes or major breaking changes
- **PreRelease**: `-preview`, `-rc` during development cycles

## Current Version

All projects are currently set to version **0.3.1**:

- `0.3.x` represents completion of UI-M02.01 milestone capabilities
- `.1` represents the versioning infrastructure patch addition

## Implementation Details

### Project Files

All `.csproj` files include:

```xml
<PropertyGroup>
  <VersionPrefix>0.3.1</VersionPrefix>
  <!-- other properties -->
</PropertyGroup>
```

### API Enhanced Metadata

The FlowTime.API project includes additional build-time metadata:

```xml
<!-- Embed git commit hash at build time -->
<SourceRevisionId Condition="'$(SourceRevisionId)' == ''">$([System.DateTime]::UtcNow.ToString(yyyyMMddHHmmss))</SourceRevisionId>

<Target Name="GetGitHash" BeforeTargets="GetAssemblyVersion">
  <Exec Command="git rev-parse --short HEAD" ContinueOnError="true" ConsoleToMSBuild="true" Condition="Exists('.git')">
    <Output TaskParameter="ConsoleOutput" PropertyName="GitHash" />
  </Exec>
  <PropertyGroup>
    <GitHash Condition="'$(GitHash)' == ''">unknown</GitHash>
  </PropertyGroup>
  <ItemGroup>
    <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
      <_Parameter1>GitCommitHash</_Parameter1>
      <_Parameter2>$(GitHash)</_Parameter2>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
      <_Parameter1>BuildTime</_Parameter1>
      <_Parameter2>$([System.DateTime]::UtcNow.ToString(yyyy-MM-ddTHH:mm:ssZ))</_Parameter2>
    </AssemblyAttribute>
  </ItemGroup>
</Target>
```

### Version Resolution

The API ServiceInfoProvider automatically resolves version information:

1. **Version**: From assembly metadata (VersionPrefix)
2. **Commit Hash**: From embedded assembly metadata or environment variable
3. **Build Time**: From embedded assembly metadata or file timestamp

## Version Management Workflow

### Pre-merge Review

Before merging to main, evaluate:

- **Does this complete a milestone?** → Minor bump
- **Is this a bug fix or improvement within current milestone?** → Patch bump  
- **Does this break existing APIs or fundamentally change architecture?** → Major bump
- **Is this work-in-progress toward next milestone?** → PreRelease suffix

### Updating Versions

To update the version across all projects:

1. Update `<VersionPrefix>` in all `.csproj` files
2. Ensure consistency across all 10 project files:
   - `src/FlowTime.Core/FlowTime.Core.csproj`
   - `src/FlowTime.Cli/FlowTime.Cli.csproj`
   - `src/FlowTime.API/FlowTime.API.csproj`
   - `src/FlowTime.Adapters.Synthetic/FlowTime.Adapters.Synthetic.csproj`
   - `ui/FlowTime.UI/FlowTime.UI.csproj`
   - `tests/FlowTime.Tests/FlowTime.Tests.csproj`
   - `tests/FlowTime.Cli.Tests/FlowTime.Cli.Tests.csproj`
   - `tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj`
   - `tests/FlowTime.Adapters.Synthetic.Tests/FlowTime.Adapters.Synthetic.Tests.csproj`
   - `ui/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj`

### No Hardcoded Automation

Version decisions are made during merge review, not automated based on branch names. This ensures thoughtful version management aligned with actual capability delivery.

## Runtime Version Information

### API Endpoints

Version information is available through:

- `GET /v1/healthz` - Comprehensive service information including version, commit hash, and build time
- `GET /healthz` - Basic health check

### Assembly Inspection

All assemblies include standard .NET assembly version information accessible via:

```csharp
var version = Assembly.GetExecutingAssembly().GetName().Version;
```

## Benefits

1. **Consistency**: All projects maintain the same version
2. **Traceability**: Git commit hash embedded for deployment tracking
3. **Milestone Alignment**: Version numbers reflect actual capability milestones
4. **Operational Visibility**: Runtime version information available via API
5. **Build Reproducibility**: Build time and commit metadata for debugging

## Inspiration

This implementation follows the proven pattern from FlowTime-Sim project, ensuring consistency across the FlowTime ecosystem.
