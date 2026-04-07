using FlowTime.Core.Models;

namespace FlowTime.Core.Metrics;

public static class RuntimeAnalyticalEvaluator
{
    private const int backlogGrowthStreakBins = 3;
    private const int backlogOverloadStreakBins = 3;
    private const int backlogAgeRiskStreakBins = 3;

    public static AnalyticalResult ComputeBin(
        RuntimeAnalyticalDescriptor descriptor,
        NodeSemantics semantics,
        NodeData data,
        int index,
        double binMs,
        double? servedOverride = null,
        IReadOnlyList<ClassEntry<ClassMetricsSnapshot>>? classEntries = null,
        double? flowLatencyMs = null,
        bool emitFlowLatency = false)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(semantics);
        ArgumentNullException.ThrowIfNull(data);

        var served = Sanitize(servedOverride) ?? SampleSeries(data.Served, index);
        var result = ComputeBin(
            descriptor,
            SampleSeries(data.QueueDepth, index),
            served,
            SampleSeries(data.ProcessingTimeMsSum, index),
            SampleSeries(data.ServedCount, index),
            binMs);

        return result with
        {
            Capacity = ComputeCapacityBin(semantics, data, index, served),
            Emission = BuildBinEmission(descriptor, data, result, flowLatencyMs, emitFlowLatency),
            ByClass = ComputeClassBins(descriptor, classEntries, binMs),
            FlowLatencyMs = Sanitize(flowLatencyMs)
        };
    }

    public static AnalyticalResult ComputeBin(
        RuntimeAnalyticalDescriptor descriptor,
        double? queueDepth,
        double? served,
        double? processingTimeMsSum,
        double? servedCount,
        double binMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        double? queueTimeMs = null;
        double? serviceTimeMs = null;

        if (descriptor.HasQueueSemantics && queueDepth.HasValue && served.HasValue
            && IsFinite(queueDepth.Value) && IsFinite(served.Value))
        {
            queueTimeMs = Sanitize(CycleTimeComputer.CalculateQueueTime(queueDepth.Value, served.Value, binMs));
        }

        if (descriptor.HasServiceSemantics)
        {
            var rawProcTime = processingTimeMsSum.HasValue && IsFinite(processingTimeMsSum.Value)
                ? processingTimeMsSum
                : null;
            var rawServedCount = servedCount.HasValue && IsFinite(servedCount.Value)
                ? servedCount
                : null;
            serviceTimeMs = Sanitize(CycleTimeComputer.CalculateServiceTime(rawProcTime, rawServedCount));
        }

        var cycleTimeMs = (descriptor.HasQueueSemantics || descriptor.HasServiceSemantics)
            ? Sanitize(CycleTimeComputer.CalculateCycleTime(queueTimeMs, serviceTimeMs))
            : null;

        var flowEfficiency = (descriptor.HasQueueSemantics || descriptor.HasServiceSemantics)
            ? Sanitize(CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs, cycleTimeMs))
            : null;

        double? latencyMinutes = null;
        if (descriptor.HasQueueSemantics && queueDepth.HasValue && served.HasValue
            && IsFinite(queueDepth.Value) && IsFinite(served.Value))
        {
            latencyMinutes = Sanitize(ComputeLatencyMinutes(queueDepth.Value, served.Value, binMs));
        }

        return new AnalyticalResult
        {
            QueueTimeMs = queueTimeMs,
            ServiceTimeMs = serviceTimeMs,
            CycleTimeMs = cycleTimeMs,
            FlowEfficiency = flowEfficiency,
            LatencyMinutes = latencyMinutes,
        };
    }

    public static AnalyticalWindowResult ComputeWindow(
        RuntimeAnalyticalDescriptor descriptor,
        NodeSemantics semantics,
        NodeData data,
        int startBin,
        int count,
        double binMs,
        double?[]? servedOverride = null,
        IReadOnlyList<ClassEntry<NodeClassData>>? classEntries = null,
        double?[]? flowLatencyMs = null,
        double stationarityTolerance = 0.25)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(semantics);
        ArgumentNullException.ThrowIfNull(data);

        var result = ComputeWindow(descriptor, data, startBin, count, binMs);
        return result with
        {
            Capacity = ComputeCapacityWindow(semantics, data, startBin, count, servedOverride),
            Emission = BuildWindowEmission(descriptor, data, result, flowLatencyMs),
            ByClass = ComputeClassWindows(descriptor, classEntries ?? ClassMetricsAggregator.BuildClassEntries(data), startBin, count, binMs),
            FlowLatencyMs = SanitizeSeries(flowLatencyMs),
            WarningFacts = BuildWindowWarningFacts(descriptor, semantics, data, startBin, count, binMs, stationarityTolerance)
        };
    }

    public static AnalyticalWindowResult ComputeWindow(
        RuntimeAnalyticalDescriptor descriptor,
        NodeData data,
        int startBin,
        int count,
        double binMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(data);

        var queueTimeMs = new double?[count];
        var serviceTimeMs = new double?[count];
        var cycleTimeMs = new double?[count];
        var flowEfficiency = new double?[count];
        var latencyMinutes = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var idx = startBin + i;
            var qd = SampleSeries(data.QueueDepth, idx);
            var sv = SampleSeries(data.Served, idx);
            var pts = SampleSeries(data.ProcessingTimeMsSum, idx);
            var sc = SampleSeries(data.ServedCount, idx);

            var bin = ComputeBin(descriptor, qd, sv, pts, sc, binMs);
            queueTimeMs[i] = bin.QueueTimeMs;
            serviceTimeMs[i] = bin.ServiceTimeMs;
            cycleTimeMs[i] = bin.CycleTimeMs;
            flowEfficiency[i] = bin.FlowEfficiency;
            latencyMinutes[i] = bin.LatencyMinutes;
        }

        return new AnalyticalWindowResult
        {
            QueueTimeMs = queueTimeMs,
            ServiceTimeMs = serviceTimeMs,
            CycleTimeMs = cycleTimeMs,
            FlowEfficiency = flowEfficiency,
            LatencyMinutes = latencyMinutes,
        };
    }

    public static AnalyticalCapacityResult ComputeCapacityBin(
        NodeSemantics semantics,
        NodeData data,
        int index,
        double? servedOverride = null)
    {
        ArgumentNullException.ThrowIfNull(semantics);
        ArgumentNullException.ThrowIfNull(data);

        var baseCapacity = SampleSeries(data.Capacity, index);
        var parallelism = SampleParallelism(semantics, data, index);
        var effectiveCapacity = ComputeEffectiveCapacity(baseCapacity, parallelism);
        var served = Sanitize(servedOverride) ?? SampleSeries(data.Served, index);
        var utilization = served.HasValue
            ? Sanitize(UtilizationComputer.Calculate(served.Value, effectiveCapacity))
            : null;

        return new AnalyticalCapacityResult
        {
            BaseCapacity = baseCapacity,
            Parallelism = parallelism,
            EffectiveCapacity = effectiveCapacity,
            Utilization = utilization,
        };
    }

    public static AnalyticalCapacityWindowResult ComputeCapacityWindow(
        NodeSemantics semantics,
        NodeData data,
        int startBin,
        int count,
        double?[]? servedOverride = null)
    {
        ArgumentNullException.ThrowIfNull(semantics);
        ArgumentNullException.ThrowIfNull(data);

        var baseCapacity = new double?[count];
        var parallelism = new double?[count];
        var effectiveCapacity = new double?[count];
        var utilization = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var index = startBin + i;
            var capacity = SampleSeries(data.Capacity, index);
            var parallelismValue = SampleParallelism(semantics, data, index);
            var effectiveCapacityValue = ComputeEffectiveCapacity(capacity, parallelismValue);
            var served = servedOverride is null
                ? SampleSeries(data.Served, index)
                : i < servedOverride.Length
                    ? Sanitize(servedOverride[i])
                    : null;

            baseCapacity[i] = capacity;
            parallelism[i] = parallelismValue;
            effectiveCapacity[i] = effectiveCapacityValue;
            utilization[i] = served.HasValue
                ? Sanitize(UtilizationComputer.Calculate(served.Value, effectiveCapacityValue))
                : null;
        }

        return new AnalyticalCapacityWindowResult
        {
            BaseCapacity = baseCapacity,
            Parallelism = parallelism,
            EffectiveCapacity = effectiveCapacity,
            Utilization = utilization,
        };
    }

    public static AnalyticalResult ComputeClassBin(
        RuntimeAnalyticalDescriptor descriptor,
        ClassMetricsSnapshot snapshot,
        double binMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return ComputeBin(descriptor, snapshot.Queue, snapshot.Served, snapshot.ProcessingTimeMsSum, snapshot.ServedCount, binMs);
    }

    public static AnalyticalWindowResult ComputeClassWindow(
        RuntimeAnalyticalDescriptor descriptor,
        NodeClassData classData,
        int startBin,
        int count,
        double binMs)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(classData);

        var queueTimeMs = new double?[count];
        var serviceTimeMs = new double?[count];
        var cycleTimeMs = new double?[count];
        var flowEfficiency = new double?[count];
        var latencyMinutes = new double?[count];

        for (var i = 0; i < count; i++)
        {
            var idx = startBin + i;
            var qd = SampleSeries(classData.QueueDepth, idx);
            var sv = SampleSeries(classData.Served, idx);
            var pts = SampleSeries(classData.ProcessingTimeMsSum, idx);
            var sc = SampleSeries(classData.ServedCount, idx);

            var bin = ComputeBin(descriptor, qd, sv, pts, sc, binMs);
            queueTimeMs[i] = bin.QueueTimeMs;
            serviceTimeMs[i] = bin.ServiceTimeMs;
            cycleTimeMs[i] = bin.CycleTimeMs;
            flowEfficiency[i] = bin.FlowEfficiency;
            latencyMinutes[i] = bin.LatencyMinutes;
        }

        return new AnalyticalWindowResult
        {
            QueueTimeMs = queueTimeMs,
            ServiceTimeMs = serviceTimeMs,
            CycleTimeMs = cycleTimeMs,
            FlowEfficiency = flowEfficiency,
            LatencyMinutes = latencyMinutes,
        };
    }

    public static IReadOnlyDictionary<string, double?[]> ComputeFlowLatency(
        Topology topology,
        IReadOnlyDictionary<string, double?[]> cycleTimeByNode,
        IReadOnlyDictionary<string, double?[]> edgeFlowById,
        IReadOnlyDictionary<string, NodeData> nodeData)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(cycleTimeByNode);
        ArgumentNullException.ThrowIfNull(edgeFlowById);
        ArgumentNullException.ThrowIfNull(nodeData);

        var bins = ResolveSeriesLength(cycleTimeByNode, edgeFlowById, nodeData);
        var result = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
        if (bins <= 0)
        {
            return result;
        }

        var incomingEdges = BuildIncomingFlowEdges(topology, edgeFlowById);
        foreach (var node in OrderNodesTopologically(topology))
        {
            if (!nodeData.TryGetValue(node.Id, out var data))
            {
                continue;
            }

            var baseSeries = cycleTimeByNode.TryGetValue(node.Id, out var rawBaseSeries)
                ? rawBaseSeries
                : CreateNullSeries(bins);
            var isSink = node.Analytical.Category == RuntimeAnalyticalNodeCategory.Sink;
            var hasArrivalsSemantics = node.Semantics.Arrivals is not null;
            var hasServedSemantics = !isSink && node.Semantics.Served is not null;

            double?[]? upstream = null;
            if (incomingEdges.TryGetValue(node.Id, out var predecessors) && predecessors.Count > 0)
            {
                upstream = new double?[bins];
                for (var i = 0; i < bins; i++)
                {
                    double totalFlow = 0d;
                    double weightedLatency = 0d;

                    foreach (var (predId, flow) in predecessors)
                    {
                        if (!result.TryGetValue(predId, out var predSeries) || i >= predSeries.Length)
                        {
                            continue;
                        }

                        var candidateLatency = predSeries[i];
                        if (!candidateLatency.HasValue || !IsFinite(candidateLatency.Value))
                        {
                            continue;
                        }

                        if (i >= flow.Length)
                        {
                            continue;
                        }

                        var flowValue = flow[i];
                        if (!flowValue.HasValue || !IsFinite(flowValue.Value) || flowValue.Value <= 0d)
                        {
                            continue;
                        }

                        totalFlow += flowValue.Value;
                        weightedLatency += flowValue.Value * candidateLatency.Value;
                    }

                    if (totalFlow > 0d)
                    {
                        upstream[i] = Round(Sanitize(weightedLatency / totalFlow));
                    }
                }
            }

            var combined = new double?[bins];
            for (var i = 0; i < bins; i++)
            {
                var baseVal = i < baseSeries.Length ? baseSeries[i] : null;
                var upVal = upstream is not null && i < upstream.Length ? upstream[i] : null;

                if (isSink && hasArrivalsSemantics)
                {
                    var arrivals = SampleSeries(data.Arrivals, i);
                    if (!arrivals.HasValue || arrivals.Value <= 0d)
                    {
                        combined[i] = null;
                        continue;
                    }
                }
                else if (hasServedSemantics)
                {
                    var served = SampleSeries(data.Served, i);
                    if (!served.HasValue || served.Value <= 0d)
                    {
                        combined[i] = null;
                        continue;
                    }
                }

                if (baseVal.HasValue && IsFinite(baseVal.Value))
                {
                    combined[i] = upVal.HasValue ? baseVal.Value + upVal.Value : baseVal.Value;
                }
                else
                {
                    combined[i] = upVal.HasValue ? upVal.Value : null;
                }
            }

            result[node.Id] = combined;
        }

        return result;
    }

    public static IReadOnlyList<string> GetAdvertisedAnalyticalKeys(RuntimeAnalyticalDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var keys = new List<string>();
        if (descriptor.HasQueueSemantics)
        {
            keys.Add("queueTimeMs");
        }

        if (descriptor.HasServiceSemantics)
        {
            keys.Add("serviceTimeMs");
        }

        if (descriptor.HasQueueSemantics || descriptor.HasServiceSemantics)
        {
            keys.Add("cycleTimeMs");
        }

        if (descriptor.HasServiceSemantics)
        {
            keys.Add("flowEfficiency");
        }

        return keys;
    }

    public static bool CheckStationarity(
        RuntimeAnalyticalDescriptor descriptor,
        double[] arrivals,
        double tolerance = 0.25)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(arrivals);

        if (!descriptor.StationarityWarningApplicable)
        {
            return false;
        }

        return IsNonStationary(arrivals, tolerance);
    }

    private static double? ComputeLatencyMinutes(double queueDepth, double served, double binMs)
    {
        var queueTimeMs = CycleTimeComputer.CalculateQueueTime(queueDepth, served, binMs);
        return queueTimeMs.HasValue ? queueTimeMs.Value / 60_000.0 : null;
    }

    private static bool IsNonStationary(double[] arrivals, double tolerance = 0.25)
    {
        if (arrivals.Length < 2)
        {
            return false;
        }

        var mid = arrivals.Length / 2;

        double sumFirst = 0;
        for (var i = 0; i < mid; i++)
        {
            sumFirst += arrivals[i];
        }

        double sumSecond = 0;
        for (var i = mid; i < arrivals.Length; i++)
        {
            sumSecond += arrivals[i];
        }

        var avgFirst = sumFirst / mid;
        var avgSecond = sumSecond / (arrivals.Length - mid);

        var baseline = Math.Max(avgFirst, avgSecond);
        if (baseline <= 0)
        {
            return false;
        }

        var divergence = Math.Abs(avgFirst - avgSecond) / baseline;
        return divergence > tolerance;
    }

    private static AnalyticalWindowWarningFacts BuildWindowWarningFacts(
        RuntimeAnalyticalDescriptor descriptor,
        NodeSemantics semantics,
        NodeData data,
        int startBin,
        int count,
        double binMs,
        double stationarityTolerance)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(semantics);
        ArgumentNullException.ThrowIfNull(data);

        var endBin = startBin + count - 1;
        var facts = new AnalyticalWindowWarningFacts();

        if (data.Arrivals is not null && count >= 2)
        {
            var arrivals = new double[count];
            for (var i = 0; i < count; i++)
            {
                var idx = startBin + i;
                arrivals[i] = idx < data.Arrivals.Length ? data.Arrivals[idx] : 0d;
            }

            facts = facts with
            {
                NonStationary = CheckStationarity(descriptor, arrivals, stationarityTolerance)
            };
        }

        var growth = FindQueueGrowthStreak(data.QueueDepth, startBin, endBin);
        if (growth.HasValue && growth.Value.Length >= backlogGrowthStreakBins)
        {
            facts = facts with
            {
                BacklogGrowth = new AnalyticalStreakWarningFact
                {
                    StartBin = growth.Value.Start,
                    EndBin = growth.Value.End,
                    Length = growth.Value.Length
                }
            };
        }

        var overload = FindOverloadStreak(semantics, data, startBin, endBin);
        if (overload.HasValue && overload.Value.Length >= backlogOverloadStreakBins)
        {
            facts = facts with
            {
                Overload = new AnalyticalStreakWarningFact
                {
                    StartBin = overload.Value.Start,
                    EndBin = overload.Value.End,
                    Length = overload.Value.Length
                }
            };
        }

        var ageRisk = FindAgeRiskStreak(data.QueueDepth, data.Served, binMs / 60_000.0, semantics.SlaMinutes, startBin, endBin);
        if (ageRisk.HasValue && ageRisk.Value.Length >= backlogAgeRiskStreakBins)
        {
            facts = facts with
            {
                AgeRisk = new AnalyticalStreakWarningFact
                {
                    StartBin = ageRisk.Value.Start,
                    EndBin = ageRisk.Value.End,
                    Length = ageRisk.Value.Length
                }
            };
        }

        return facts;
    }

    private static (int Start, int End, int Length)? FindQueueGrowthStreak(
        double[]? queueSeries,
        int startBin,
        int endBin)
    {
        if (queueSeries is null || endBin - startBin < 1)
        {
            return null;
        }

        int? bestStart = null;
        int? bestEnd = null;
        var bestLength = 0;
        int? currentStart = null;
        var currentLength = 0;

        for (var i = startBin + 1; i <= endBin; i++)
        {
            var previous = SampleSeries(queueSeries, i - 1);
            var current = SampleSeries(queueSeries, i);

            if (previous.HasValue && current.HasValue && current.Value > previous.Value)
            {
                if (currentLength == 0)
                {
                    currentStart = i - 1;
                }

                currentLength++;
                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestStart = currentStart;
                    bestEnd = i;
                }
            }
            else
            {
                currentLength = 0;
                currentStart = null;
            }
        }

        return bestLength > 0 && bestStart.HasValue && bestEnd.HasValue
            ? (bestStart.Value, bestEnd.Value, bestLength)
            : null;
    }

    private static (int Start, int End, int Length)? FindOverloadStreak(
        NodeSemantics semantics,
        NodeData data,
        int startBin,
        int endBin)
    {
        if (data.Arrivals is null || data.Capacity is null)
        {
            return null;
        }

        return FindStreak(startBin, endBin, bin =>
        {
            var arrivals = SampleSeries(data.Arrivals, bin);
            var capacity = ComputeCapacityBin(semantics, data, bin).EffectiveCapacity;
            if (!arrivals.HasValue || !capacity.HasValue || capacity.Value <= 0d)
            {
                return false;
            }

            return arrivals.Value / capacity.Value > 1d;
        });
    }

    private static (int Start, int End, int Length)? FindAgeRiskStreak(
        double[]? queueSeries,
        double[]? servedSeries,
        double binMinutes,
        double? slaMinutes,
        int startBin,
        int endBin)
    {
        if (!slaMinutes.HasValue || slaMinutes.Value <= 0d || queueSeries is null || servedSeries is null || binMinutes <= 0d)
        {
            return null;
        }

        var threshold = slaMinutes.Value;
        return FindStreak(startBin, endBin, bin =>
        {
            var queue = SampleSeries(queueSeries, bin);
            var served = SampleSeries(servedSeries, bin);
            if (!queue.HasValue || !served.HasValue)
            {
                return false;
            }

            var latency = ComputeLatencyMinutes(queue.Value, served.Value, binMinutes * 60_000.0);
            return latency.HasValue && latency.Value > threshold;
        });
    }

    private static (int Start, int End, int Length)? FindStreak(
        int startBin,
        int endBin,
        Func<int, bool> predicate)
    {
        int? bestStart = null;
        int? bestEnd = null;
        var bestLength = 0;
        int? currentStart = null;
        var currentLength = 0;

        for (var i = startBin; i <= endBin; i++)
        {
            if (predicate(i))
            {
                if (currentLength == 0)
                {
                    currentStart = i;
                }

                currentLength++;
                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestStart = currentStart;
                    bestEnd = i;
                }
            }
            else
            {
                currentLength = 0;
                currentStart = null;
            }
        }

        return bestLength > 0 && bestStart.HasValue && bestEnd.HasValue
            ? (bestStart.Value, bestEnd.Value, bestLength)
            : null;
    }

    private static double? SampleSeries(double[]? series, int index)
    {
        if (series is null || index < 0 || index >= series.Length)
        {
            return null;
        }

        var value = series[index];
        return IsFinite(value) ? value : null;
    }

    private static IReadOnlyList<ClassEntry<AnalyticalClassResult>>? ComputeClassBins(
        RuntimeAnalyticalDescriptor descriptor,
        IReadOnlyList<ClassEntry<ClassMetricsSnapshot>>? classEntries,
        double binMs)
    {
        if (classEntries is null || classEntries.Count == 0)
        {
            return null;
        }

        var hasSpecificClasses = classEntries.Any(entry => entry.Kind == ClassEntryKind.Specific);
        var results = new List<ClassEntry<AnalyticalClassResult>>(classEntries.Count);

        foreach (var entry in classEntries)
        {
            if (entry.Kind == ClassEntryKind.Fallback && hasSpecificClasses)
            {
                continue;
            }

            var snapshot = entry.Payload;
            var analytical = ComputeClassBin(descriptor, snapshot, binMs);
            var payload = new AnalyticalClassResult
            {
                Arrivals = snapshot.Arrivals,
                Served = snapshot.Served,
                Errors = snapshot.Errors,
                Queue = snapshot.Queue,
                Capacity = snapshot.Capacity,
                ProcessingTimeMsSum = snapshot.ProcessingTimeMsSum,
                ServedCount = snapshot.ServedCount,
                QueueTimeMs = analytical.QueueTimeMs,
                ServiceTimeMs = analytical.ServiceTimeMs,
                CycleTimeMs = analytical.CycleTimeMs,
                FlowEfficiency = analytical.FlowEfficiency,
            };

            results.Add(entry.Kind == ClassEntryKind.Fallback
                ? ClassEntry<AnalyticalClassResult>.Fallback(payload)
                : ClassEntry<AnalyticalClassResult>.Specific(entry.ClassId!, payload));
        }

        return results.Count == 0 ? null : results;
    }

    private static IReadOnlyList<ClassEntry<AnalyticalClassWindowResult>>? ComputeClassWindows(
        RuntimeAnalyticalDescriptor descriptor,
        IReadOnlyList<ClassEntry<NodeClassData>> classEntries,
        int startBin,
        int count,
        double binMs)
    {
        if (classEntries.Count == 0)
        {
            return null;
        }

        var hasSpecificClasses = classEntries.Any(entry => entry.Kind == ClassEntryKind.Specific);
        var results = new List<ClassEntry<AnalyticalClassWindowResult>>(classEntries.Count);

        foreach (var entry in classEntries)
        {
            if (entry.Kind == ClassEntryKind.Fallback && hasSpecificClasses)
            {
                continue;
            }

            var classData = entry.Payload ?? new NodeClassData();
            var analytical = ComputeClassWindow(descriptor, classData, startBin, count, binMs);
            var hasCycleTime = HasAnyValues(analytical.CycleTimeMs);
            var payload = new AnalyticalClassWindowResult
            {
                Arrivals = SliceSeries(classData.Arrivals, startBin, count),
                Served = SliceSeries(classData.Served, startBin, count),
                Errors = SliceSeries(classData.Errors, startBin, count),
                Queue = SliceSeries(classData.QueueDepth, startBin, count),
                Capacity = SliceSeries(classData.Capacity, startBin, count),
                ProcessingTimeMsSum = SliceSeries(classData.ProcessingTimeMsSum, startBin, count),
                ServedCount = SliceSeries(classData.ServedCount, startBin, count),
                QueueTimeMs = hasCycleTime && descriptor.HasQueueSemantics ? analytical.QueueTimeMs : null,
                ServiceTimeMs = hasCycleTime && descriptor.HasServiceSemantics ? analytical.ServiceTimeMs : null,
                CycleTimeMs = hasCycleTime ? analytical.CycleTimeMs : null,
                FlowEfficiency = hasCycleTime && descriptor.HasServiceSemantics ? analytical.FlowEfficiency : null,
            };

            if (!payload.HasAnySeries())
            {
                continue;
            }

            results.Add(entry.Kind == ClassEntryKind.Fallback
                ? ClassEntry<AnalyticalClassWindowResult>.Fallback(payload)
                : ClassEntry<AnalyticalClassWindowResult>.Specific(entry.ClassId!, payload));
        }

        return results.Count == 0 ? null : results;
    }

    private static AnalyticalEmissionTruth BuildBinEmission(
        RuntimeAnalyticalDescriptor descriptor,
        NodeData data,
        AnalyticalResult result,
        double? flowLatencyMs,
        bool emitFlowLatency)
    {
        return new AnalyticalEmissionTruth
        {
            EmitLatencyMinutes = descriptor.HasQueueSemantics && data.QueueDepth is not null && data.Served is not null,
            EmitServiceTimeMs = descriptor.HasServiceSemantics && data.ProcessingTimeMsSum is not null && data.ServedCount is not null,
            EmitQueueTimeMs = descriptor.HasQueueSemantics && result.QueueTimeMs.HasValue,
            EmitCycleTimeMs = result.CycleTimeMs.HasValue,
            EmitFlowEfficiency = descriptor.HasServiceSemantics && result.FlowEfficiency.HasValue,
            EmitUtilization = data.Capacity is not null,
            EmitFlowLatencyMs = emitFlowLatency,
            HasAnyQueueTimeValue = descriptor.HasQueueSemantics && result.QueueTimeMs.HasValue,
            HasAnyCycleTimeValue = result.CycleTimeMs.HasValue,
        };
    }

    private static AnalyticalEmissionTruth BuildWindowEmission(
        RuntimeAnalyticalDescriptor descriptor,
        NodeData data,
        AnalyticalWindowResult result,
        double?[]? flowLatencyMs)
    {
        var hasCycleTime = HasAnyValues(result.CycleTimeMs);
        var hasQueueTime = descriptor.HasQueueSemantics && HasAnyValues(result.QueueTimeMs);
        var hasFlowEfficiency = descriptor.HasServiceSemantics && HasAnyValues(result.FlowEfficiency);

        return new AnalyticalEmissionTruth
        {
            EmitLatencyMinutes = descriptor.HasQueueSemantics && data.QueueDepth is not null && data.Served is not null,
            EmitServiceTimeMs = descriptor.HasServiceSemantics && data.ProcessingTimeMsSum is not null && data.ServedCount is not null,
            EmitQueueTimeMs = hasQueueTime,
            EmitCycleTimeMs = hasCycleTime,
            EmitFlowEfficiency = hasFlowEfficiency,
            EmitUtilization = data.Capacity is not null,
            EmitFlowLatencyMs = flowLatencyMs is not null,
            HasAnyQueueTimeValue = hasQueueTime,
            HasAnyCycleTimeValue = hasCycleTime,
        };
    }

    private static Dictionary<string, List<(string Pred, double?[] Flow)>> BuildIncomingFlowEdges(
        Topology topology,
        IReadOnlyDictionary<string, double?[]> edgeFlowById)
    {
        var incoming = new Dictionary<string, List<(string Pred, double?[] Flow)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in topology.Edges)
        {
            if (!IsFlowLatencyEdge(edge))
            {
                continue;
            }

            var edgeId = string.IsNullOrWhiteSpace(edge.Id)
                ? $"{edge.Source}->{edge.Target}"
                : edge.Id!;
            if (!edgeFlowById.TryGetValue(edgeId, out var flow))
            {
                continue;
            }

            var sourceId = ExtractNodeReference(edge.Source);
            var targetId = ExtractNodeReference(edge.Target);
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            if (!incoming.TryGetValue(targetId, out var entries))
            {
                entries = new List<(string Pred, double?[] Flow)>();
                incoming[targetId] = entries;
            }

            entries.Add((sourceId, flow));
        }

        return incoming;
    }

    private static IReadOnlyList<Node> OrderNodesTopologically(Topology topology)
    {
        var nodeById = topology.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var incomingCounts = topology.Nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var outgoing = topology.Nodes.ToDictionary(node => node.Id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var edge in topology.Edges)
        {
            if (!IsFlowLatencyEdge(edge))
            {
                continue;
            }

            var sourceId = ExtractNodeReference(edge.Source);
            var targetId = ExtractNodeReference(edge.Target);
            if (!nodeById.ContainsKey(sourceId) || !nodeById.ContainsKey(targetId))
            {
                continue;
            }

            outgoing[sourceId].Add(targetId);
            incomingCounts[targetId] += 1;
        }

        var queue = new Queue<Node>(topology.Nodes.Where(node => incomingCounts[node.Id] == 0));
        var ordered = new List<Node>(topology.Nodes.Count);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            ordered.Add(node);

            foreach (var targetId in outgoing[node.Id])
            {
                incomingCounts[targetId] -= 1;
                if (incomingCounts[targetId] == 0)
                {
                    queue.Enqueue(nodeById[targetId]);
                }
            }
        }

        return ordered.Count == topology.Nodes.Count ? ordered : topology.Nodes;
    }

    private static bool IsFlowLatencyEdge(Edge edge)
    {
        var edgeType = string.IsNullOrWhiteSpace(edge.EdgeType)
            ? "throughput"
            : edge.EdgeType.Trim().ToLowerInvariant();
        return edgeType is "throughput" or "effort" or "dependency";
    }

    private static string ExtractNodeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return string.Empty;
        }

        var separator = reference.IndexOf(':');
        return separator < 0 ? reference : reference[..separator];
    }

    private static int ResolveSeriesLength(
        IReadOnlyDictionary<string, double?[]> cycleTimeByNode,
        IReadOnlyDictionary<string, double?[]> edgeFlowById,
        IReadOnlyDictionary<string, NodeData> nodeData)
    {
        foreach (var series in cycleTimeByNode.Values)
        {
            return series.Length;
        }

        foreach (var series in edgeFlowById.Values)
        {
            return series.Length;
        }

        foreach (var data in nodeData.Values)
        {
            return data.Arrivals.Length;
        }

        return 0;
    }

    private static double?[] CreateNullSeries(int length)
    {
        return new double?[length];
    }

    private static double?[]? SliceSeries(double[]? source, int startBin, int count)
    {
        if (source is null)
        {
            return null;
        }

        var slice = new double?[count];
        var hasValue = false;
        for (var i = 0; i < count; i++)
        {
            var index = startBin + i;
            if (index < 0 || index >= source.Length)
            {
                continue;
            }

            var value = Sanitize(source[index]);
            slice[i] = value;
            hasValue |= value.HasValue;
        }

        return hasValue ? slice : null;
    }

    private static double?[]? SanitizeSeries(double?[]? source)
    {
        if (source is null)
        {
            return null;
        }

        var result = new double?[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            result[i] = Sanitize(source[i]);
        }

        return result;
    }

    private static bool HasAnyValues(double?[]? series)
    {
        if (series is null)
        {
            return false;
        }

        for (var i = 0; i < series.Length; i++)
        {
            if (series[i].HasValue)
            {
                return true;
            }
        }

        return false;
    }

    private static double? SampleParallelism(NodeSemantics semantics, NodeData data, int index)
    {
        if (data.Parallelism is not null && index >= 0 && index < data.Parallelism.Length)
        {
            var value = data.Parallelism[index];
            return IsFinite(value) ? value : null;
        }

        var constant = semantics.Parallelism?.Constant;
        return constant.HasValue && IsFinite(constant.Value) && constant.Value > 0d
            ? constant.Value
            : null;
    }

    private static double? ComputeEffectiveCapacity(double? baseCapacity, double? parallelism)
    {
        if (!baseCapacity.HasValue)
        {
            return null;
        }

        if (!parallelism.HasValue || !IsFinite(parallelism.Value) || parallelism.Value <= 0d)
        {
            return baseCapacity.Value;
        }

        return baseCapacity.Value * parallelism.Value;
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static double? Sanitize(double? value) =>
        value.HasValue && IsFinite(value.Value) ? value : null;

    private static double? Round(double? value) =>
        value.HasValue ? Math.Round(value.Value, 6, MidpointRounding.AwayFromZero) : null;
}

public sealed record AnalyticalResult
{
    public double? QueueTimeMs { get; init; }
    public double? ServiceTimeMs { get; init; }
    public double? CycleTimeMs { get; init; }
    public double? FlowEfficiency { get; init; }
    public double? LatencyMinutes { get; init; }
    public AnalyticalCapacityResult? Capacity { get; init; }
    public AnalyticalEmissionTruth Emission { get; init; } = new();
    public IReadOnlyList<ClassEntry<AnalyticalClassResult>>? ByClass { get; init; }
    public double? FlowLatencyMs { get; init; }
    public AnalyticalWindowWarningFacts WarningFacts { get; init; } = new();
}

public sealed record AnalyticalWindowResult
{
    public required double?[] QueueTimeMs { get; init; }
    public required double?[] ServiceTimeMs { get; init; }
    public required double?[] CycleTimeMs { get; init; }
    public required double?[] FlowEfficiency { get; init; }
    public required double?[] LatencyMinutes { get; init; }
    public AnalyticalCapacityWindowResult? Capacity { get; init; }
    public AnalyticalEmissionTruth Emission { get; init; } = new();
    public IReadOnlyList<ClassEntry<AnalyticalClassWindowResult>>? ByClass { get; init; }
    public double?[]? FlowLatencyMs { get; init; }
    public AnalyticalWindowWarningFacts WarningFacts { get; init; } = new();
}

public sealed record AnalyticalWindowWarningFacts
{
    public bool NonStationary { get; init; }
    public AnalyticalStreakWarningFact? BacklogGrowth { get; init; }
    public AnalyticalStreakWarningFact? Overload { get; init; }
    public AnalyticalStreakWarningFact? AgeRisk { get; init; }
}

public sealed record AnalyticalStreakWarningFact
{
    public required int StartBin { get; init; }
    public required int EndBin { get; init; }
    public required int Length { get; init; }
}

public sealed record AnalyticalCapacityResult
{
    public double? BaseCapacity { get; init; }
    public double? Parallelism { get; init; }
    public double? EffectiveCapacity { get; init; }
    public double? Utilization { get; init; }
}

public sealed record AnalyticalCapacityWindowResult
{
    public required double?[] BaseCapacity { get; init; }
    public required double?[] Parallelism { get; init; }
    public required double?[] EffectiveCapacity { get; init; }
    public required double?[] Utilization { get; init; }
}

public sealed record AnalyticalEmissionTruth
{
    public bool EmitLatencyMinutes { get; init; }
    public bool EmitServiceTimeMs { get; init; }
    public bool EmitQueueTimeMs { get; init; }
    public bool EmitCycleTimeMs { get; init; }
    public bool EmitFlowEfficiency { get; init; }
    public bool EmitUtilization { get; init; }
    public bool EmitFlowLatencyMs { get; init; }
    public bool HasAnyQueueTimeValue { get; init; }
    public bool HasAnyCycleTimeValue { get; init; }
}

public sealed record AnalyticalClassResult
{
    public double? Arrivals { get; init; }
    public double? Served { get; init; }
    public double? Errors { get; init; }
    public double? Queue { get; init; }
    public double? Capacity { get; init; }
    public double? ProcessingTimeMsSum { get; init; }
    public double? ServedCount { get; init; }
    public double? QueueTimeMs { get; init; }
    public double? ServiceTimeMs { get; init; }
    public double? CycleTimeMs { get; init; }
    public double? FlowEfficiency { get; init; }
}

public sealed record AnalyticalClassWindowResult
{
    public double?[]? Arrivals { get; init; }
    public double?[]? Served { get; init; }
    public double?[]? Errors { get; init; }
    public double?[]? Queue { get; init; }
    public double?[]? Capacity { get; init; }
    public double?[]? ProcessingTimeMsSum { get; init; }
    public double?[]? ServedCount { get; init; }
    public double?[]? QueueTimeMs { get; init; }
    public double?[]? ServiceTimeMs { get; init; }
    public double?[]? CycleTimeMs { get; init; }
    public double?[]? FlowEfficiency { get; init; }

    public bool HasAnySeries() =>
        Arrivals is not null || Served is not null || Errors is not null || Queue is not null || Capacity is not null ||
        ProcessingTimeMsSum is not null || ServedCount is not null || QueueTimeMs is not null || ServiceTimeMs is not null ||
        CycleTimeMs is not null || FlowEfficiency is not null;
}