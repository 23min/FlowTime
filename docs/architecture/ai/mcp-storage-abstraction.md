# MCP Storage Abstraction

Status: draft

## 1. Purpose

Define a storage architecture that supports MCP-driven modeling and analysis without local filesystem dependencies. This document is part of the MCP epic and covers drafts, generated models, run bundles, provenance, and inspection artifacts, using a pluggable storage interface with multiple backends.

## 2. Scope

### In Scope
- Storage abstraction for drafts, models, and run bundles.
- Pluggable backends (local filesystem for dev, object storage for deployed environments).
- Optional metadata index (file-based or database-backed).
- Configuration-driven selection of storage backend.
- Clear boundary between MCP server, Sim, and Engine for storage access.

### Out of Scope
- Cloud-specific deployment implementation details.
- Authentication/authorization design.
- Full audit logging and compliance posture.

## 3. Goals

- Remove direct repository filesystem assumptions from MCP workflows.
- Allow both single-user local development and multi-user deployed scenarios.
- Keep storage concerns isolated from MCP tool schemas.
- Support incremental adoption without breaking existing workflows.

## 4. Storage Surfaces

### 4.1 Drafts
- Draft YAML content.
- Draft metadata (createdAt, updatedAt, baseTemplateId, contentHash).
- Draft versions or change history (optional).

### 4.2 Models
- Generated model artifacts and their provenance metadata.
- Model hash indexing for reuse and caching.

### 4.3 Runs and Artifacts
- Run bundles (inputs, outputs, diagnostics).
- Provenance metadata (template/draft source, parameters, engine version).
- Inspection artifacts for analyst tools (graph snapshots, metrics).

## 5. Storage Interface (Conceptual)

Define a storage abstraction with logical operations:
- DraftStore: create/get/list/update/delete, diff/patch support.
- ModelStore: write/read/list by template/model hash.
- RunStore: write/read bundles and metadata; list runs by tenant/user.

These are conceptual interfaces; the storage backend hides whether data is local files, object storage, or database-backed.

## 6. Storage Backends (Pluggable)

### 6.1 Local Filesystem (Dev)
- Drafts and run bundles stored on local disk.
- Simple index file for metadata (JSON/YAML).
- Best for development and PoC.

### 6.2 Object Storage + Index File
- Drafts and run bundles stored as blobs.
- Metadata index stored in object storage as JSON/YAML.
- Simpler than a database, but needs concurrency handling (etag/versioning).

### 6.3 Object Storage + Database
- Drafts and run bundles stored as blobs.
- Metadata stored in a database for fast queries and multi-user access.
- Recommended for production and multi-tenant deployments.

## 7. Configuration-Driven Selection

Storage backend selection should be controlled by configuration:
- storage.backend = filesystem | blob | blob+db
- storage.root or storage.container
- optional metadata index strategy (file or db)

Configuration should be shared by MCP server, Sim, and Engine services so they can resolve the same artifacts.

## 8. Service Boundaries

### MCP Server
- Uses storage abstraction; never assumes repo paths.
- Produces drafts and run requests; hands off storage references (URIs or IDs).

### Sim Service
- Reads drafts/models from storage using the same abstraction.
- Produces model artifacts and writes them to storage.

### Engine
- Accepts a storage reference to a run bundle (not a local path).
- Reads run artifacts via storage abstraction.

## 9. Migration and Compatibility

- Support a local filesystem backend for existing workflows.
- Provide a migration path to object storage with minimal API changes.
- Ensure run bundles can be referenced by URI rather than path.

## 10. Milestone Mapping

This storage work is delivered last in the MCP epic:
- M-08.01: MCP Server PoC (run + inspect loop).
- M-08.02: Draft workflow tools (authoring surface).
- M-08.03: Data intake and profile fitting.
- M-08.04: Storage abstraction (HTTP-only drafts/runs, pluggable backends).

## 11. Risks and Open Questions

- How to enforce safe concurrency without a database (etag/versioning strategy).
- How to handle cleanup/retention of large run bundles.
- How to provide stable URIs across environments.
- What level of metadata querying is needed for analyst workflows.
