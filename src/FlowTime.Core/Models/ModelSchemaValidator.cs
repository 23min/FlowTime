using System.Text.Json.Nodes;
using System.Text.Json;
using System.Globalization;
using FlowTime.Core.Configuration;
using FlowTime.Expressions;
using Json.Schema;
using YamlDotNet.RepresentationModel;

namespace FlowTime.Core;

/// <summary>
/// Validates model YAML against the published JSON schema and enforces class references.
/// </summary>
public static class ModelSchemaValidator
{
    private static readonly Lazy<JsonSchema?> schema = new(LoadSchema);
    private static string? schemaLoadError;

    /// <summary>
    /// Validate a model YAML document using the canonical schema and class rules.
    /// </summary>
    public static ValidationResult Validate(string yaml)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(yaml))
        {
            errors.Add("Model YAML cannot be null or empty.");
            return new ValidationResult(errors);
        }

        var schemaInstance = schema.Value;
        if (schemaInstance is null)
        {
            errors.Add(schemaLoadError is not null
                ? $"Model schema could not be loaded for validation: {schemaLoadError}"
                : "Model schema could not be loaded for validation.");
            return new ValidationResult(errors);
        }

        try
        {
            var json = ConvertYamlToJson(yaml);
            var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Parsed JSON was null.");

            var evaluation = schemaInstance.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
            if (!evaluation.IsValid)
            {
                var collectedBefore = errors.Count;
                errors.AddRange(CollectErrors(evaluation));
                if (errors.Count == collectedBefore)
                {
                    // Silent-error fallback (m-E23-01 / D3): JsonEverything keywords like
                    // `not`, `oneOf`-no-arm-match, and deep `allOf` failures can mark a
                    // subtree invalid without populating any leaf `Errors` entry. Without
                    // this fallback the validator returns IsValid==false with an empty
                    // Errors list, which is the silent-error class D3 found in the canary.
                    // Synthesize a path-only diagnostic so an invalid evaluation always
                    // produces at least one human-readable message.
                    errors.Add(SynthesizePathOnlyError(evaluation));
                }
            }

            errors.AddRange(ValidateClassReferences(node));

            // m-E23-01 / AC4 — cross-reference, cross-array and format adjuncts.
            // JSON Schema draft-07 cannot express these; they sit alongside
            // ValidateClassReferences as named methods that are additive — each appends
            // to the running error list. Each adjunct is defensive against earlier
            // failures (e.g. a missing nodes[] block) so a single broken model does not
            // crash the whole validation pass — partial information remains useful.
            errors.AddRange(ValidateNodeIdUniqueness(node));
            errors.AddRange(ValidateOutputSeriesReferences(node));
            errors.AddRange(ValidateExpressionNodeReferences(node));
            errors.AddRange(ValidateConstNodeValueCount(node));
            errors.AddRange(ValidatePmfArrayLengths(node));
            errors.AddRange(ValidatePmfValueUniqueness(node));
            errors.AddRange(ValidatePmfProbabilitySum(node));
            errors.AddRange(ValidateSelfShiftRequiresInitialCondition(node));
            errors.AddRange(ValidateTopologySeriesReferences(node));
            errors.AddRange(ValidateWipOverflowTarget(node));
            errors.AddRange(ValidateWipOverflowAcyclic(node));
            errors.AddRange(ValidateDateTimeFormats(node));
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            errors.Add($"Invalid YAML syntax: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Validation error: {ex.Message}");
        }

        return new ValidationResult(errors);
    }

    private static JsonSchema? LoadSchema()
    {
        try
        {
            var solutionRoot = DirectoryProvider.FindSolutionRoot();
            if (string.IsNullOrWhiteSpace(solutionRoot))
            {
                schemaLoadError = "Solution root not found.";
                return null;
            }

            var schemaPath = Path.Combine(solutionRoot, "docs", "schemas", "model.schema.yaml");
            if (!File.Exists(schemaPath))
            {
                schemaLoadError = $"Schema file not found at {schemaPath}.";
                return null;
            }

            var yaml = File.ReadAllText(schemaPath);
            var json = ConvertYamlToJson(yaml);
            var schemaNode = JsonNode.Parse(json) ?? throw new InvalidDataException("Schema JSON could not be parsed.");
            if (schemaNode is JsonObject schemaObj)
            {
                schemaObj.Remove("$schema");
            }
            json = schemaNode.ToJsonString();
            return JsonSchema.FromText(json);
        }
        catch (Exception ex)
        {
            schemaLoadError = ex.ToString();
            return null;
        }
    }

    private static IEnumerable<string> CollectErrors(EvaluationResults results)
    {
        if (results.IsValid)
        {
            yield break;
        }

        if (results.Errors is { Count: > 0 } errors)
        {
            foreach (var error in errors)
            {
                yield return $"{results.InstanceLocation}: {error.Value}";
            }
        }

        foreach (var detail in results.Details)
        {
            foreach (var message in CollectErrors(detail))
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// Synthesize a path-only diagnostic from an invalid <see cref="EvaluationResults"/>
    /// when no node in the tree populated a textual error. Picks the deepest invalid node
    /// (by <see cref="EvaluationResults.EvaluationPath"/> segment count, ties broken by
    /// <see cref="EvaluationResults.InstanceLocation"/> segment count) so the message
    /// points at the most specific schema rule that failed. The choice is deterministic:
    /// repeated calls on the same <see cref="EvaluationResults"/> always return the same
    /// string. Format: <c>{instance}: schema rule failed at {path}</c>.
    /// </summary>
    private static string SynthesizePathOnlyError(EvaluationResults results)
    {
        var deepest = WalkForDeepestInvalid(results) ?? results;
        var instance = deepest.InstanceLocation.ToString();
        var path = deepest.EvaluationPath.ToString();
        return $"{instance}: schema rule failed at {path}";
    }

    /// <summary>
    /// Recursive descent over <see cref="EvaluationResults.Details"/>. Returns the deepest
    /// invalid node, where "deepest" is measured by EvaluationPath segment count (with
    /// InstanceLocation segment count as a stable tiebreaker). Returns <c>null</c> when
    /// no descendant is invalid; the caller should fall back to the root.
    /// </summary>
    private static EvaluationResults? WalkForDeepestInvalid(EvaluationResults results)
    {
        EvaluationResults? best = results.IsValid ? null : results;
        foreach (var detail in results.Details)
        {
            var candidate = WalkForDeepestInvalid(detail);
            if (candidate is null)
            {
                continue;
            }
            if (best is null || IsDeeperThan(candidate, best))
            {
                best = candidate;
            }
        }
        return best;
    }

    private static bool IsDeeperThan(EvaluationResults a, EvaluationResults b)
    {
        var aPath = a.EvaluationPath.Segments.Length;
        var bPath = b.EvaluationPath.Segments.Length;
        if (aPath != bPath)
        {
            return aPath > bPath;
        }
        return a.InstanceLocation.Segments.Length > b.InstanceLocation.Segments.Length;
    }

    /// <summary>
    /// Test-only entry point exposing <see cref="CollectErrors(EvaluationResults)"/> for
    /// the silent-error regression tests in <c>FlowTime.Core.Tests</c>. Visible via
    /// <c>InternalsVisibleTo("FlowTime.Core.Tests")</c>; not part of the public surface.
    /// </summary>
    internal static IEnumerable<string> CollectErrorsForTests(EvaluationResults results)
        => CollectErrors(results);

    /// <summary>
    /// Test-only entry point exposing <see cref="SynthesizePathOnlyError(EvaluationResults)"/>
    /// for the silent-error regression tests in <c>FlowTime.Core.Tests</c>. Visible via
    /// <c>InternalsVisibleTo("FlowTime.Core.Tests")</c>; not part of the public surface.
    /// </summary>
    internal static string SynthesizePathOnlyErrorForTests(EvaluationResults results)
        => SynthesizePathOnlyError(results);

    private static IEnumerable<string> ValidateClassReferences(JsonNode node)
    {
        var classes = node["classes"] as JsonArray;
        var classIds = new HashSet<string>(StringComparer.Ordinal);

        if (classes is not null)
        {
            foreach (var classNode in classes)
            {
                var id = classNode?["id"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    classIds.Add(id);
                }
            }
        }

        var arrivals = node["traffic"]?["arrivals"] as JsonArray;
        if (arrivals is null || arrivals.Count == 0)
        {
            yield break;
        }

        foreach (var arrivalNode in arrivals)
        {
            var nodeId = arrivalNode?["nodeId"]?.GetValue<string>() ?? "<unknown>";
            var classId = arrivalNode?["classId"]?.GetValue<string>();

            if (classIds.Count == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(classId))
            {
                yield return $"Arrival targeting '{nodeId}' must declare classId because model.classes are defined.";
                continue;
            }

            if (!classIds.Contains(classId))
            {
                yield return $"Class '{classId}' is not declared under model.classes.";
            }
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    // m-E23-01 / AC4 — Cross-reference / cross-array / format adjuncts.
    //
    // Each adjunct mirrors the shape of ValidateClassReferences: takes the root
    // JsonNode parsed from the model YAML, appends errors for the rule it owns, and
    // returns nothing else. Adjuncts are defensive: a missing block produces an
    // empty result, never a throw, so the schema's structural diagnostics remain the
    // primary failure surface and the adjunct findings are additive.
    //
    // The cross-reference findings table in the audit doc
    // (work/epics/E-23-model-validation-consolidation/m-E23-01-rule-coverage-audit-tracking.md
    // → "Cross-reference findings") is the source of truth for which rule each
    // method enforces and where the imperative original lives.
    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adjunct: every <c>nodes[].id</c> must be unique. JSON Schema draft-07 cannot
    /// express array-of-object uniqueness on a single field; this is a textbook
    /// adjunct candidate. Schema notes (line 1221) document the rule but rely on
    /// this method to enforce it.
    /// </summary>
    private static IEnumerable<string> ValidateNodeIdUniqueness(JsonNode node)
    {
        var nodes = node["nodes"] as JsonArray;
        if (nodes is null || nodes.Count == 0)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < nodes.Count; i++)
        {
            var id = nodes[i]?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue; // schema's required+minLength owns the missing-id case
            }

            if (!seen.Add(id))
            {
                yield return $"/nodes/{i}: duplicate node id '{id}' (node ids must be unique within nodes[]).";
            }
        }
    }

    /// <summary>
    /// Adjunct: every <c>outputs[].series</c> must reference a declared <c>nodes[].id</c>
    /// or be the wildcard <c>*</c>. The wildcard is documented in the schema (line 791
    /// example) and consumed by <c>SimModelBuilder</c> output expansion. Schema notes
    /// (line 1226) document the rule.
    /// </summary>
    private static IEnumerable<string> ValidateOutputSeriesReferences(JsonNode node)
    {
        var outputs = node["outputs"] as JsonArray;
        if (outputs is null || outputs.Count == 0)
        {
            yield break;
        }

        var nodeIds = CollectNodeIds(node);
        for (int i = 0; i < outputs.Count; i++)
        {
            var series = outputs[i]?["series"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(series))
            {
                continue; // schema owns required+minLength on series
            }

            if (series == "*")
            {
                continue; // wildcard is the documented "all series" sigil
            }

            if (!nodeIds.Contains(series))
            {
                yield return $"/outputs/{i}: output series '{series}' does not match any declared nodes[].id.";
            }
        }
    }

    /// <summary>
    /// Adjunct: every node id referenced inside an <c>expr</c> formula must resolve
    /// to a declared <c>nodes[].id</c>. Walks the parsed AST via <see cref="ExpressionParser"/>
    /// to extract <see cref="NodeReferenceNode"/> instances; unparseable expressions
    /// are silently skipped (the schema's <c>expr.minLength: 1</c> only guards emptiness;
    /// the parser failure surfaces later via <c>ModelParser</c>). Schema notes (line 1224)
    /// document the rule.
    /// </summary>
    private static IEnumerable<string> ValidateExpressionNodeReferences(JsonNode node)
    {
        var nodes = node["nodes"] as JsonArray;
        if (nodes is null || nodes.Count == 0)
        {
            yield break;
        }

        var nodeIds = CollectNodeIds(node);
        for (int i = 0; i < nodes.Count; i++)
        {
            var entry = nodes[i];
            if (entry is null) continue;
            var kind = entry["kind"]?.GetValue<string>();
            if (!string.Equals(kind, "expr", StringComparison.Ordinal))
            {
                continue;
            }

            var expr = entry["expr"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(expr))
            {
                continue; // schema's expr.minLength owns the empty-expr case
            }

            ExpressionNode ast;
            try
            {
                ast = new ExpressionParser(expr).Parse();
            }
            catch
            {
                // Parse error is owned by ModelParser/ExpressionCompiler. Adjunct
                // does not double-report syntactic errors.
                continue;
            }

            var nodeId = entry["id"]?.GetValue<string>() ?? "<unknown>";
            var collector = new NodeReferenceCollector();
            ast.Accept(collector);
            foreach (var referenced in collector.References)
            {
                if (!nodeIds.Contains(referenced))
                {
                    yield return $"/nodes/{i}: expression on '{nodeId}' references unknown node id '{referenced}'.";
                }
            }
        }
    }

    /// <summary>
    /// Adjunct: <c>const</c> nodes must declare <c>values</c> with length equal to
    /// <c>grid.bins</c>. Cross-array between <c>nodes[].values</c> and <c>grid.bins</c>;
    /// JSON Schema cannot express array-length-equal-to-other-property. Schema notes
    /// (line 1222) document the rule. Skipped when grid.bins is invalid (the schema's
    /// minimum:1 owns that diagnostic).
    /// </summary>
    private static IEnumerable<string> ValidateConstNodeValueCount(JsonNode node)
    {
        var nodes = node["nodes"] as JsonArray;
        if (nodes is null || nodes.Count == 0)
        {
            yield break;
        }

        if (!TryGetGridBins(node, out var bins) || bins <= 0)
        {
            yield break; // schema owns invalid bins; do not double-flag
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            var entry = nodes[i];
            if (entry is null) continue;
            var kind = entry["kind"]?.GetValue<string>();
            if (!string.Equals(kind, "const", StringComparison.Ordinal))
            {
                continue;
            }

            var values = entry["values"] as JsonArray;
            if (values is null)
            {
                continue; // schema's required+kind-arm owns the missing-values case
            }

            if (values.Count != bins)
            {
                var nodeId = entry["id"]?.GetValue<string>() ?? "<unknown>";
                yield return $"/nodes/{i}: const node '{nodeId}' values length {values.Count} does not match grid.bins {bins}.";
            }
        }
    }

    /// <summary>
    /// Adjunct: <c>pmf.values</c> and <c>pmf.probabilities</c> must have the same
    /// length. Cross-array length equality. Mirrors the imperative check at
    /// <c>ModelParser.cs:420-421</c>; the imperative check stays as defence-in-depth.
    /// </summary>
    private static IEnumerable<string> ValidatePmfArrayLengths(JsonNode node)
    {
        var nodes = node["nodes"] as JsonArray;
        if (nodes is null || nodes.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            var entry = nodes[i];
            if (entry is null) continue;
            if (!IsPmfNode(entry)) continue;

            var pmf = entry["pmf"];
            if (pmf is null) continue;
            var values = pmf["values"] as JsonArray;
            var probabilities = pmf["probabilities"] as JsonArray;
            if (values is null || probabilities is null) continue;

            if (values.Count != probabilities.Count)
            {
                var nodeId = entry["id"]?.GetValue<string>() ?? "<unknown>";
                yield return $"/nodes/{i}: pmf node '{nodeId}' probabilities length {probabilities.Count} does not match values length {values.Count}.";
            }
        }
    }

    /// <summary>
    /// Adjunct: <c>pmf.values</c> contains no duplicates. Cross-array dedup; mirrors
    /// the imperative check at <c>ModelParser.cs:431-432</c>. The imperative check
    /// stays as defence-in-depth.
    /// </summary>
    private static IEnumerable<string> ValidatePmfValueUniqueness(JsonNode node)
    {
        var nodes = node["nodes"] as JsonArray;
        if (nodes is null || nodes.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            var entry = nodes[i];
            if (entry is null) continue;
            if (!IsPmfNode(entry)) continue;

            var pmf = entry["pmf"];
            var values = pmf?["values"] as JsonArray;
            if (values is null || values.Count == 0) continue;

            var seen = new HashSet<double>();
            foreach (var v in values)
            {
                if (v is null) continue;
                double parsed;
                try { parsed = v.GetValue<double>(); }
                catch { continue; }
                if (!seen.Add(parsed))
                {
                    var nodeId = entry["id"]?.GetValue<string>() ?? "<unknown>";
                    yield return $"/nodes/{i}: pmf node '{nodeId}' contains duplicate value '{parsed}'.";
                }
            }
        }
    }

    /// <summary>
    /// Adjunct: <c>pmf.probabilities</c> must sum to <c>1.0</c> within tolerance
    /// <c>1e-4</c>. Lifted from the deep <c>Pmf.Pmf</c> ctor path so the error
    /// surfaces from the validator instead of throwing during compile. Schema notes
    /// (line 1223) document the rule.
    /// </summary>
    private static IEnumerable<string> ValidatePmfProbabilitySum(JsonNode node)
    {
        const double tolerance = 1e-4;
        var nodes = node["nodes"] as JsonArray;
        if (nodes is null || nodes.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            var entry = nodes[i];
            if (entry is null) continue;
            if (!IsPmfNode(entry)) continue;

            var pmf = entry["pmf"];
            var probabilities = pmf?["probabilities"] as JsonArray;
            if (probabilities is null || probabilities.Count == 0) continue;

            double sum = 0;
            bool ok = true;
            foreach (var p in probabilities)
            {
                if (p is null) { ok = false; break; }
                try { sum += p.GetValue<double>(); }
                catch { ok = false; break; }
            }
            if (!ok) continue;

            if (Math.Abs(sum - 1.0) > tolerance)
            {
                var nodeId = entry["id"]?.GetValue<string>() ?? "<unknown>";
                yield return $"/nodes/{i}: pmf node '{nodeId}' probabilities sum {sum.ToString("G", CultureInfo.InvariantCulture)} is not 1.0 (tolerance 1e-4).";
            }
        }
    }

    /// <summary>
    /// Adjunct: when an expression node uses <c>SHIFT(self, n)</c> with positive lag,
    /// the topology must declare an <c>initialCondition.queueDepth</c> for the same
    /// id. Cross-reference between expression AST and topology block; mirrors the
    /// imperative check at <c>ModelParser.cs:307-316</c>. Reuses
    /// <see cref="ExpressionSemanticValidator"/> to identify the self-shift pattern.
    /// </summary>
    private static IEnumerable<string> ValidateSelfShiftRequiresInitialCondition(JsonNode node)
    {
        var nodes = node["nodes"] as JsonArray;
        if (nodes is null || nodes.Count == 0)
        {
            yield break;
        }

        var initialised = CollectTopologyInitialIds(node);

        for (int i = 0; i < nodes.Count; i++)
        {
            var entry = nodes[i];
            if (entry is null) continue;
            var kind = entry["kind"]?.GetValue<string>();
            if (!string.Equals(kind, "expr", StringComparison.Ordinal))
            {
                continue;
            }

            var nodeId = entry["id"]?.GetValue<string>();
            var expr = entry["expr"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(expr))
            {
                continue;
            }

            ExpressionNode ast;
            try { ast = new ExpressionParser(expr).Parse(); }
            catch { continue; }

            var validation = ExpressionSemanticValidator.Validate(ast, nodeId);
            var selfShift = validation.Errors.FirstOrDefault(
                e => e.Code == ExpressionValidationErrorCodes.SelfShiftRequiresInitialCondition);
            if (selfShift is null)
            {
                continue;
            }

            if (!initialised.Contains(nodeId))
            {
                yield return $"/nodes/{i}: {selfShift.Message}";
            }
        }
    }

    /// <summary>
    /// Adjunct: every series binding under <c>topology.nodes[].semantics</c> must
    /// resolve to a declared <c>nodes[].id</c>. The <see cref="SemanticReferenceResolver"/>
    /// owns the syntactic forms (<c>self</c>, <c>file:</c> prefix, <c>nodeId@classId</c>);
    /// only the syntactic form that resolves to a runtime node id is checked here.
    /// Currently a gap: no other layer enforces this end-to-end.
    /// </summary>
    private static IEnumerable<string> ValidateTopologySeriesReferences(JsonNode node)
    {
        var topoNodes = node["topology"]?["nodes"] as JsonArray;
        if (topoNodes is null || topoNodes.Count == 0)
        {
            yield break;
        }

        var nodeIds = CollectNodeIds(node);
        // Series-id fields under semantics that point to a nodes[].id. List is
        // intentionally explicit (vs. iterating all properties) so future schema
        // additions don't accidentally pick up a non-series-id field. Aliases /
        // metadata maps and numeric-coefficient fields (slaMin, maxAttempts) are
        // excluded.
        var seriesFields = new[]
        {
            "arrivals", "served", "errors", "attempts", "failures",
            "exhaustedFailures", "retryEcho", "retryBudgetRemaining",
            "externalDemand", "queueDepth", "capacity",
            "processingTimeMsSum", "servedCount"
        };

        for (int i = 0; i < topoNodes.Count; i++)
        {
            var topo = topoNodes[i];
            var semantics = topo?["semantics"];
            if (semantics is null) continue;

            foreach (var field in seriesFields)
            {
                var value = semantics[field]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(value)) continue;

                var trimmed = value.Trim();
                if (trimmed.Equals("self", StringComparison.OrdinalIgnoreCase)) continue;
                if (trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) continue;

                // Strip the optional `series:` scheme prefix and the `@classId` suffix.
                var seriesText = trimmed.StartsWith("series:", StringComparison.OrdinalIgnoreCase)
                    ? trimmed["series:".Length..]
                    : trimmed;
                var at = seriesText.IndexOf('@');
                var refId = at >= 0 ? seriesText[..at].Trim() : seriesText.Trim();
                if (string.IsNullOrWhiteSpace(refId)) continue;

                if (!nodeIds.Contains(refId))
                {
                    yield return $"/topology/nodes/{i}/semantics/{field}: series reference '{refId}' does not match any declared nodes[].id.";
                }
            }
        }
    }

    /// <summary>
    /// Adjunct: when a topology node declares <c>wipOverflow</c> with a value that is
    /// neither <c>"loss"</c> nor empty, the value must reference another topology node
    /// id. Mirrors <c>ModelCompiler.cs:380-384</c>; the compiler check stays as
    /// defence-in-depth.
    /// </summary>
    private static IEnumerable<string> ValidateWipOverflowTarget(JsonNode node)
    {
        var topoNodes = node["topology"]?["nodes"] as JsonArray;
        if (topoNodes is null || topoNodes.Count == 0)
        {
            yield break;
        }

        var topoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in topoNodes)
        {
            var id = t?["id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                topoIds.Add(id);
            }
        }

        for (int i = 0; i < topoNodes.Count; i++)
        {
            var t = topoNodes[i];
            var overflow = t?["wipOverflow"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(overflow)) continue;

            var trimmed = overflow.Trim();
            if (trimmed.Equals("loss", StringComparison.OrdinalIgnoreCase)) continue;

            if (!topoIds.Contains(trimmed))
            {
                var fromId = t?["id"]?.GetValue<string>() ?? "<unknown>";
                yield return $"/topology/nodes/{i}: wipOverflow target '{trimmed}' on '{fromId}' does not match any topology node.";
            }
        }
    }

    /// <summary>
    /// Adjunct: the <c>wipOverflow</c> routing graph must be acyclic. Standard
    /// path-traversal cycle detection over the (node → overflow target) map.
    /// Mirrors <c>ModelCompiler.cs:412-420</c>; the compiler check stays as
    /// defence-in-depth.
    /// </summary>
    private static IEnumerable<string> ValidateWipOverflowAcyclic(JsonNode node)
    {
        var topoNodes = node["topology"]?["nodes"] as JsonArray;
        if (topoNodes is null || topoNodes.Count == 0)
        {
            yield break;
        }

        var edges = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in topoNodes)
        {
            var id = t?["id"]?.GetValue<string>();
            var overflow = t?["wipOverflow"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(overflow)) continue;
            if (overflow.Trim().Equals("loss", StringComparison.OrdinalIgnoreCase)) continue;
            edges[id] = overflow.Trim();
        }

        if (edges.Count == 0)
        {
            yield break;
        }

        foreach (var start in edges.Keys)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
            var current = start;
            while (edges.TryGetValue(current, out var next))
            {
                if (!visited.Add(next))
                {
                    yield return $"/topology/nodes: wipOverflow routing contains a cycle involving '{current}' → '{next}'.";
                    yield break; // one cycle finding is enough — adjunct does not enumerate every entry
                }
                current = next;
            }
        }
    }

    /// <summary>
    /// Adjunct: declared <c>format: date-time</c> fields (<c>grid.start</c> and
    /// <c>provenance.generatedAt</c>) must parse via
    /// <see cref="DateTime.TryParse(string?, IFormatProvider?, DateTimeStyles, out DateTime)"/>.
    /// Draft-07's <c>format: date-time</c> is annotation-only and not enforced by
    /// JsonEverything by default. Mirrors <c>ModelParser.cs:251-252</c>.
    /// </summary>
    private static IEnumerable<string> ValidateDateTimeFormats(JsonNode node)
    {
        var gridStart = node["grid"]?["start"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(gridStart) && !TryParseIsoDateTime(gridStart))
        {
            yield return $"/grid/start: value '{gridStart}' is not a parseable ISO-8601 date-time.";
        }

        var generatedAt = node["provenance"]?["generatedAt"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(generatedAt) && !TryParseIsoDateTime(generatedAt))
        {
            yield return $"/provenance/generatedAt: value '{generatedAt}' is not a parseable ISO-8601 date-time.";
        }
    }

    // ─── Adjunct helpers ───────────────────────────────────────────────────────

    private static HashSet<string> CollectNodeIds(JsonNode node)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var nodes = node["nodes"] as JsonArray;
        if (nodes is null) return ids;
        foreach (var entry in nodes)
        {
            var id = entry?["id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    private static HashSet<string> CollectTopologyInitialIds(JsonNode node)
    {
        // Returns the set of node ids that have an initialCondition supplied via the
        // topology block — either the topology node's own id, or its
        // semantics.queueDepth binding (the id of the series that carries the queue).
        // Mirrors ModelParser.ValidateInitialConditions construction.
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var topoNodes = node["topology"]?["nodes"] as JsonArray;
        if (topoNodes is null) return ids;

        foreach (var t in topoNodes)
        {
            var initial = t?["initialCondition"];
            if (initial is null) continue;

            var topoId = t?["id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(topoId)) ids.Add(topoId);

            var queueDepthRef = t?["semantics"]?["queueDepth"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(queueDepthRef))
            {
                ids.Add(queueDepthRef.Trim());
            }
        }
        return ids;
    }

    private static bool IsPmfNode(JsonNode entry)
    {
        var kind = entry["kind"]?.GetValue<string>();
        return string.Equals(kind, "pmf", StringComparison.Ordinal);
    }

    private static bool TryGetGridBins(JsonNode node, out int bins)
    {
        bins = 0;
        var binsNode = node["grid"]?["bins"];
        if (binsNode is null) return false;
        try
        {
            bins = binsNode.GetValue<int>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseIsoDateTime(string value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out _);
    }

    /// <summary>
    /// Visitor that collects every <see cref="NodeReferenceNode.NodeId"/> reachable
    /// from the AST root. Used by <see cref="ValidateExpressionNodeReferences"/> to
    /// extract dependencies without re-implementing the parser's identifier rules.
    /// </summary>
    private sealed class NodeReferenceCollector : IExpressionVisitor<object?>
    {
        public HashSet<string> References { get; } = new(StringComparer.Ordinal);

        public object? VisitBinaryOp(BinaryOpNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            return null;
        }

        public object? VisitFunctionCall(FunctionCallNode node)
        {
            foreach (var arg in node.Arguments)
            {
                arg.Accept(this);
            }
            return null;
        }

        public object? VisitNodeReference(NodeReferenceNode node)
        {
            if (!string.IsNullOrWhiteSpace(node.NodeId))
            {
                References.Add(node.NodeId);
            }
            return null;
        }

        public object? VisitLiteral(LiteralNode node) => null;
        public object? VisitArrayLiteral(ArrayLiteralNode node) => null;
    }

    private static string ConvertYamlToJson(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0)
        {
            throw new InvalidDataException("YAML document was empty.");
        }

        var root = stream.Documents[0].RootNode;
        var node = ConvertYamlNode(root);
        return node.ToJsonString();
    }

    private static JsonNode ConvertYamlNode(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => ParseScalar(scalar),
            YamlSequenceNode sequence => ConvertSequence(sequence),
            YamlMappingNode mapping => ConvertMapping(mapping),
            _ => JsonValue.Create(node.ToString() ?? string.Empty)!
        };
    }

    private static JsonNode ConvertSequence(YamlSequenceNode sequence)
    {
        var array = new JsonArray();
        foreach (var child in sequence.Children)
        {
            array.Add(ConvertYamlNode(child));
        }
        return array;
    }

    private static JsonNode ConvertMapping(YamlMappingNode mapping)
    {
        var obj = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
            {
                continue;
            }

            var key = keyNode.Value ?? string.Empty;
            obj[key] = ConvertYamlNode(entry.Value);
        }
        return obj;
    }

    /// <summary>
    /// Convert a YAML scalar node to a typed JSON value. Honors YAML 1.2's rule that
    /// quoted scalars (<see cref="YamlDotNet.Core.ScalarStyle.SingleQuoted"/>,
    /// <see cref="YamlDotNet.Core.ScalarStyle.DoubleQuoted"/>) and block scalars
    /// (<see cref="YamlDotNet.Core.ScalarStyle.Literal"/>,
    /// <see cref="YamlDotNet.Core.ScalarStyle.Folded"/>) are explicitly typed as strings;
    /// only plain scalars are subject to bool/int/double type resolution
    /// (m-E24-04 / ADR-E-24-04). <c>internal</c> for direct test access.
    /// </summary>
    internal static JsonNode ParseScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (value is null)
        {
            return JsonValue.Create((string?)null)!;
        }

        // YAML 1.2: only plain scalars are candidates for type resolution. Quoted and
        // block scalars are unconditionally typed as strings — author intent is preserved.
        if (scalar.Style != YamlDotNet.Core.ScalarStyle.Plain)
        {
            return JsonValue.Create(value)!;
        }

        if (bool.TryParse(value, out var boolResult))
        {
            return JsonValue.Create(boolResult)!;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intResult))
        {
            return JsonValue.Create(intResult)!;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleResult))
        {
            return JsonValue.Create(doubleResult)!;
        }

        return JsonValue.Create(value)!;
    }
}
