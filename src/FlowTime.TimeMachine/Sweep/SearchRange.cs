namespace FlowTime.TimeMachine.Sweep;

/// <summary>A closed search interval [Lo, Hi] with Lo &lt; Hi.</summary>
public sealed record SearchRange(double Lo, double Hi);
