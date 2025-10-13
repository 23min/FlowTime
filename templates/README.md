# FlowTime Template Notes

## it-system-microservices

- `telemetryRequestsSource`: optional `file://` URI for observed request arrivals. Leave blank for simulation (inline `values` remain authoritative).
- Telemetry example payload (synthetic files under `data/telemetry/` by default):
  ```json
  {
    "telemetryRequestsSource": "file:///workspaces/flowtime-vnext/data/telemetry/order-service_arrivals.csv"
  }
  ```

## transportation-basic

- `telemetryDemandSource`: optional `file://` URI for measured passenger demand. Defaults to empty to reuse the curated pattern.
- Telemetry example payload:
  ```json
  {
    "telemetryDemandSource": "file:///workspaces/flowtime-vnext/data/telemetry/passenger_demand.csv"
  }
  ```

## manufacturing-line

- `telemetryRawMaterialsSource`: optional `file://` URI for actual raw-material availability per bin.
- Telemetry example payload:
  ```json
  {
    "telemetryRawMaterialsSource": "file:///workspaces/flowtime-vnext/data/telemetry/raw_materials.csv"
  }
  ```

## supply-chain-multi-tier

- `telemetryDemandSource`: optional `file://` URI for customer demand measurements.
- Telemetry example payload:
  ```json
  {
    "telemetryDemandSource": "file:///workspaces/flowtime-vnext/data/telemetry/customer_demand.csv"
  }
  ```

## network-reliability

- `telemetryBaseLoadSource`: optional `file://` URI for base request load observations.
- Telemetry example payload:
  ```json
  {
    "telemetryBaseLoadSource": "file:///workspaces/flowtime-vnext/data/telemetry/base_load.csv"
  }
  ```
