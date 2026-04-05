# Synthetic Ingest

**Status:** Retrospective completed epic grouping.

This folder groups the synthetic adapter milestone that enabled fully offline event ingestion using fixed NDJSON and Parquet datasets, giving FlowTime a stable local and CI-friendly ingest path.

## Themes

- Provide an offline event source that mimics normalized Gold data without cloud dependencies.
- Feed the existing stitching and metrics pipeline through file-backed adapters.
- Supply deterministic sample datasets for golden tests and end-to-end validation.

## Milestones

- `SYN-M-00.00` — synthetic adapter for local NDJSON and Parquet ingest.

## Notes

- This capability remains distinct from the later service and artifact registry work even though those surfaces reuse its file adapters.