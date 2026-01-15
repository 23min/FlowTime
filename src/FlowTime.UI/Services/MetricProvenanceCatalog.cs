using System;
using System.Collections.Generic;

namespace FlowTime.UI.Services;

public sealed record MetricFormulaOption(string Formula, IReadOnlyList<string> Inputs);

public sealed record MetricProvenanceDefinition(
    string MetricKey,
    string DisplayName,
    string Unit,
    IReadOnlyList<MetricFormulaOption> Formulas,
    IReadOnlyList<string> GatingRules,
    string? Meaning)
{
    public MetricProvenanceEvaluation Evaluate(IReadOnlyCollection<string> availableSeries)
    {
        var seriesSet = new HashSet<string>(availableSeries ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        MetricFormulaOption? bestFormula = null;
        List<string>? bestMissing = null;

        foreach (var formula in Formulas)
        {
            var missing = new List<string>();
            foreach (var input in formula.Inputs)
            {
                if (!seriesSet.Contains(input))
                {
                    missing.Add(input);
                }
            }

            if (missing.Count == 0)
            {
                return new MetricProvenanceEvaluation(this, formula, missing);
            }

            if (bestMissing is null || missing.Count < bestMissing.Count)
            {
                bestMissing = missing;
                bestFormula = formula;
            }
        }

        return new MetricProvenanceEvaluation(this, bestFormula, bestMissing ?? new List<string>());
    }
}

public sealed record MetricProvenanceEvaluation(
    MetricProvenanceDefinition Definition,
    MetricFormulaOption? SelectedFormula,
    IReadOnlyList<string> MissingInputs);

public static class MetricProvenanceCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, MetricProvenanceDefinition>> Catalog =
        BuildCatalog();

    public static IReadOnlyDictionary<string, MetricProvenanceDefinition> GetForNodeKind(string nodeKind)
    {
        if (string.IsNullOrWhiteSpace(nodeKind))
        {
            return EmptyCatalog;
        }

        return Catalog.TryGetValue(nodeKind.Trim(), out var entries)
            ? entries
            : EmptyCatalog;
    }

    private static IReadOnlyDictionary<string, MetricProvenanceDefinition> EmptyCatalog { get; } =
        new Dictionary<string, MetricProvenanceDefinition>(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, MetricProvenanceDefinition>> BuildCatalog()
    {
        var service = BuildServiceCatalog();
        var queue = BuildQueueCatalog();
        var serviceWithBuffer = BuildServiceWithBufferCatalog();
        var sink = BuildSinkCatalog();
        var computed = BuildComputedCatalog();
        var pmf = BuildPmfCatalog();

        return new Dictionary<string, IReadOnlyDictionary<string, MetricProvenanceDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["service"] = service,
            ["router"] = service,
            ["sink"] = sink,
            ["serviceWithBuffer"] = serviceWithBuffer,
            ["queue"] = queue,
            ["dlq"] = queue,
            ["expr"] = computed,
            ["expression"] = computed,
            ["const"] = computed,
            ["constant"] = computed,
            ["pmf"] = pmf
        };
    }

    private static IReadOnlyDictionary<string, MetricProvenanceDefinition> BuildServiceCatalog()
    {
        return new Dictionary<string, MetricProvenanceDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["successRate"] = CreateDefinition(
                "successRate",
                "Success rate",
                "percent",
                new[]
                {
                    new MetricFormulaOption("successRate = throughputRatio", new[] { "throughputRatio" }),
                    new MetricFormulaOption("successRate = served / arrivals", new[] { "served", "arrivals" }),
                    new MetricFormulaOption("successRate = SLA completion", new[] { "sla:completion" }),
                    new MetricFormulaOption("successRate = SLA schedule adherence", new[] { "sla:scheduleAdherence" })
                },
                "Share of arrivals that complete successfully (or meet SLA when derived from SLA series).",
                new[]
                {
                    "Schedule adherence uses dispatch schedule bins only."
                }),
            ["utilization"] = CreateDefinition(
                "utilization",
                "Utilization",
                "percent",
                new[]
                {
                    new MetricFormulaOption("utilization = served / capacity", new[] { "served", "capacity" })
                },
                "Fraction of capacity used."),
            ["serviceTimeMs"] = CreateDefinition(
                "serviceTimeMs",
                "Service time",
                "ms",
                new[]
                {
                    new MetricFormulaOption("serviceTime = processingTimeMsSum / servedCount", new[] { "processingTimeMsSum", "servedCount" })
                },
                "Average processing time per served item."),
            ["latencyMinutes"] = CreateDefinition(
                "latencyMinutes",
                "Latency",
                "min",
                new[]
                {
                    new MetricFormulaOption("latency = latencyMinutes series", new[] { "latencyMinutes" })
                },
                "Reported latency for the service (source-defined).",
                new[]
                {
                    "Unavailable when queue latency gate is closed."
                }),
            ["arrivals"] = CreateDefinition(
                "arrivals",
                "Arrivals",
                "count",
                new[]
                {
                    new MetricFormulaOption("arrivals = arrivals series", new[] { "arrivals" })
                },
                "Number of arrivals observed."),
            ["served"] = CreateDefinition(
                "served",
                "Served",
                "count",
                new[]
                {
                    new MetricFormulaOption("served = served series", new[] { "served" })
                },
                "Number of items completed."),
            ["capacity"] = CreateDefinition(
                "capacity",
                "Capacity",
                "count",
                new[]
                {
                    new MetricFormulaOption("capacity = capacity series", new[] { "capacity" })
                },
                "Available service capacity."),
            ["parallelism"] = CreateDefinition(
                "parallelism",
                "Instances",
                "count",
                new[]
                {
                    new MetricFormulaOption("parallelism = parallelism series", new[] { "parallelism" })
                },
                "Concurrent instances/workers."),
            ["effectiveCapacity"] = CreateDefinition(
                "effectiveCapacity",
                "Effective capacity",
                "count",
                new[]
                {
                    new MetricFormulaOption("effectiveCapacity = capacity * parallelism", new[] { "capacity", "parallelism" })
                },
                "Capacity after applying parallelism."),
            ["queue"] = CreateDefinition(
                "queue",
                "Queue depth",
                "count",
                new[]
                {
                    new MetricFormulaOption("queue = queue series", new[] { "queue" })
                },
                "Queue depth."),
            ["flowLatencyMs"] = CreateDefinition(
                "flowLatencyMs",
                "Flow latency",
                "ms",
                new[]
                {
                    new MetricFormulaOption("flowLatency = flowLatencyMs series", new[] { "flowLatencyMs" })
                },
                "Average end-to-end latency for completed items."),
            ["errorRate"] = CreateDefinition(
                "errorRate",
                "Error rate",
                "percent",
                new[]
                {
                    new MetricFormulaOption("errorRate = errors / arrivals", new[] { "errors", "arrivals" }),
                    new MetricFormulaOption("errorRate = errors / served", new[] { "errors", "served" })
                },
                "Fraction of arrivals or served items that errored."),
            ["errors"] = CreateDefinition(
                "errors",
                "Errors",
                "count",
                new[]
                {
                    new MetricFormulaOption("errors = errors series", new[] { "errors" })
                },
                "Number of errors."),
            ["attempts"] = CreateDefinition(
                "attempts",
                "Attempts",
                "count",
                new[]
                {
                    new MetricFormulaOption("attempts = attempts series", new[] { "attempts" })
                },
                "Total processing attempts (initial plus retries)."),
            ["failures"] = CreateDefinition(
                "failures",
                "Failed retries",
                "count",
                new[]
                {
                    new MetricFormulaOption("failures = failures series", new[] { "failures" })
                },
                "Failed retry attempts."),
            ["retryEcho"] = CreateDefinition(
                "retryEcho",
                "Retry echo",
                "count",
                new[]
                {
                    new MetricFormulaOption("retryEcho = retryEcho series", new[] { "retryEcho" })
                },
                "Retries echoed back as arrivals for retry workload."),
            ["exhaustedFailures"] = CreateDefinition(
                "exhaustedFailures",
                "Exhausted",
                "count",
                new[]
                {
                    new MetricFormulaOption("exhausted = exhaustedFailures series", new[] { "exhaustedFailures" })
                },
                "Failures after all retries are exhausted."),
            ["retryBudgetRemaining"] = CreateDefinition(
                "retryBudgetRemaining",
                "Retry budget remaining",
                "count",
                new[]
                {
                    new MetricFormulaOption("retryBudgetRemaining = retryBudgetRemaining series", new[] { "retryBudgetRemaining" })
                },
                "Remaining retry budget."),
            ["retryTax"] = CreateDefinition(
                "retryTax",
                "Retry tax",
                "percent",
                new[]
                {
                    new MetricFormulaOption("retryTax = retryTax series", new[] { "retryTax" })
                },
                "Fraction of capacity spent on retries."),
            ["maxAttempts"] = CreateDefinition(
                "maxAttempts",
                "Max attempts",
                "count",
                new[]
                {
                    new MetricFormulaOption("maxAttempts = maxAttempts series", new[] { "maxAttempts" })
                },
                "Configured maximum attempts per arrival."),
            ["arrivalSchedule"] = CreateDefinition(
                "arrivalSchedule",
                "Arrival schedule",
                "metadata",
                new[]
                {
                    new MetricFormulaOption("arrivalSchedule = dispatch schedule metadata", new[] { "dispatchSchedule" })
                },
                "Dispatch schedule metadata that gates arrivals."),
            ["scheduleCapacity"] = CreateDefinition(
                "scheduleCapacity",
                "Schedule capacity",
                "metadata",
                new[]
                {
                    new MetricFormulaOption("scheduleCapacity = dispatch schedule metadata", new[] { "dispatchSchedule" })
                },
                "Capacity metadata used by the dispatch schedule."),
            ["queueLatencyStatus"] = CreateDefinition(
                "queueLatencyStatus",
                "Latency status",
                "metadata",
                new[]
                {
                    new MetricFormulaOption("queueLatencyStatus = queue latency status metadata", new[] { "queueLatencyStatus" })
                },
                "Queue latency gate status metadata."),
            ["completion"] = CreateDefinition(
                "completion",
                "Completion SLA",
                "percent",
                new[]
                {
                    new MetricFormulaOption("completion = SLA completion series", new[] { "sla:completion" })
                },
                "Share of arrivals that met the completion SLA."),
            ["backlogAge"] = CreateDefinition(
                "backlogAge",
                "Backlog age SLA",
                "min",
                new[]
                {
                    new MetricFormulaOption("backlogAge = SLA backlogAge series", new[] { "sla:backlogAge" })
                },
                "Backlog age SLA value in minutes (how long the oldest pending work has been waiting).",
                new[]
                {
                    "Unavailable when backlog age series is missing."
                }),
            ["scheduleAdherence"] = CreateDefinition(
                "scheduleAdherence",
                "Schedule adherence",
                "percent",
                new[]
                {
                    new MetricFormulaOption("scheduleAdherence = SLA scheduleAdherence series", new[] { "sla:scheduleAdherence" })
                },
                "Share of schedule bins that met dispatch timing.",
                new[]
                {
                    "Uses dispatch schedule bins only."
                })
        };
    }

    private static IReadOnlyDictionary<string, MetricProvenanceDefinition> BuildQueueCatalog()
    {
        return new Dictionary<string, MetricProvenanceDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["queue"] = CreateDefinition(
                "queue",
                "Queue depth",
                "count",
                new[]
                {
                    new MetricFormulaOption("queue = queue series", new[] { "queue" })
                },
                "Queue depth."),
            ["latencyMinutes"] = CreateDefinition(
                "latencyMinutes",
                "Queue latency",
                "min",
                new[]
                {
                    new MetricFormulaOption("queue latency = latencyMinutes series", new[] { "latencyMinutes" })
                },
                "Average wait time in the queue.",
                new[]
                {
                    "Unavailable when queue latency gate is closed."
                }),
            ["arrivals"] = CreateDefinition(
                "arrivals",
                "Arrivals",
                "count",
                new[]
                {
                    new MetricFormulaOption("arrivals = arrivals series", new[] { "arrivals" })
                },
                "Number of arrivals observed."),
            ["served"] = CreateDefinition(
                "served",
                "Served",
                "count",
                new[]
                {
                    new MetricFormulaOption("served = served series", new[] { "served" })
                },
                "Number of items completed."),
            ["queueLatencyStatus"] = CreateDefinition(
                "queueLatencyStatus",
                "Latency status",
                "metadata",
                new[]
                {
                    new MetricFormulaOption("queueLatencyStatus = queue latency status metadata", new[] { "queueLatencyStatus" })
                },
                "Queue latency gate status metadata.")
        };
    }

    private static IReadOnlyDictionary<string, MetricProvenanceDefinition> BuildServiceWithBufferCatalog()
    {
        var baseCatalog = new Dictionary<string, MetricProvenanceDefinition>(BuildServiceCatalog(), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in BuildQueueCatalog())
        {
            baseCatalog[entry.Key] = entry.Value;
        }

        return baseCatalog;
    }

    private static IReadOnlyDictionary<string, MetricProvenanceDefinition> BuildSinkCatalog()
    {
        var baseCatalog = new Dictionary<string, MetricProvenanceDefinition>(BuildServiceCatalog(), StringComparer.OrdinalIgnoreCase)
        {
            ["successRate"] = CreateDefinition(
                "successRate",
                "Success rate",
                "percent",
                new[]
                {
                    new MetricFormulaOption("successRate = SLA schedule adherence", new[] { "sla:scheduleAdherence" }),
                    new MetricFormulaOption("successRate = SLA completion", new[] { "sla:completion" }),
                    new MetricFormulaOption("successRate = throughputRatio", new[] { "throughputRatio" }),
                    new MetricFormulaOption("successRate = served / arrivals", new[] { "served", "arrivals" })
                },
                "Share of arrivals that complete successfully (or meet SLA when derived from SLA series).",
                new[]
                {
                    "Schedule adherence uses dispatch schedule bins only."
                })
        };

        return baseCatalog;
    }

    private static IReadOnlyDictionary<string, MetricProvenanceDefinition> BuildComputedCatalog()
    {
        return new Dictionary<string, MetricProvenanceDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["values"] = CreateDefinition(
                "values",
                "Output",
                "count",
                new[]
                {
                    new MetricFormulaOption("output = values series", new[] { "values" })
                },
                "Expression output.")
        };
    }

    private static IReadOnlyDictionary<string, MetricProvenanceDefinition> BuildPmfCatalog()
    {
        return new Dictionary<string, MetricProvenanceDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["probability"] = CreateDefinition(
                "probability",
                "Probability",
                "percent",
                new[]
                {
                    new MetricFormulaOption("probability = distribution series", new[] { "distribution" })
                },
                "Probability mass for the value bucket."),
            ["values"] = CreateDefinition(
                "values",
                "Values",
                "count",
                new[]
                {
                    new MetricFormulaOption("values = values series", new[] { "values" })
                },
                "PMF support values.")
        };
    }

    private static MetricProvenanceDefinition CreateDefinition(
        string metricKey,
        string displayName,
        string unit,
        IReadOnlyList<MetricFormulaOption> formulas,
        string? meaning = null,
        IReadOnlyList<string>? gatingRules = null)
    {
        return new MetricProvenanceDefinition(
            metricKey,
            displayName,
            unit,
            formulas,
            gatingRules ?? Array.Empty<string>(),
            meaning);
    }
}
