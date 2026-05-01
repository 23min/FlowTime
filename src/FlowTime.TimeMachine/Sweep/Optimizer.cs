namespace FlowTime.TimeMachine.Sweep;

/// <summary>
/// Finds the set of const-node parameter values that minimize or maximize a metric series mean
/// using the Nelder-Mead simplex algorithm.
/// </summary>
public sealed class Optimizer
{
    // Nelder-Mead standard coefficients
    private const double Alpha = 1.0;  // reflection
    private const double Gamma = 2.0;  // expansion
    private const double Rho   = 0.5;  // contraction
    private const double Sigma  = 0.5; // shrink

    private readonly IModelEvaluator evaluator;

    public Optimizer(IModelEvaluator evaluator)
    {
        this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    /// <summary>
    /// Run Nelder-Mead optimization and return the best parameter values found.
    /// </summary>
    public async Task<OptimizeResult> OptimizeAsync(
        OptimizeSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var n = spec.ParamIds.Count;
        var paramIds = spec.ParamIds;

        // Sign: internally always minimize. Maximize → negate metric.
        double sign = spec.Objective == OptimizeObjective.Minimize ? 1.0 : -1.0;

        // Trace buffer — bounded by 1 (pre-loop) + MaxIterations (per-iteration) entries
        // per D-2026-04-21-034. Pre-size to the upper bound to avoid List growth.
        var trace = new List<OptimizeTracePoint>(capacity: spec.MaxIterations + 1);

        // ── Build initial simplex (N+1 vertices) ──────────────────────────
        // v[0] = midpoint of all search ranges
        // v[i] = v[0] perturbed +5% of range in dimension i-1
        var simplex = new double[n + 1][];
        var midpoint = new double[n];
        for (var d = 0; d < n; d++)
        {
            var r = spec.SearchRanges[paramIds[d]];
            midpoint[d] = (r.Lo + r.Hi) / 2.0;
        }

        simplex[0] = (double[])midpoint.Clone();
        for (var i = 1; i <= n; i++)
        {
            var v = (double[])midpoint.Clone();
            var r = spec.SearchRanges[paramIds[i - 1]];
            v[i - 1] = Clamp(v[i - 1] + 0.05 * (r.Hi - r.Lo), r.Lo, r.Hi);
            simplex[i] = v;
        }

        // ── Evaluate initial simplex ──────────────────────────────────────
        var fValues = new double[n + 1];
        var metricMeans = new double[n + 1];
        for (var i = 0; i <= n; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (fValues[i], metricMeans[i]) = await EvaluateAsync(spec, simplex[i], sign, cancellationToken)
                .ConfigureAwait(false);
        }

        // ── Sort ──────────────────────────────────────────────────────────
        Sort(simplex, fValues, metricMeans);

        // Record the post-pre-loop-sort best vertex as iteration: 0.
        // metricMeans[0] is unsigned (see EvaluateAsync) so maximize runs still
        // report unsigned metricMean here.
        trace.Add(MakeTracePoint(0, simplex[0], paramIds, metricMeans[0]));

        // ── Pre-loop convergence check (0 iterations) ─────────────────────
        if (fValues[n] - fValues[0] < spec.Tolerance)
            return MakeResult(simplex[0], paramIds, metricMeans[0], converged: true, iterations: 0, trace);

        // ── Nelder-Mead loop ──────────────────────────────────────────────
        for (var iteration = 1; iteration <= spec.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Centroid of best N vertices (exclude worst = simplex[N])
            var centroid = Centroid(simplex, n);

            // Reflection
            var xr = Clamp(spec, paramIds, ReflectExpand(centroid, simplex[n], Alpha));
            var (fr, mr) = await EvaluateAsync(spec, xr, sign, cancellationToken).ConfigureAwait(false);

            bool shrink = false;

            if (fr < fValues[0])
            {
                // Better than best — try expansion
                var xe = Clamp(spec, paramIds, ReflectExpand(centroid, simplex[n], -Gamma));
                var (fe, me) = await EvaluateAsync(spec, xe, sign, cancellationToken).ConfigureAwait(false);

                if (fe < fr)
                    Replace(simplex, fValues, metricMeans, n, xe, fe, me);
                else
                    Replace(simplex, fValues, metricMeans, n, xr, fr, mr);
            }
            else if (fr < fValues[n - 1])
            {
                // Better than second-worst — accept reflection
                Replace(simplex, fValues, metricMeans, n, xr, fr, mr);
            }
            else
            {
                // Contraction
                if (fr < fValues[n])
                {
                    // Outside contraction
                    var xoc = Clamp(spec, paramIds, ReflectExpand(centroid, simplex[n], -Rho));
                    var (foc, moc) = await EvaluateAsync(spec, xoc, sign, cancellationToken).ConfigureAwait(false);

                    if (foc <= fr)
                        Replace(simplex, fValues, metricMeans, n, xoc, foc, moc);
                    else
                        shrink = true;
                }
                else
                {
                    // Inside contraction
                    var xic = Clamp(spec, paramIds, Interpolate(centroid, simplex[n], Rho));
                    var (fic, mic) = await EvaluateAsync(spec, xic, sign, cancellationToken).ConfigureAwait(false);

                    if (fic < fValues[n])
                        Replace(simplex, fValues, metricMeans, n, xic, fic, mic);
                    else
                        shrink = true;
                }
            }

            if (shrink)
            {
                for (var i = 1; i <= n; i++)
                {
                    simplex[i] = Clamp(spec, paramIds, Interpolate(simplex[0], simplex[i], Sigma));
                    (fValues[i], metricMeans[i]) = await EvaluateAsync(spec, simplex[i], sign, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            Sort(simplex, fValues, metricMeans);

            // Record the post-iteration-sort best vertex as iteration: 1..N.
            trace.Add(MakeTracePoint(iteration, simplex[0], paramIds, metricMeans[0]));

            if (fValues[n] - fValues[0] < spec.Tolerance)
                return MakeResult(simplex[0], paramIds, metricMeans[0], converged: true, iterations: iteration, trace);

            if (iteration == spec.MaxIterations)
                return MakeResult(simplex[0], paramIds, metricMeans[0], converged: false, iterations: iteration, trace);
        }

        // Unreachable — MaxIterations >= 1 enforced by spec
        return MakeResult(simplex[0], paramIds, metricMeans[0], converged: false, iterations: spec.MaxIterations, trace);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<(double FValue, double MetricMean)> EvaluateAsync(
        OptimizeSpec spec,
        double[] vertex,
        double sign,
        CancellationToken cancellationToken)
    {
        var yaml = spec.ModelYaml;
        for (var d = 0; d < spec.ParamIds.Count; d++)
            yaml = ConstNodePatcher.Patch(yaml, spec.ParamIds[d], vertex[d]);

        var series = await evaluator.EvaluateAsync(yaml, cancellationToken).ConfigureAwait(false);

        double metricMean = 0.0;
        if (series.TryGetValue(spec.MetricSeriesId, out var values) && values.Length > 0)
            metricMean = values.Average();

        return (sign * metricMean, metricMean);
    }

    /// <summary>Compute centroid of the best N vertices (excludes the worst = simplex[N]).</summary>
    private static double[] Centroid(double[][] simplex, int n)
    {
        var c = new double[n];
        for (var d = 0; d < n; d++)
        {
            double sum = 0;
            for (var i = 0; i < n; i++) sum += simplex[i][d];
            c[d] = sum / n;
        }
        return c;
    }

    /// <summary>
    /// Reflect/expand: c + coeff*(c - worst) where coeff=α for reflect, coeff=-γ for expand.
    /// Also handles contraction via negative coeff when using ReflectExpand with -ρ.
    /// </summary>
    private static double[] ReflectExpand(double[] centroid, double[] worst, double coeff)
    {
        var result = new double[centroid.Length];
        for (var d = 0; d < centroid.Length; d++)
            result[d] = centroid[d] + coeff * (centroid[d] - worst[d]);
        return result;
    }

    /// <summary>Interpolate: base + t*(other - base), used for contraction and shrink.</summary>
    private static double[] Interpolate(double[] baseVertex, double[] other, double t)
    {
        var result = new double[baseVertex.Length];
        for (var d = 0; d < baseVertex.Length; d++)
            result[d] = baseVertex[d] + t * (other[d] - baseVertex[d]);
        return result;
    }

    /// <summary>Clamp all dimensions of a vertex to their respective search ranges.</summary>
    private static double[] Clamp(OptimizeSpec spec, IReadOnlyList<string> paramIds, double[] v)
    {
        var result = (double[])v.Clone();
        for (var d = 0; d < paramIds.Count; d++)
        {
            var r = spec.SearchRanges[paramIds[d]];
            result[d] = Clamp(result[d], r.Lo, r.Hi);
        }
        return result;
    }

    private static double Clamp(double v, double lo, double hi) =>
        v < lo ? lo : v > hi ? hi : v;

    /// <summary>Sort simplex ascending by f-value (best first).</summary>
    private static void Sort(double[][] simplex, double[] fValues, double[] metricMeans)
    {
        var n = simplex.Length;
        // Simple insertion sort — simplex is small (N+1 elements)
        for (var i = 1; i < n; i++)
        {
            var kf = fValues[i];
            var km = metricMeans[i];
            var kv = simplex[i];
            var j = i - 1;
            while (j >= 0 && fValues[j] > kf)
            {
                fValues[j + 1] = fValues[j];
                metricMeans[j + 1] = metricMeans[j];
                simplex[j + 1] = simplex[j];
                j--;
            }
            fValues[j + 1] = kf;
            metricMeans[j + 1] = km;
            simplex[j + 1] = kv;
        }
    }

    private static void Replace(
        double[][] simplex, double[] fValues, double[] metricMeans,
        int idx, double[] v, double f, double m)
    {
        simplex[idx] = v;
        fValues[idx] = f;
        metricMeans[idx] = m;
    }

    /// <summary>
    /// Snapshot a best vertex into an <see cref="OptimizeTracePoint"/>. <paramref name="metricMean"/>
    /// must be the <b>unsigned</b> user-space metric (not the internal sign-flipped f-value).
    /// </summary>
    private static OptimizeTracePoint MakeTracePoint(
        int iteration,
        double[] vertex,
        IReadOnlyList<string> paramIds,
        double metricMean)
    {
        var paramValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var d = 0; d < paramIds.Count; d++)
            paramValues[paramIds[d]] = vertex[d];

        return new OptimizeTracePoint(iteration, paramValues, metricMean);
    }

    private static OptimizeResult MakeResult(
        double[] bestVertex,
        IReadOnlyList<string> paramIds,
        double metricMean,
        bool converged,
        int iterations,
        List<OptimizeTracePoint> trace)
    {
        var paramValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var d = 0; d < paramIds.Count; d++)
            paramValues[paramIds[d]] = bestVertex[d];

        return new OptimizeResult
        {
            ParamValues = paramValues,
            AchievedMetricMean = metricMean,
            Converged = converged,
            Iterations = iterations,
            Trace = trace,
        };
    }
}
