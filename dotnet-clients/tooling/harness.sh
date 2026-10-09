#!/usr/bin/env bash
# dotnet-clients local harness. Run from the repo root.
#
# Usage:
#   ./dotnet-clients/tooling/harness.sh
#   ./dotnet-clients/tooling/harness.sh --checks restore,fmt,ast-grep,resx-path,build
#   ./dotnet-clients/tooling/harness.sh --exec dotnet test Voucha.DotNet.sln
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ROOT_DIR="$(cd "$DOTNET_DIR/.." && pwd)"
SOLUTION="$DOTNET_DIR/Voucha.DotNet.sln"
DOTNET_TEMP_ROOT="${VOUCHA_DOTNET_TEMP_ROOT:-${TMPDIR:-/tmp}/voucha-dotnet-clients-$(whoami)}"
DOTNET_CACHE_ROOT="${VOUCHA_DOTNET_CACHE_ROOT:-${XDG_CACHE_HOME:-$HOME/.cache}/voucha/dotnet}"

while IFS='=' read -r inherited_name _; do
  normalized_name="$(printf '%s' "$inherited_name" | tr '[:upper:]' '[:lower:]')"
  case "$normalized_name" in
    artifactspath | baseintermediateoutputpath | baseoutputpath | msbuildprojectextensionspath | \
      nuget_http_cache_path | nuget_packages | nuget_plugins_cache_path | nuget_scratch | \
      restorepackagespath | useartifactsoutput | voucha_dotnet_cache_root | voucha_dotnet_temp_root)
      unset "$inherited_name"
      ;;
  esac
done < <(env)

export DOTNET_NOLOGO="${DOTNET_NOLOGO:-1}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}"
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export MSBUILDDISABLENODEREUSE=1
export NUGET_PACKAGES="$DOTNET_CACHE_ROOT/packages"
export NUGET_HTTP_CACHE_PATH="$DOTNET_CACHE_ROOT/http-cache"
export NUGET_PLUGINS_CACHE_PATH="$DOTNET_CACHE_ROOT/plugins-cache"
export NUGET_SCRATCH="$DOTNET_TEMP_ROOT/scratch"
export UseArtifactsOutput=false
export VOUCHA_DOTNET_CACHE_ROOT="$DOTNET_CACHE_ROOT"
export VOUCHA_DOTNET_TEMP_ROOT="$DOTNET_TEMP_ROOT"

MODE='checks'
EXEC_COMMAND=()
ALL_CHECKS=(restore fmt ast-grep resx-path build)
CHECKS=("${ALL_CHECKS[@]}")

if [[ "${1:-}" == "--exec" ]]; then
  MODE='exec'
  shift
  if [[ $# -eq 0 ]]; then
    echo "Error: --exec requires a command" >&2
    exit 1
  fi
  EXEC_COMMAND=("$@")
else
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --checks)
        if [[ $# -lt 2 ]]; then
          echo "Error: --checks requires an argument" >&2
          exit 1
        fi
        IFS=',' read -ra requested_checks <<< "$2"
        for requested_check in "${requested_checks[@]}"; do
          case " ${ALL_CHECKS[*]} " in
            *" $requested_check "*) ;;
            *) echo "Unknown check: $requested_check" >&2; exit 1 ;;
          esac
        done
        CHECKS=("${requested_checks[@]}")
        shift 2
        ;;
      *)
        echo "Unknown argument: $1" >&2
        exit 1
        ;;
    esac
  done
fi

contains() {
  local needle="$1"
  local item
  for item in "${CHECKS[@]}"; do [[ "$item" == "$needle" ]] && return 0; done
  return 1
}

print_dotnet_install_policy() {
  echo 'Install mise and the SDK required by the repository root .mise.toml/global.json policy, then run this harness through mise exec. See dotnet-clients/README.md.' >&2
}

resolve_dotnet_host_path() {
  local host="$1"
  local resolved=''
  local host_name host_parent host_dir
  if command -v realpath >/dev/null 2>&1; then
    resolved="$(realpath "$host" 2>/dev/null)" || resolved=''
  fi
  if [[ -z "$resolved" ]] && command -v python3 >/dev/null 2>&1; then
    resolved="$(python3 -c 'import os, sys; print(os.path.realpath(sys.argv[1]))' "$host")" || resolved=''
  fi
  if [[ -z "$resolved" ]]; then
    host_name="${host##*/}"
    host_parent="${host%/*}"
    [[ "$host_parent" == "$host" ]] && host_parent='.'
    host_dir="$(cd -P -- "$host_parent" && pwd)" || return 1
    resolved="$host_dir/$host_name"
  fi
  is_executable_file "$resolved" || return 1
  printf '%s\n' "$resolved"
}

is_executable_file() {
  [[ -f "$1" && -x "$1" ]]
}

VOUCHA_DOTNET_HOST=''
needs_dotnet=false
if [[ "$MODE" == 'exec' ]] || contains restore || contains fmt || contains resx-path || contains build; then
  needs_dotnet=true
fi

if [[ "$needs_dotnet" == true ]]; then
  if ! command -v mise >/dev/null 2>&1; then
    echo 'Error: mise is required to select the repository .NET SDK.' >&2
    print_dotnet_install_policy
    exit 127
  fi

  selected_root="$(cd "$ROOT_DIR" && mise where dotnet 2>/dev/null)" || {
    echo 'Error: the repository mise configuration has no installed .NET SDK.' >&2
    print_dotnet_install_policy
    exit 127
  }
  selected_root="$(cd -P -- "$selected_root" && pwd)" || {
    echo 'Error: mise returned an invalid .NET SDK installation directory.' >&2
    exit 1
  }
  selected_host="$(cd "$ROOT_DIR" && mise which dotnet 2>/dev/null)" || {
    echo 'Error: mise could not resolve the repository .NET SDK host.' >&2
    print_dotnet_install_policy
    exit 127
  }
  selected_host="$(resolve_dotnet_host_path "$selected_host")" || {
    echo 'Error: mise resolved a missing or non-executable .NET host.' >&2
    exit 127
  }
  if [[ "$selected_host" != "$selected_root/dotnet" ]]; then
    echo "Error: mise selected .NET outside its pinned installation root: $selected_host" >&2
    exit 1
  fi

  expected_version="$(node -p 'JSON.parse(require("fs").readFileSync(process.argv[1], "utf8")).sdk.version' "$ROOT_DIR/global.json")"
  actual_version="$(cd "$ROOT_DIR" && mise exec -- dotnet --version)" || {
    echo 'Error: mise could not run the repository .NET SDK.' >&2
    exit 127
  }
  if [[ "$actual_version" != "$expected_version" ]]; then
    echo "Error: mise selected .NET SDK $actual_version; global.json requires $expected_version." >&2
    print_dotnet_install_policy
    exit 1
  fi

  VOUCHA_DOTNET_HOST="$selected_host"
  export VOUCHA_DOTNET_HOST DOTNET_ROOT="$selected_root"
  export PATH="$selected_root:$PATH"
fi

if [[ "$MODE" == 'exec' ]]; then
  if [[ "${EXEC_COMMAND[0]}" == 'dotnet' ]]; then
    EXEC_COMMAND[0]="$VOUCHA_DOTNET_HOST"
  fi
  cd "$DOTNET_DIR"
  exec "${EXEC_COMMAND[@]}"
fi

cd "$DOTNET_DIR"

PASS=0
FAIL=0
SKIPPED=0
CHECK_NAMES=()
CHECK_STATUSES=()
CHECK_ELAPSED_SECONDS=()
CHECK_CLASSIFICATIONS=()
CHECK_OUTPUT_FILES=()

cleanup_result_files() {
  local output_file
  for output_file in "${CHECK_OUTPUT_FILES[@]:-}"; do
    [[ -n "$output_file" ]] || continue
    rm -f -- "$output_file" || true
  done
}

trap cleanup_result_files EXIT

classify_failure() {
  local status="$1"
  local output_file="$2"

  if [[ "$status" -eq 124 ]] && grep -Eq '^with-host-lock: (host-package-manager|expensive-build) command exceeded [0-9]*[1-9][0-9]*s; terminating its process group$' "$output_file"; then
    printf '%s\n' 'host-timeout'
  elif [[ "$status" -eq 1 ]] && grep -Eq '^with-host-lock: (host-package-manager|expensive-build) lock not acquired within [0-9]*[1-9][0-9]*s$' "$output_file"; then
    printf '%s\n' 'lock-timeout'
  else
    printf '%s\n' 'check-failure'
  fi
}

record_result() {
  CHECK_NAMES+=("$1")
  CHECK_STATUSES+=("$2")
  CHECK_ELAPSED_SECONDS+=("$3")
  CHECK_CLASSIFICATIONS+=("$4")
}

terminate_output_line() {
  local output_file="$1"
  local last_byte

  [[ -s "$output_file" ]] || return 0
  last_byte="$(tail -c 1 "$output_file" | od -An -t x1)"
  [[ "$last_byte" == *0a* ]] || printf '\n'
}

run_check() {
  local name="$1"
  local output_file
  local start_seconds
  local elapsed_seconds
  local status
  local classification
  shift

  output_file="$(mktemp "${TMPDIR:-/tmp}/voucha-dotnet-harness.XXXXXX")" || {
    echo 'Error: cannot create .NET harness result file' >&2
    exit 2
  }
  CHECK_OUTPUT_FILES+=("$output_file")
  start_seconds=$SECONDS
  printf "  %-12s " "$name"
  if "$@" >"$output_file" 2>&1; then
    status=0
    elapsed_seconds=$((SECONDS - start_seconds))
    echo "ok"
    PASS=$((PASS + 1))
  else
    status=$?
    elapsed_seconds=$((SECONDS - start_seconds))
    echo "fail"
    cat "$output_file" || {
      echo 'Error: cannot replay .NET harness check output' >&2
      exit 2
    }
    terminate_output_line "$output_file"
    FAIL=$((FAIL + 1))
  fi
  if [[ "$status" -eq 0 ]]; then
    classification='passed'
  else
    classification="$(classify_failure "$status" "$output_file")"
  fi
  record_result "$name" "$status" "$elapsed_seconds" "$classification"
}

run_in_directory() {
  local directory="$1"
  shift
  (cd -- "$directory" && "$@")
}

print_failure_summary() {
  local index
  local output_file

  [[ "$FAIL" -gt 0 ]] || return 0

  echo 'failed checks:'
  echo 'check | classification | exit | elapsed'
  for index in "${!CHECK_NAMES[@]}"; do
    [[ "${CHECK_STATUSES[index]}" -ne 0 ]] || continue
    printf '%s | %s | %s | %ss\n' \
      "${CHECK_NAMES[index]}" \
      "${CHECK_CLASSIFICATIONS[index]}" \
      "${CHECK_STATUSES[index]}" \
      "${CHECK_ELAPSED_SECONDS[index]}"
    output_file="${CHECK_OUTPUT_FILES[index]}"
    echo 'diagnostic tail (last 40 lines):'
    if [[ -s "$output_file" ]]; then
      tail -n 40 "$output_file" || {
        echo 'Error: cannot read .NET harness diagnostic output' >&2
        exit 2
      }
      terminate_output_line "$output_file"
    else
      echo '(no diagnostic output)'
    fi
  done
}

echo "dotnet-clients harness"
echo "----------------------"

if contains restore; then
  run_check "restore" pnpm exec vouchington with-host-lock --name host-package-manager --timeout-seconds 300 --command-timeout-seconds 0 -- "$VOUCHA_DOTNET_HOST" restore --locked-mode -p:Configuration=Release "$SOLUTION"
fi

if contains fmt; then
  run_check "fmt" "$VOUCHA_DOTNET_HOST" format "$SOLUTION" --verify-no-changes --no-restore
fi

if contains ast-grep; then
  if [[ -z "${AST_GREP_BIN:-}" ]]; then
    if [[ -x "$ROOT_DIR/node_modules/@ast-grep/cli/ast-grep" ]]; then
      AST_GREP_BIN="$ROOT_DIR/node_modules/@ast-grep/cli/ast-grep"
    fi
  fi
  if [[ -n "${AST_GREP_BIN:-}" ]]; then
    run_check "ast-grep" run_in_directory "$ROOT_DIR" "$AST_GREP_BIN" scan --error --no-ignore hidden -- dotnet-clients/
  else
    echo "  ast-grep     - (skipped: repo-local ast-grep not found; run pnpm install at repo root)"
    SKIPPED=$((SKIPPED + 1))
  fi
fi

if contains resx-path; then
  run_check "resx-path" "$SCRIPT_DIR/resx-apostrophe-path.sh"
fi

if contains build; then
  run_check "build" "$SCRIPT_DIR/with-build-lock.sh" "$VOUCHA_DOTNET_HOST" build "$SOLUTION" --configuration Release --no-restore -p:UseSharedCompilation=false -nodeReuse:false
fi

echo "----------------------"
echo "  passed: $PASS  failed: $FAIL  skipped: $SKIPPED"
print_failure_summary

[[ $FAIL -eq 0 ]]
