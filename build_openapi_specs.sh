#!/usr/bin/env bash
# Downloads the AI worker OpenAPI specs from GitHub and generates C# clients
# using NSwag CLI. Also validates the backend spec.
#
# Requirements:
#   - .NET SDK
#   - curl
#   - python3 + pyyaml  (pip install pyyaml)
#
# Usage:
#   ./build_openapi_specs.sh              # download specs from GitHub main branch
#   SPECS_REF=my-branch ./build_openapi_specs.sh  # use a different branch/commit
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
GENERATED_DIR="$SCRIPT_DIR/Stylora.Infrastructure/Generated"
DOCS_DIR="$SCRIPT_DIR/docs"
SPECS_REF="${SPECS_REF:-main}"

CLIP_SPEC_URL="https://raw.githubusercontent.com/karinaconstandache/Stylora-AI/${SPECS_REF}/docs/clip-openapi.yaml"
GEMMA_SPEC_URL="https://raw.githubusercontent.com/karinaconstandache/Stylora-AI/${SPECS_REF}/docs/gemma-openapi.yaml"

CLIP_SPEC_LOCAL="/tmp/clip-openapi.yaml"
GEMMA_SPEC_LOCAL="/tmp/gemma-openapi.yaml"

# ── Helpers ──────────────────────────────────────────────────────────────────

check_deps() {
  local missing=0
  for cmd in curl python3 dotnet; do
    if ! command -v "$cmd" &>/dev/null; then
      echo "Error: '$cmd' is required but not installed." >&2
      missing=1
    fi
  done
  if ! python3 -c "import yaml" &>/dev/null; then
    echo "Error: python3 pyyaml is required. Run: pip install pyyaml" >&2
    missing=1
  fi
  [ "$missing" -eq 0 ] || exit 1
}

validate_yaml() {
  local file="$1"
  python3 -c "
import sys, yaml
try:
    doc = yaml.safe_load(open('$file'))
    if not isinstance(doc, dict) or 'openapi' not in doc or 'paths' not in doc:
        print('  missing required openapi/paths keys', file=sys.stderr)
        sys.exit(1)
except yaml.YAMLError as e:
    print(f'  YAML error: {e}', file=sys.stderr)
    sys.exit(1)
"
}

ensure_nswag() {
  if ! dotnet tool list --global | grep -qi "nswag.consoledotnet"; then
    echo "Installing NSwag CLI..."
    dotnet tool install --global NSwag.ConsoleCore
  fi
}

# ── 1. Validate backend spec ──────────────────────────────────────────────────

echo "==> Validating backend spec..."
validate_yaml "$DOCS_DIR/openapi.yaml"
echo "    OK: docs/openapi.yaml"

# ── 2. Download AI specs ──────────────────────────────────────────────────────

echo ""
echo "==> Downloading AI specs (ref: $SPECS_REF)..."
curl -fsSL "$CLIP_SPEC_URL"  -o "$CLIP_SPEC_LOCAL"
echo "    OK: clip-openapi.yaml"
curl -fsSL "$GEMMA_SPEC_URL" -o "$GEMMA_SPEC_LOCAL"
echo "    OK: gemma-openapi.yaml"

echo ""
echo "==> Validating downloaded specs..."
validate_yaml "$CLIP_SPEC_LOCAL"
echo "    OK: clip-openapi.yaml"
validate_yaml "$GEMMA_SPEC_LOCAL"
echo "    OK: gemma-openapi.yaml"

# ── 3. Generate C# clients ────────────────────────────────────────────────────

mkdir -p "$GENERATED_DIR"
ensure_nswag

echo ""
echo "==> Generating ClipAiClient..."
nswag openapi2csclient \
  /input:"$CLIP_SPEC_LOCAL" \
  /namespace:Stylora.Infrastructure.Generated \
  /className:ClipAiClient \
  /generateClientInterfaces:true \
  /injectHttpClient:true \
  /useBaseUrl:false \
  /output:"$GENERATED_DIR/ClipAiClient.g.cs"
echo "    OK: Stylora.Infrastructure/Generated/ClipAiClient.g.cs"

echo ""
echo "==> Generating GemmaAiClient..."
nswag openapi2csclient \
  /input:"$GEMMA_SPEC_LOCAL" \
  /namespace:Stylora.Infrastructure.Generated \
  /className:GemmaAiClient \
  /generateClientInterfaces:true \
  /injectHttpClient:true \
  /useBaseUrl:false \
  /output:"$GENERATED_DIR/GemmaAiClient.g.cs"
echo "    OK: Stylora.Infrastructure/Generated/GemmaAiClient.g.cs"

# ── 4. Verify the solution still builds ───────────────────────────────────────

echo ""
echo "==> Verifying build..."
dotnet build "$SCRIPT_DIR/Stylora.sln" -q
echo "    OK: build succeeded"

echo ""
echo "Done. Commit the files under Stylora.Infrastructure/Generated/ when ready."
