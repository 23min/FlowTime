// Adapter: engine-session EngineGraph → dag-map-view GraphResponse.
//
// The engine session returns a graph with simple { nodes, edges } shape.
// dag-map-view expects the GraphResponse shape used by the existing FlowTime API
// (GraphNode has `id` + `kind` + extras; GraphEdge has `id` + `from` + `to`).
//
// Pure function — unit-tested.

import type { EngineGraph } from './engine-session.js';
import type { GraphResponse, GraphNode, GraphEdge } from './types.js';

/**
 * Convert an EngineGraph to the GraphResponse shape consumed by dag-map-view.
 * Edge ids are synthesized as `e-{index}` since the engine graph doesn't include them.
 */
export function adaptEngineGraph(eg: EngineGraph): GraphResponse {
	const nodes: GraphNode[] = eg.nodes.map((n) => ({ id: n.id, kind: n.kind }));
	const edges: GraphEdge[] = eg.edges.map((e, i) => ({
		id: `e-${i}`,
		from: e.from,
		to: e.to,
	}));
	return { nodes, edges, order: [] };
}
