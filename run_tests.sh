#!/usr/bin/env bash
# Runs every automated smoke test headlessly (Linux / macOS / Git Bash on Windows).
# Set GODOT to your Godot 4.4 executable, e.g.
#   GODOT=/Applications/Godot.app/Contents/MacOS/Godot ./run_tests.sh
#   GODOT=~/bin/Godot_v4.4.1-stable_linux.x86_64 ./run_tests.sh
set -u
cd "$(dirname "$0")"
GODOT="${GODOT:-godot}"
failed=0
log=$(mktemp)

"$GODOT" --headless --path . --import >/dev/null 2>&1

for t in --smoke --smoke-islands; do
  echo "=== $t"
  "$GODOT" --headless --path . --fixed-fps 60 -- "$t" >"$log" 2>&1
  grep '^\[smoke\]' "$log"
  grep -q '^\[smoke\] PASS' "$log" || failed=1
done

echo "=== multiplayer (host + client on 127.0.0.1)"
host_log=$(mktemp); client_log=$(mktemp)
"$GODOT" --headless --path . -- --smoke-host >"$host_log" 2>&1 &
host_pid=$!
sleep 3
"$GODOT" --headless --path . -- --smoke-join=127.0.0.1 >"$client_log" 2>&1
wait "$host_pid"
grep -h '^\[smoke\]' "$host_log" "$client_log"
grep -q '^\[smoke\] PASS' "$host_log" || failed=1
grep -q '^\[smoke\] PASS' "$client_log" || failed=1

echo
if [ "$failed" -eq 0 ]; then echo "ALL TESTS PASSED"; else echo "SOME TESTS FAILED"; exit 1; fi
