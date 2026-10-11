#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$PROJECT_DIR/HuePilot.Mcp.csproj"
ASSEMBLY="$PROJECT_DIR/bin/Debug/net10.0/HuePilot.Mcp.dll"

# Keep build output off stdout; stdio is reserved for MCP protocol messages.
dotnet build "$PROJECT" --nologo --verbosity quiet >&2
exec dotnet "$ASSEMBLY"
