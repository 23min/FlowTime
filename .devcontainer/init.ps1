param([switch]$PostCreate)

Write-Host "➡️ FlowTime-Sim init"
$dotnet = & dotnet --info 2>$null
if ($LASTEXITCODE -ne 0) { Write-Host "dotnet SDK not found"; exit 1 }

Write-Host "Restoring solution..."
& dotnet restore | Out-Null

if ($PostCreate) {
  Write-Host "✅ Ready. Try:"
  Write-Host "  dotnet build"
  Write-Host "  dotnet test"
  Write-Host "\nTip: Open the multi-root workspace for full context:"
  Write-Host "  code flowtime-sim.code-workspace"
}
