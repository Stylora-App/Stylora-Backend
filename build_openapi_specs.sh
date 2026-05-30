#!/usr/bin/env bash
# Bundles the backend OpenAPI spec, downloads the AI worker specs from GitHub,
# and generates C# clients using NSwag CLI.
#
# Requirements:
#   - .NET SDK
#   - curl
#   - python3 + pyyaml  (pip install pyyaml)
#
# Usage:
#   ./build_openapi_specs.sh              # download AI specs from GitHub main branch
#   SPECS_REF=my-branch ./build_openapi_specs.sh  # use a different branch/commit
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
GENERATED_DIR="$SCRIPT_DIR/Stylora.Infrastructure/Generated"
DOCS_DIR="$SCRIPT_DIR/docs"
SPECS_REF="${SPECS_REF:-main}"

CLIP_SPEC_URL="https://raw.githubusercontent.com/Stylora-App/Stylora-AI/${SPECS_REF}/docs/clip-openapi.yaml"
GEMMA_SPEC_URL="https://raw.githubusercontent.com/Stylora-App/Stylora-AI/${SPECS_REF}/docs/gemma-openapi.yaml"

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

# Bundle openapi.entry.yaml + endpoints/*.yaml into a single openapi.yaml.
# Resolves path-item $refs and rewrites back-references to components.
bundle_backend_spec() {
  local entry="$1"
  local out="$2"
  python3 - "$entry" "$out" <<'PYEOF'
import sys, copy, yaml
from pathlib import Path

entry_path = Path(sys.argv[1]).resolve()
out_path   = Path(sys.argv[2]).resolve()
docs_dir   = entry_path.parent

doc = yaml.safe_load(entry_path.read_text())

def resolve_ref(ref: str, base_dir: Path):
    """Return the object pointed to by a $ref string (file#/json-pointer)."""
    if '#' in ref:
        file_part, fragment = ref.split('#', 1)
    else:
        file_part, fragment = ref, ''

    if file_part:
        target_file = (base_dir / file_part).resolve()
        target_doc  = yaml.safe_load(target_file.read_text())
        target_dir  = target_file.parent
    else:
        target_doc  = doc
        target_dir  = base_dir

    obj = target_doc
    for key in filter(None, fragment.lstrip('/').split('/')):
        obj = obj[key]

    return copy.deepcopy(obj), target_dir

def rewrite_refs(obj, endpoint_dir: Path):
    """Replace '../openapi.entry.yaml#/...' back-refs with '#/...' in-place."""
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k == '$ref' and isinstance(v, str):
                # back-reference to the entry/main file  → make it local
                for alias in ('openapi.entry.yaml', 'openapi.yaml'):
                    prefix = f'../{alias}#'
                    if v.startswith(prefix):
                        obj[k] = '#' + v[len(prefix):]
                        break
            else:
                rewrite_refs(v, endpoint_dir)
    elif isinstance(obj, list):
        for item in obj:
            rewrite_refs(item, endpoint_dir)

# Inline every path-item $ref
for path_key, path_item in list(doc.get('paths', {}).items()):
    if isinstance(path_item, dict) and '$ref' in path_item:
        ref       = path_item['$ref']
        resolved, ep_dir = resolve_ref(ref, docs_dir)
        rewrite_refs(resolved, ep_dir)
        doc['paths'][path_key] = resolved

out_path.write_text(yaml.dump(doc, allow_unicode=True, sort_keys=False, width=120))
print(f"  Bundled {entry_path.name} → {out_path.name}")
PYEOF
}

ensure_nswag() {
  if ! dotnet tool list --global | grep -qi "nswag.consoledotnet"; then
    echo "Installing NSwag CLI..."
    dotnet tool install --global NSwag.ConsoleCore
  fi
}

check_deps

# ── 1. Bundle backend spec ────────────────────────────────────────────────────

echo "==> Bundling backend spec..."
bundle_backend_spec "$DOCS_DIR/openapi.entry.yaml" "$DOCS_DIR/openapi.yaml"

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
echo "Done. Commit docs/openapi.yaml and files under Stylora.Infrastructure/Generated/ when ready."
