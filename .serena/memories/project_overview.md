# FlowTime Project Overview

## Purpose
FlowTime is a deterministic, discrete-time, graph-based engine that models flows (entities) across services and queues, producing explainable time-series for backlog, latency, and throughput. It supports both:
- **What-if** scenarios (simulation)
- **What-is/what-was** scenarios (time-travel observability)

## Target Users
- SREs, platform engineers, data/ops analysts, product owners
- Teams needing explainable flow modeling without heavyweight simulation tooling

## Tech Stack
- .NET 9, C# with nullable reference types enabled
- ASP.NET Core for API
- Minimal APIs (no controllers)
- YAML for model definitions
- CSV for output artifacts
- HTTP API first, CLI/UI consume same surface

## Repository
- **flowtime-vnext**: Main FlowTime Engine repository
- **flowtime-sim-vnext**: FlowTime-Sim (template-based model authoring)
- Current branch: gold-first-kiss
