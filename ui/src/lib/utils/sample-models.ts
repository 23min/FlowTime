/**
 * Built-in sample models guaranteed to compile with the Rust engine.
 * Used as a fallback on /analysis when the selected run's model has a
 * Sim-style shape the Rust engine can't compile.
 *
 * Each sample is a concrete scenario with plain-English parameter names
 * so users can intuit what the sweep/sensitivity results mean without
 * studying the YAML.
 *
 * Design note — baseline tuning for sensitivity:
 *   The engine's sensitivity uses `values[0]` as the baseline and patches
 *   EVERY bin to a flat perturbed value (±perturbation). So if `values[0]`
 *   is well below the served rate, a ±5% perturbation stays below saturation
 *   and the queue never builds — gradient is zero. To get meaningful
 *   sensitivity, the first bin's arrivals must be close to (or slightly
 *   above) the served rate so small perturbations cross the saturation
 *   threshold. Later-bin values still show in sweep results (because sweep
 *   varies the whole flat value), but the first bin anchors sensitivity.
 *
 * Rust engine constraints:
 *   - Top-level `nodes:` accepts only kind: const | expr | pmf | router.
 *   - `topology.nodes:` accepts serviceWithBuffer / queue / service / dlq.
 *   - Every topology-node semantics ref must resolve to a top-level const/expr.
 *     Cross-topology refs like `OtherNode.served` are NOT supported.
 */

export interface SampleModel {
	id: string;
	title: string;
	description: string;
	/** One-paragraph explanation of the scenario in plain English. */
	scenario: string;
	/** Per-parameter plain-English meaning shown next to const node ids. */
	paramLegend: Record<string, string>;
	/** Per-topology-node plain-English meaning shown next to their queue series. */
	nodeLegend: Record<string, string>;
	yaml: string;
}

/**
 * Coffee shop: one barista serves a line of customers.
 * Baseline arrivals (22) is slightly ABOVE the barista rate (20), so the
 * register queue grows steadily. Sensitivity over customers_per_hour is
 * strongly positive; sensitivity over barista_service_rate is strongly
 * negative — exactly as intuition suggests.
 */
const COFFEE_SHOP: SampleModel = {
	id: 'coffee-shop',
	title: 'Coffee shop — register at rush',
	description: 'One barista, one line. Arrivals slightly exceed service — queue grows.',
	scenario:
		'One coffee shop with a single barista serving customers in order of arrival, over 24 one-hour bins. Arrivals are calibrated to slightly exceed the barista\'s throughput at the start of the day so the register queue builds up and is measurable. Try the Sensitivity tab to see how strongly each knob (arrivals vs. barista speed) pushes the queue up or down.',
	paramLegend: {
		customers_per_hour: 'How many customers arrive each hour. Baseline is 22/hr (just above the barista\'s rate), so the queue grows.',
		barista_service_rate: 'How many customers the barista can serve each hour. Baseline is 20/hr.',
	},
	nodeLegend: {
		Register: 'The line at the register. The engine emits its queue depth as series "register_queue".',
	},
	// values[0] anchors sensitivity — keep it at a saturation-relevant value.
	// Later bins add realistic variation for sweep visualisation.
	yaml: `schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  - id: customers_per_hour
    kind: const
    values: [22, 24, 26, 28, 30, 32, 30, 28, 26, 24, 22, 20, 18, 16, 18, 20, 22, 24, 26, 24, 22, 20, 18, 16]
  - id: barista_service_rate
    kind: const
    values: [20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20]
topology:
  nodes:
    - id: Register
      kind: serviceWithBuffer
      semantics:
        arrivals: customers_per_hour
        served: barista_service_rate
  edges: []
  constraints: []
`
};

/**
 * Support center: three independent channels (email, chat, phone).
 * Each channel has baseline arrivals slightly above its agent throughput
 * so the queue dynamics are non-degenerate and sensitivity ranks the
 * channels by how steeply they respond to their own parameters.
 */
const SUPPORT_CENTER: SampleModel = {
	id: 'support-center',
	title: 'Support center — three channels',
	description: 'Email / chat / phone, each over capacity at baseline. Six parameters.',
	scenario:
		'A customer support center with three independent channels — email, live chat, and phone — each with its own staff. At baseline, every channel runs slightly over capacity so each queue has real dynamics. Sensitivity over the six parameters tells you which channel is most responsive to changes in arrivals vs. staffing. Expect arrivals → positive gradient and throughput → negative gradient for each channel; their magnitudes rank the channels.',
	paramLegend: {
		email_tickets_per_hour: 'Email tickets arriving each hour. Baseline 42/hr (just over email team throughput).',
		email_agent_throughput: 'Email tickets resolved each hour by the email team. Baseline 40/hr.',
		chat_tickets_per_hour: 'Chat sessions starting each hour. Baseline 22/hr (just over chat team throughput).',
		chat_agent_throughput: 'Chat sessions resolved each hour by the chat team. Baseline 20/hr.',
		phone_tickets_per_hour: 'Phone calls arriving each hour. Baseline 17/hr (just over phone team throughput).',
		phone_agent_throughput: 'Phone calls resolved each hour by the phone team. Baseline 15/hr.',
	},
	nodeLegend: {
		EmailQueue: 'Email backlog. Series id: "email_queue_queue".',
		ChatQueue: 'Chat backlog. Series id: "chat_queue_queue".',
		PhoneQueue: 'Phone backlog. Series id: "phone_queue_queue".',
	},
	yaml: `schemaVersion: 1
grid:
  bins: 24
  binSize: 1
  binUnit: hours
nodes:
  - id: email_tickets_per_hour
    kind: const
    values: [42, 45, 48, 52, 55, 58, 60, 58, 55, 50, 48, 45, 42, 40, 38, 36, 34, 32, 30, 28, 26, 24, 22, 20]
  - id: email_agent_throughput
    kind: const
    values: [40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40]
  - id: chat_tickets_per_hour
    kind: const
    values: [22, 22, 24, 26, 28, 30, 28, 26, 24, 22, 20, 18, 16, 14, 14, 16, 18, 20, 22, 20, 18, 16, 14, 12]
  - id: chat_agent_throughput
    kind: const
    values: [20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20]
  - id: phone_tickets_per_hour
    kind: const
    values: [17, 18, 20, 22, 24, 26, 24, 22, 20, 18, 16, 14, 12, 10, 10, 12, 14, 16, 18, 16, 14, 12, 10, 8]
  - id: phone_agent_throughput
    kind: const
    values: [15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15]
topology:
  nodes:
    - id: EmailQueue
      kind: serviceWithBuffer
      semantics:
        arrivals: email_tickets_per_hour
        served: email_agent_throughput
    - id: ChatQueue
      kind: serviceWithBuffer
      semantics:
        arrivals: chat_tickets_per_hour
        served: chat_agent_throughput
    - id: PhoneQueue
      kind: serviceWithBuffer
      semantics:
        arrivals: phone_tickets_per_hour
        served: phone_agent_throughput
  edges: []
  constraints: []
`
};

export const SAMPLE_MODELS: SampleModel[] = [COFFEE_SHOP, SUPPORT_CENTER];
