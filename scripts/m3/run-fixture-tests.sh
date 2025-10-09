#!/bin/bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$ROOT"

echo "Running FlowTime fixture integration suite..."
dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --filter FixtureIntegrationTests
