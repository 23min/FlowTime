#!/bin/bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
API_URL="http://localhost:8080"
FIXTURE_YAML="$ROOT/fixtures/order-system/api-model.yaml"

if [ ! -f "$FIXTURE_YAML" ]; then
  echo "Fixture model not found: $FIXTURE_YAML" >&2
  exit 1
fi

API_RUNNING=false
if curl -fsS "$API_URL/healthz" > /dev/null; then
  echo "✅ FlowTime API already running"
  API_RUNNING=true
else
  echo "Starting FlowTime API..."
  (cd "$ROOT" && dotnet run --project src/FlowTime.API --urls $API_URL >/dev/null 2>&1 &)
  API_PID=$!
  echo "Waiting for API to become healthy..."
  for _ in {1..30}; do
    if curl -fsS "$API_URL/healthz" > /dev/null; then
      echo "✅ API started"
      break
    fi
    sleep 1
  done
  if ! curl -fsS "$API_URL/healthz" > /dev/null; then
    echo "❌ API did not start" >&2
    if [ -n "${API_PID:-}" ]; then kill $API_PID 2>/dev/null || true; fi
    exit 1
  fi
fi

echo "Submitting order-system fixture to /v1/run..."
HTTP_CODE=$(curl -sS -o /tmp/api_smoke_response.json -w "%{http_code}" \
  -X POST "$API_URL/v1/run" \
  -H "Content-Type: text/plain" \
  --data-binary @"$FIXTURE_YAML")
RESPONSE=$(cat /tmp/api_smoke_response.json)
rm -f /tmp/api_smoke_response.json
echo "Response ($HTTP_CODE): $RESPONSE"

if [ "$HTTP_CODE" -ne 200 ]; then
  echo "❌ Unexpected status code from /v1/run: $HTTP_CODE" >&2
  [ "${API_RUNNING}" = true ] || kill $API_PID 2>/dev/null || true
  exit 1
fi

RUN_ID=$(echo "$RESPONSE" | grep -o '"runId"\s*:\s*"[^"]*"' | cut -d'"' -f4)
if [ -z "$RUN_ID" ]; then
  echo "❌ Unable to parse runId from response" >&2
  [ "${API_RUNNING}" = true ] || kill $API_PID 2>/dev/null || true
  exit 1
fi

echo "Checking run index for $RUN_ID ..."
INDEX_RESPONSE=$(curl -sS "$API_URL/v1/runs/$RUN_ID/index")
echo "Index response: $INDEX_RESPONSE"

if echo "$INDEX_RESPONSE" | grep -q '"series":\['; then
  echo "✅ Run artifacts reported by API"
else
  echo "⚠️ Run index did not contain expected entries" >&2
fi

if [ "$API_RUNNING" = false ] && [ -n "${API_PID:-}" ]; then
  echo "Stopping API..."
  kill $API_PID 2>/dev/null || true
  wait $API_PID 2>/dev/null || true
fi

echo "Fixture API smoke test complete."
