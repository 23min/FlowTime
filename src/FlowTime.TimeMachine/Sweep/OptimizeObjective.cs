namespace FlowTime.TimeMachine.Sweep;

/// <summary>Direction of the optimization objective.</summary>
public enum OptimizeObjective
{
    /// <summary>Find parameter values that minimize the metric series mean.</summary>
    Minimize,

    /// <summary>Find parameter values that maximize the metric series mean.</summary>
    Maximize,
}
