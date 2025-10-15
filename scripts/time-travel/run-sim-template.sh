#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

usage() {
  cat <<USAGE
Usage: $(basename "$0") --template-id <id> --telemetry-dir <path> [options]

Options:
  --template-id <id>             Template identifier registered with FlowTime-Sim (required)
  --telemetry-dir <path>         Directory containing captured telemetry bundle (required)
  --out-dir <path>               Output directory for generated model/provenance (default: ./out/templates/<template>)
  --param-file <path>            Base JSON parameters file to merge before overrides
  --telemetry-param key=FILE     Map template parameter to CSV within telemetry directory (adds file:// prefix)
  --literal-param key=value      Set template parameter to a literal string value
  --json-param key=JSON          Set template parameter to raw JSON (arrays, numbers, booleans)
  --mode <mode>                  Template mode (default: telemetry)
  --verbose                      Pass verbose flag to FlowTime-Sim CLI
  --sim-cli <command>            Override FlowTime-Sim invocation (default: dotnet run --project src/FlowTime.Sim.Cli/FlowTime.Sim.Cli.csproj --)
  --help                         Show this help message

Example:
  $(basename "$0") --template-id it-system-microservices \\
    --telemetry-dir data/telemetry/run_deterministic_72ca609c \\
    --telemetry-param telemetryRequestsSource=OrderService_arrivals.csv \\
    --out-dir out/templates/it-system-microservices
USAGE
}

template_id=""
telemetry_dir=""
out_dir=""
param_file=""
mode="telemetry"
verbose="false"
custom_sim_cli=""
declare -a telemetry_params=()
declare -a literal_params=()
declare -a json_params=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --template-id)
      template_id="$2"; shift 2 ;;
    --telemetry-dir)
      telemetry_dir="$2"; shift 2 ;;
    --out-dir)
      out_dir="$2"; shift 2 ;;
    --param-file)
      param_file="$2"; shift 2 ;;
    --telemetry-param)
      telemetry_params+=("$2"); shift 2 ;;
    --literal-param)
      literal_params+=("$2"); shift 2 ;;
    --json-param)
      json_params+=("$2"); shift 2 ;;
    --mode)
      mode="$2"; shift 2 ;;
    --verbose)
      verbose="true"; shift ;;
    --sim-cli)
      custom_sim_cli="$2"; shift 2 ;;
    --help|-h)
      usage; exit 0 ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1 ;;
  esac
done

if [[ -z "$template_id" ]]; then
  echo "Error: --template-id is required" >&2
  usage
  exit 1
fi

if [[ -z "$telemetry_dir" ]]; then
  echo "Error: --telemetry-dir is required" >&2
  usage
  exit 1
fi

if [[ ! -d "$telemetry_dir" ]]; then
  echo "Error: telemetry directory not found: $telemetry_dir" >&2
  exit 1
fi

telemetry_dir="$(cd "$telemetry_dir" && pwd)"

if [[ -n "$param_file" ]]; then
  if [[ ! -f "$param_file" ]]; then
    echo "Error: parameter file not found: $param_file" >&2
    exit 1
  fi
  param_file="$(cd "$(dirname "$param_file")" && pwd)/$(basename "$param_file")"
fi

if [[ -z "$out_dir" ]]; then
  out_dir="$REPO_ROOT/out/templates/$template_id"
fi
mkdir -p "$out_dir"
out_dir="$(cd "$out_dir" && pwd)"
model_out="$out_dir/model.yaml"
provenance_out="$out_dir/provenance.json"

params_tmp="$(mktemp)"
cleanup() {
  rm -f "$params_tmp"
}
trap cleanup EXIT

resolved_literal_params=()
if ((${#literal_params[@]})); then
  resolved_literal_params+=("${literal_params[@]}")
fi

if ((${#telemetry_params[@]})); then
  for entry in "${telemetry_params[@]}"; do
    key="${entry%%=*}"
    rel="${entry#*=}"
    abs_path="$(readlink -f "$telemetry_dir/$rel" 2>/dev/null || true)"
    if [[ -z "$abs_path" || ! -f "$abs_path" ]]; then
      echo "Telemetry file not found for parameter $key: $rel" >&2
      exit 2
    fi
    resolved_literal_params+=("$key=file://$abs_path")
  done
fi

jq_cmd=(jq -n)
if [[ -n "$param_file" ]]; then
  jq_cmd+=(--slurpfile base "$param_file")
  jq_filter='($base[0] // {})'
else
  jq_filter='{}'
fi

for entry in "${resolved_literal_params[@]}"; do
  key="${entry%%=*}"
  value="${entry#*=}"
  var_name="arg_$(echo "$key" | tr '-' '_' | tr '[:upper:]' '[:lower:]')"
  jq_cmd+=(--arg "$var_name" "$value")
  jq_filter+=" | .[\"$key\"] = \$$var_name"
done

if ((${#json_params[@]})); then
  for entry in "${json_params[@]}"; do
    key="${entry%%=*}"
    value="${entry#*=}"
    var_name="json_$(echo "$key" | tr '-' '_' | tr '[:upper:]' '[:lower:]')"
    jq_cmd+=(--argjson "$var_name" "$value")
    jq_filter+=" | .[\"$key\"] = \$$var_name"
  done
fi

jq_cmd+=("$jq_filter")

"${jq_cmd[@]}" > "$params_tmp"

if [[ ! -s "$params_tmp" ]]; then
  echo "Failed to construct parameters" >&2
  exit 1
fi

echo "[sim] Writing parameters to $params_tmp"

sim_cmd=(dotnet run --project "$REPO_ROOT/src/FlowTime.Sim.Cli/FlowTime.Sim.Cli.csproj" --)
if [[ -n "$custom_sim_cli" ]]; then
  IFS=' ' read -r -a sim_cmd <<<"$custom_sim_cli"
fi

cmd=("${sim_cmd[@]}" generate --id "$template_id" --mode "$mode" --params "$params_tmp" --out "$model_out" --provenance "$provenance_out")
if [[ "$verbose" == "true" ]]; then
  cmd+=("--verbose")
fi

echo "[sim] Generating model via: ${cmd[*]}"
"${cmd[@]}"

echo
echo "Model written to: $model_out"
echo "Provenance written to: $provenance_out"
