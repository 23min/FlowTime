# Simulation Schema (Legacy - Replaced)

**⚠️ REPLACED**: This schema has been completely replaced by the new node-based Template Schema.

See [`template-schema.md`](template-schema.md) for the current schema.

**Template recreation guide**: [`template-migration.md`](template-migration.md)

## Legacy Schema (For Reference Only)

The legacy schema used simple `arrivals`/`route` structure:

```yaml
schemaVersion: 1
grid:
  bins: 12
  binMinutes: 60
arrivals:
  kind: const  # or pmf
  values: [100, 150, 200]
  # For PMF:
  # probabilities: [0.4, 0.3, 0.3]
route:
  id: requests
```

### Key Limitations
- No parameterization support
- Single arrival source only
- No complex expressions or dependencies
- Limited PMF functionality
- No metadata or validation

## Complete Replacement

All legacy simulation files will be replaced with node-based templates. No legacy support will be provided.

## Node-Based Template Benefits

The new template schema provides:
- ✅ Parameterized templates with type safety
- ✅ Multiple node types (const, pmf, expr)
- ✅ Complex expressions and dependencies  
- ✅ Enhanced PMF functionality
- ✅ Metadata and validation
- ✅ Multiple output series
- ✅ Better extensibility