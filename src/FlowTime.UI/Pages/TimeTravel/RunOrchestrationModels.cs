using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowTime.UI.Services;

namespace FlowTime.UI.Pages.TimeTravel;

public enum OrchestrationMode
{
    Simulation,
    Telemetry
}

public static class OrchestrationModeExtensions
{
    public static string ToApiString(this OrchestrationMode mode) =>
        mode == OrchestrationMode.Telemetry ? "telemetry" : "simulation";

    public static OrchestrationMode FromString(string value) =>
        string.Equals(value, "telemetry", StringComparison.OrdinalIgnoreCase)
            ? OrchestrationMode.Telemetry
            : OrchestrationMode.Simulation;
}

public enum RunOrchestrationPhase
{
    Idle,
    Planning,
    Running,
    Success,
    Failure
}

public sealed record RunOrchestrationFormModel
{
    public string? TemplateId { get; init; }
    public OrchestrationMode Mode { get; init; } = OrchestrationMode.Simulation;
    public string? ParameterText { get; init; }
    public string? TelemetryBindingsText { get; init; }
    public string? CaptureDirectory { get; init; }
    public string? RngSeedText { get; init; }
}

public sealed record RunSubmissionSnapshot(
    string TemplateId,
    OrchestrationMode Mode,
    DateTimeOffset SubmittedAtUtc,
    bool IsDryRun)
{
    public string? ParameterText { get; init; }
    public string? TelemetryBindingsText { get; init; }
    public string? CaptureDirectory { get; init; }
    public string? RngSeedText { get; init; }
}

public sealed record RunOrchestrationSuccess(string RunId);

public static class RunOrchestrationRequestBuilder
{
    private const int DefaultRngSeed = 123;

    public static bool TryBuild(
        RunOrchestrationFormModel model,
        bool dryRun,
        out RunCreateRequestDto? request,
        out string? error)
    {
        request = null;
        error = null;

        if (model is null)
        {
            error = "Form data is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(model.TemplateId))
        {
            error = "Select a template before running orchestration.";
            return false;
        }

        if (!TryParseParameters(model.ParameterText, out var parameters, out error))
        {
            return false;
        }

        RunTelemetryOptionsDto? telemetry = null;
        if (model.Mode == OrchestrationMode.Telemetry)
        {
            if (string.IsNullOrWhiteSpace(model.CaptureDirectory))
            {
                error = "Template does not declare a telemetry capture bundle.";
                return false;
            }

            if (!TryParseBindings(model.TelemetryBindingsText, out var bindings, out error))
            {
                return false;
            }

            telemetry = new RunTelemetryOptionsDto(
                CaptureDirectory: model.CaptureDirectory.Trim(),
                Bindings: bindings);
        }

        var options = new RunCreationOptionsDto(
            DeterministicRunId: false,
            RunId: null,
            DryRun: dryRun,
            OverwriteExisting: !dryRun);

        if (!TryResolveRng(model.RngSeedText, out var rng, out error))
        {
            return false;
        }

        request = new RunCreateRequestDto(
            TemplateId: model.TemplateId.Trim(),
            Mode: model.Mode.ToApiString(),
            Parameters: parameters,
            Telemetry: telemetry,
            Options: options,
            Rng: rng);

        return true;
    }

    private static bool TryResolveRng(string? seedText, out RunRngOptionsDto rng, out string? error)
    {
        error = null;
        rng = default!;

        var seedValue = DefaultRngSeed;
        if (!string.IsNullOrWhiteSpace(seedText))
        {
            if (!int.TryParse(seedText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seedValue))
            {
                error = "RNG seed must be an integer.";
                return false;
            }

            if (seedValue < 0)
            {
                error = "RNG seed must be non-negative.";
                return false;
            }
        }

        rng = new RunRngOptionsDto("pcg32", seedValue);
        return true;
    }

    private static bool TryParseParameters(
        string? text,
        out Dictionary<string, JsonElement>? parameters,
        out string? error)
    {
        parameters = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    error = "Parameters JSON must be an object.";
                    return false;
                }

                parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    parameters[prop.Name] = prop.Value.Clone();
                }

                return true;
            }
            catch (JsonException ex)
            {
                error = $"Invalid JSON parameters: {ex.Message}";
                return false;
            }
        }

        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in EnumerateLines(trimmed))
        {
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == line.Length - 1)
            {
                error = $"Invalid parameter line: '{line}'. Use key=value.";
                return false;
            }

            var key = line[..separator].Trim();
            var valueText = line[(separator + 1)..].Trim();
            if (key.Length == 0)
            {
                error = $"Invalid parameter line: '{line}'.";
                return false;
            }

            dict[key] = ParseLooseJson(valueText);
        }

        parameters = dict.Count == 0 ? null : dict;
        return true;
    }

    private static bool TryParseBindings(
        string? text,
        out Dictionary<string, string>? bindings,
        out string? error)
    {
        bindings = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in EnumerateLines(text))
        {
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == line.Length - 1)
            {
                error = $"Invalid binding line: '{line}'. Use key=value.";
                return false;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
            {
                error = $"Invalid binding line: '{line}'.";
                return false;
            }

            dict[key] = value;
        }

        bindings = dict.Count == 0 ? null : dict;
        return true;
    }

    private static IEnumerable<string> EnumerateLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            yield return trimmed;
        }
    }

    private static JsonElement ParseLooseJson(string value)
    {
        var trimmed = value.Trim();
        string candidate;

        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
        {
            candidate = trimmed.ToLowerInvariant();
        }
        else if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            candidate = trimmed;
        }
        else if ((trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal)) ||
                 (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)))
        {
            candidate = trimmed;
        }
        else
        {
            candidate = JsonSerializer.Serialize(trimmed);
        }

        using var doc = JsonDocument.Parse(candidate);
        return doc.RootElement.Clone();
    }
}

public static class RunSubmissionSnapshotStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record SnapshotPayload(
        string TemplateId,
        string Mode,
        DateTimeOffset SubmittedAtUtc,
        bool IsDryRun)
    {
        public string? ParameterText { get; init; }
        public string? TelemetryBindingsText { get; init; }
        public string? CaptureDirectory { get; init; }
        public string? RngSeedText { get; init; }
    }

    public static string Serialize(RunSubmissionSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

        var payload = new SnapshotPayload(
            TemplateId: snapshot.TemplateId,
            Mode: snapshot.Mode.ToApiString(),
            SubmittedAtUtc: snapshot.SubmittedAtUtc,
            IsDryRun: snapshot.IsDryRun)
        {
            ParameterText = snapshot.ParameterText,
            TelemetryBindingsText = snapshot.TelemetryBindingsText,
            CaptureDirectory = snapshot.CaptureDirectory,
            RngSeedText = snapshot.RngSeedText
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static RunSubmissionSnapshot? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<SnapshotPayload>(json, SerializerOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.TemplateId))
            {
                return null;
            }

            var mode = OrchestrationModeExtensions.FromString(payload.Mode);
            var snapshot = new RunSubmissionSnapshot(
                TemplateId: payload.TemplateId,
                Mode: mode,
                SubmittedAtUtc: payload.SubmittedAtUtc,
                IsDryRun: payload.IsDryRun)
            {
                ParameterText = payload.ParameterText,
                TelemetryBindingsText = payload.TelemetryBindingsText,
                CaptureDirectory = payload.CaptureDirectory,
                RngSeedText = payload.RngSeedText
            };

            return snapshot;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record RunSuccessWarning(string Code, string Message, string? NodeId);

public sealed record RunSuccessSnapshot(
    string RunId,
    string TemplateId,
    string? TemplateTitle,
    string? TemplateVersion,
    string Mode,
    bool TelemetryResolved,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<RunSuccessWarning> Warnings,
    int? RngSeed);

public static class RunSuccessSnapshotStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record WarningPayload(string Code, string Message, string? NodeId);

    private sealed record SuccessPayload(
        string RunId,
        string TemplateId,
        string? TemplateTitle,
        string? TemplateVersion,
        string Mode,
        bool TelemetryResolved,
        DateTimeOffset CompletedAtUtc,
        IReadOnlyList<WarningPayload>? Warnings,
        int? RngSeed);

    public static string Serialize(RunSuccessSnapshot snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

        var warnings = snapshot.Warnings.Count == 0
            ? Array.Empty<WarningPayload>()
            : snapshot.Warnings.Select(w => new WarningPayload(w.Code, w.Message, w.NodeId)).ToArray();

        var payload = new SuccessPayload(
            RunId: snapshot.RunId,
            TemplateId: snapshot.TemplateId,
            TemplateTitle: snapshot.TemplateTitle,
            TemplateVersion: snapshot.TemplateVersion,
            Mode: snapshot.Mode,
            TelemetryResolved: snapshot.TelemetryResolved,
            CompletedAtUtc: snapshot.CompletedAtUtc,
            Warnings: warnings,
            RngSeed: snapshot.RngSeed);

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static RunSuccessSnapshot? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<SuccessPayload>(json, SerializerOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.RunId) || string.IsNullOrWhiteSpace(payload.TemplateId))
            {
                return null;
            }

            var warningPayloads = payload.Warnings ?? Array.Empty<WarningPayload>();
            var warnings = warningPayloads.Count == 0
                ? Array.Empty<RunSuccessWarning>()
                : warningPayloads.Select(w => new RunSuccessWarning(w.Code, w.Message, w.NodeId)).ToArray();

            return new RunSuccessSnapshot(
                RunId: payload.RunId,
                TemplateId: payload.TemplateId,
                TemplateTitle: payload.TemplateTitle,
                TemplateVersion: payload.TemplateVersion,
                Mode: payload.Mode,
                TelemetryResolved: payload.TelemetryResolved,
                CompletedAtUtc: payload.CompletedAtUtc,
                Warnings: warnings,
                RngSeed: payload.RngSeed);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class RunOrchestrationStateMachine
{
    private RunSubmissionSnapshot? pending;

    public RunOrchestrationPhase Phase { get; private set; } = RunOrchestrationPhase.Idle;
    public RunOrchestrationSuccess? LastSuccess { get; private set; }
    public string? LastError { get; private set; }
    public RunSubmissionSnapshot? PendingSubmission => pending;
    public bool IsBusy => Phase is RunOrchestrationPhase.Planning or RunOrchestrationPhase.Running;

    public bool TryBeginPlanning(RunSubmissionSnapshot snapshot)
    {
        if (Phase is RunOrchestrationPhase.Planning or RunOrchestrationPhase.Running)
        {
            return false;
        }

        Phase = RunOrchestrationPhase.Planning;
        pending = snapshot;
        LastSuccess = null;
        LastError = null;
        return true;
    }

    public void PromoteToRunning()
    {
        if (Phase == RunOrchestrationPhase.Planning)
        {
            Phase = RunOrchestrationPhase.Running;
        }
    }

    public void CompleteSuccess(RunOrchestrationSuccess success)
    {
        if (success is null) throw new ArgumentNullException(nameof(success));

        Phase = RunOrchestrationPhase.Success;
        LastSuccess = success;
        LastError = null;
        pending = null;
    }

    public void CompleteFailure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            error = "Run orchestration failed.";
        }

        Phase = RunOrchestrationPhase.Failure;
        LastError = error;
        LastSuccess = null;
        pending = null;
    }

    public void Reset()
    {
        Phase = RunOrchestrationPhase.Idle;
        LastError = null;
        pending = null;
    }

    public void RestorePending(RunSubmissionSnapshot snapshot, RunOrchestrationPhase phase)
    {
        if (phase is not RunOrchestrationPhase.Planning and not RunOrchestrationPhase.Running)
        {
            phase = RunOrchestrationPhase.Planning;
        }

        Phase = phase;
        pending = snapshot;
        LastError = null;
        LastSuccess = null;
    }

    public void ClearPending() => pending = null;
}
