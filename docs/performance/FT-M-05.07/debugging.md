# Topology Debug Logging

Enable verbose topology logging by appending `#debug=1` to the topology URL (e.g., `/time-travel/topology?runId=...#debug=1`). When enabled, the existing `debugLog()` statements in `src/FlowTime.UI/wwwroot/js/topologyCanvas.js` will emit `[Topology] ...` messages to the browser console.

Disable by removing the hash or using `#debug=0`.
