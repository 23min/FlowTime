using FlowTime.TimeMachine.Sweep;
using MessagePack;
using MessagePack.Resolvers;

namespace FlowTime.TimeMachine.Tests.Sweep;

/// <summary>
/// Branch-coverage tests for <see cref="SessionModelEvaluator"/>'s internal parsing and
/// framing helpers. These cover paths that are defensive against protocol corruption or
/// malformed YAML and that cannot be reached from the integration tests because the real
/// Rust engine never produces them. Required for 100% coverage of the implementation.
/// </summary>
public class SessionModelEvaluatorHelperTests
{
    private static readonly MessagePackSerializerOptions MpOptions =
        MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

    // ─────────────────────────────────────────────────────────────────
    // BuildOverrides
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildOverrides_EmptyParamIds_ReturnsEmptyDictionary()
    {
        var result = SessionModelEvaluator.BuildOverrides(
            modelYaml: "grid: {bins: 1, binSize: 1, binUnit: hours}\nnodes: []",
            paramIds: Array.Empty<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void BuildOverrides_AllParamsFoundInYaml_ReturnsAllValues()
    {
        var yaml = """
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: arrivals
                kind: const
                values: [10, 10, 10, 10]
              - id: capacity
                kind: const
                values: [5, 5, 5, 5]
            """;
        var result = SessionModelEvaluator.BuildOverrides(yaml, ["arrivals", "capacity"]);

        Assert.Equal(2, result.Count);
        Assert.Equal(10.0, Assert.IsType<double>(result["arrivals"]));
        Assert.Equal(5.0, Assert.IsType<double>(result["capacity"]));
    }

    [Fact]
    public void BuildOverrides_ParamNotInYaml_OmitsThatParam()
    {
        // Only "arrivals" exists; "capacity" is in paramIds but not in the YAML.
        var yaml = """
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: arrivals
                kind: const
                values: [10, 10, 10, 10]
            """;
        var result = SessionModelEvaluator.BuildOverrides(yaml, ["arrivals", "capacity"]);

        Assert.Single(result);
        Assert.True(result.ContainsKey("arrivals"));
        Assert.False(result.ContainsKey("capacity"));
    }

    // ─────────────────────────────────────────────────────────────────
    // ExtractResult
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractResult_SuccessResponse_ReturnsResultDict()
    {
        var inner = new Dictionary<object, object> { ["series"] = new Dictionary<object, object>() };
        var response = new Dictionary<object, object> { ["result"] = inner };

        var result = SessionModelEvaluator.ExtractResult(response, "eval");

        Assert.Same(inner, result);
    }

    [Fact]
    public void ExtractResult_ErrorResponse_ThrowsWithCodeAndMessage()
    {
        var err = new Dictionary<object, object>
        {
            ["code"] = "compile_error",
            ["message"] = "bad yaml",
        };
        var response = new Dictionary<object, object> { ["error"] = err };

        var ex = Assert.Throws<InvalidOperationException>(
            () => SessionModelEvaluator.ExtractResult(response, "compile"));
        Assert.Contains("compile_error", ex.Message);
        Assert.Contains("bad yaml", ex.Message);
        Assert.Contains("compile", ex.Message); // method name
    }

    [Fact]
    public void ExtractResult_ErrorWithMissingCodeAndMessage_StillThrows()
    {
        // Defensive: an error dict with no sub-fields should still produce a readable
        // exception rather than a NullReferenceException.
        var response = new Dictionary<object, object>
        {
            ["error"] = new Dictionary<object, object>(),
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => SessionModelEvaluator.ExtractResult(response, "eval"));
        Assert.Contains("eval", ex.Message);
    }

    [Fact]
    public void ExtractResult_NeitherErrorNorResult_Throws()
    {
        var response = new Dictionary<object, object> { ["unexpected"] = "x" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => SessionModelEvaluator.ExtractResult(response, "compile"));
        Assert.Contains("result", ex.Message);
    }

    [Fact]
    public void ExtractResult_ResultWrongShape_Throws()
    {
        // "result" present but not a dictionary — treat as malformed.
        var response = new Dictionary<object, object> { ["result"] = "not-a-dict" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => SessionModelEvaluator.ExtractResult(response, "eval"));
        Assert.Contains("result", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────
    // ExtractParamIds
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractParamIds_MissingParamsKey_ReturnsEmpty()
    {
        var result = new Dictionary<object, object> { ["other"] = "value" };
        Assert.Empty(SessionModelEvaluator.ExtractParamIds(result));
    }

    [Fact]
    public void ExtractParamIds_ParamsNotArray_ReturnsEmpty()
    {
        var result = new Dictionary<object, object> { ["params"] = "not-an-array" };
        Assert.Empty(SessionModelEvaluator.ExtractParamIds(result));
    }

    [Fact]
    public void ExtractParamIds_ValidParams_ReturnsIds()
    {
        var p1 = new Dictionary<object, object> { ["id"] = "arrivals", ["kind"] = "Scalar" };
        var p2 = new Dictionary<object, object> { ["id"] = "capacity", ["kind"] = "Scalar" };
        var result = new Dictionary<object, object>
        {
            ["params"] = new object[] { p1, p2 },
        };

        var ids = SessionModelEvaluator.ExtractParamIds(result);

        Assert.Equal(2, ids.Count);
        Assert.Contains("arrivals", ids);
        Assert.Contains("capacity", ids);
    }

    [Fact]
    public void ExtractParamIds_MalformedItems_SkipsThemSilently()
    {
        // Mix of valid + malformed items — valid ones are kept, malformed are skipped.
        var good = new Dictionary<object, object> { ["id"] = "arrivals" };
        var noId = new Dictionary<object, object> { ["kind"] = "Scalar" };
        var idNotString = new Dictionary<object, object> { ["id"] = 42 };
        var notADict = "not-a-dict";
        var result = new Dictionary<object, object>
        {
            ["params"] = new object[] { good, noId, idNotString, notADict },
        };

        var ids = SessionModelEvaluator.ExtractParamIds(result);

        Assert.Single(ids);
        Assert.Equal("arrivals", ids[0]);
    }

    // ─────────────────────────────────────────────────────────────────
    // ExtractSeries
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractSeries_MissingSeriesKey_Throws()
    {
        var result = new Dictionary<object, object> { ["other"] = 1.0 };
        var ex = Assert.Throws<InvalidOperationException>(
            () => SessionModelEvaluator.ExtractSeries(result));
        Assert.Contains("series", ex.Message);
    }

    [Fact]
    public void ExtractSeries_SeriesNotADict_Throws()
    {
        var result = new Dictionary<object, object> { ["series"] = "not-a-dict" };
        Assert.Throws<InvalidOperationException>(
            () => SessionModelEvaluator.ExtractSeries(result));
    }

    [Fact]
    public void ExtractSeries_ValidSeries_ReturnsConvertedDoubles()
    {
        // MessagePack deserializes numeric arrays as object[] with boxed primitives.
        var series = new Dictionary<object, object>
        {
            ["arrivals"] = new object[] { 10.0, 20.0, 30.0 },
            ["served"] = new object[] { 5, 10, 15 }, // ints: must convert to doubles
        };
        var result = new Dictionary<object, object> { ["series"] = series };

        var output = SessionModelEvaluator.ExtractSeries(result);

        Assert.Equal(2, output.Count);
        Assert.Equal(new[] { 10.0, 20.0, 30.0 }, output["arrivals"]);
        Assert.Equal(new[] { 5.0, 10.0, 15.0 }, output["served"]);
    }

    [Fact]
    public void ExtractSeries_KeysAreCaseInsensitive()
    {
        var series = new Dictionary<object, object>
        {
            ["Arrivals"] = new object[] { 10.0 },
        };
        var result = new Dictionary<object, object> { ["series"] = series };

        var output = SessionModelEvaluator.ExtractSeries(result);

        Assert.True(output.ContainsKey("arrivals"));
        Assert.True(output.ContainsKey("ARRIVALS"));
    }

    [Fact]
    public void ExtractSeries_NonStringKey_IsSkipped()
    {
        var series = new Dictionary<object, object>
        {
            ["valid"] = new object[] { 1.0 },
            [42] = new object[] { 99.0 }, // non-string key — must be skipped
        };
        var result = new Dictionary<object, object> { ["series"] = series };

        var output = SessionModelEvaluator.ExtractSeries(result);

        Assert.Single(output);
        Assert.True(output.ContainsKey("valid"));
    }

    [Fact]
    public void ExtractSeries_NonArrayValue_IsSkipped()
    {
        var series = new Dictionary<object, object>
        {
            ["valid"] = new object[] { 1.0 },
            ["malformed"] = "not-an-array", // must be skipped
        };
        var result = new Dictionary<object, object> { ["series"] = series };

        var output = SessionModelEvaluator.ExtractSeries(result);

        Assert.Single(output);
        Assert.True(output.ContainsKey("valid"));
        Assert.False(output.ContainsKey("malformed"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Framing (WriteFrameAsync / ReadFrameAsync / ReadExactAsync)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteFrameAsync_WritesLengthPrefixedMessagePack()
    {
        using var ms = new MemoryStream();

        var payload = new Dictionary<string, object> { ["method"] = "hello" };
        await SessionModelEvaluator.WriteFrameAsync(ms, payload, CancellationToken.None);

        var bytes = ms.ToArray();
        Assert.True(bytes.Length > 4, "must have length prefix + payload");

        // 4-byte big-endian length prefix
        var len = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        Assert.Equal(bytes.Length - 4, len);

        // Remaining bytes deserialize back
        var round = MessagePackSerializer.Deserialize<Dictionary<object, object>>(
            bytes.AsMemory(4).ToArray(), MpOptions);
        Assert.Equal("hello", round["method"]);
    }

    [Fact]
    public async Task ReadFrameAsync_ValidFrame_RoundTripsDictionary()
    {
        // Serialize a dict, prepend length prefix, feed through ReadFrameAsync.
        var original = new Dictionary<string, object> { ["status"] = "ok", ["count"] = 3 };
        var payload = MessagePackSerializer.Serialize<object>(original, MpOptions);

        using var ms = new MemoryStream();
        ms.WriteByte((byte)((payload.Length >> 24) & 0xff));
        ms.WriteByte((byte)((payload.Length >> 16) & 0xff));
        ms.WriteByte((byte)((payload.Length >> 8) & 0xff));
        ms.WriteByte((byte)(payload.Length & 0xff));
        ms.Write(payload);
        ms.Position = 0;

        var result = await SessionModelEvaluator.ReadFrameAsync(ms, CancellationToken.None);

        Assert.Equal("ok", result["status"]);
        Assert.Equal(3, Convert.ToInt32(result["count"]));
    }

    [Fact]
    public async Task ReadFrameAsync_ZeroLength_Throws()
    {
        using var ms = new MemoryStream([0, 0, 0, 0]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SessionModelEvaluator.ReadFrameAsync(ms, CancellationToken.None));
    }

    [Fact]
    public async Task ReadFrameAsync_NegativeLength_Throws()
    {
        // 0xffffffff interpreted as a signed int is negative.
        using var ms = new MemoryStream([0xff, 0xff, 0xff, 0xff]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SessionModelEvaluator.ReadFrameAsync(ms, CancellationToken.None));
    }

    [Fact]
    public async Task ReadFrameAsync_ExcessiveLength_Throws()
    {
        // 100 MB exceeds the 64 MB cap.
        const int hundredMb = 100 * 1024 * 1024;
        var bytes = new byte[]
        {
            (byte)((hundredMb >> 24) & 0xff),
            (byte)((hundredMb >> 16) & 0xff),
            (byte)((hundredMb >> 8) & 0xff),
            (byte)(hundredMb & 0xff),
        };
        using var ms = new MemoryStream(bytes);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SessionModelEvaluator.ReadFrameAsync(ms, CancellationToken.None));
    }

    [Fact]
    public async Task ReadExactAsync_EofMidRead_ThrowsEndOfStreamException()
    {
        // Stream has only 2 bytes, but we ask for 4 — must throw.
        using var ms = new MemoryStream([1, 2]);
        var buffer = new byte[4];
        await Assert.ThrowsAsync<EndOfStreamException>(
            () => SessionModelEvaluator.ReadExactAsync(ms, buffer, 0, 4, CancellationToken.None));
    }

    [Fact]
    public async Task ReadExactAsync_ExactBytesAvailable_FillsBuffer()
    {
        using var ms = new MemoryStream([10, 20, 30, 40]);
        var buffer = new byte[4];
        await SessionModelEvaluator.ReadExactAsync(ms, buffer, 0, 4, CancellationToken.None);
        Assert.Equal([10, 20, 30, 40], buffer);
    }

    [Fact]
    public async Task ReadFrameAsync_TruncatedPayload_ThrowsEndOfStreamException()
    {
        // Length prefix says 10 bytes but only 3 bytes follow — EOF mid-read.
        using var ms = new MemoryStream([0, 0, 0, 10, 1, 2, 3]);
        await Assert.ThrowsAsync<EndOfStreamException>(
            () => SessionModelEvaluator.ReadFrameAsync(ms, CancellationToken.None));
    }
}
