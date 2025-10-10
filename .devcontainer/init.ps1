param([switch]$PostCreate)

Write-Host "➡️ FlowTime consolidated init"
$dotnet = & dotnet --info 2>$null
if ($LASTEXITCODE -ne 0) {
  Write-Host "dotnet SDK not found"; exit 1
}

# Install uv if not already installed
if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
  Write-Host "Installing uv..."
  $env:CARGO_HOME = "$env:HOME/.local/share/uv-cargo"
  $env:CARGO_BIN = "$env:HOME/.local/bin"
  & bash -c "curl -LsSf https://astral.sh/uv/install.sh | env CARGO_HOME=$env:CARGO_HOME sh"
  $env:PATH = "$env:HOME/.local/bin:$env:PATH"
}

Write-Host "Restoring solution..."
& dotnet restore | Out-Null

# Install Razor/Blazor workloads if needed for UI project
Write-Host "Checking Razor workloads..."
& dotnet workload restore

if ($PostCreate) {
  Write-Host "✅ Ready. Try:"
  Write-Host "  dotnet build FlowTime.sln"
  Write-Host "  dotnet test FlowTime.sln"
  Write-Host "  dotnet run --project src/FlowTime.Sim.Service --urls http://0.0.0.0:8090"
}
