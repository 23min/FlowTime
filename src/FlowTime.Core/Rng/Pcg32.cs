namespace FlowTime.Core;

/// <summary>
/// PCG32 (Permuted Congruential Generator) - Fast, deterministic random number generator.
/// Implements the PCG-XSH-RR variant with 64-bit state and 32-bit output.
/// </summary>
/// <remarks>
/// This implementation provides deterministic pseudo-random number generation for
/// reproducible model execution. Same seed always produces the same sequence.
/// 
/// Algorithm: PCG-XSH-RR by Melissa O'Neill (www.pcg-random.org)
/// License: Apache 2.0 or MIT (permissive)
/// 
/// For algorithm selection rationale and performance analysis, see:
/// docs/architecture/rng-algorithm.md
/// </remarks>
public class Pcg32
{
    private ulong state;
    private readonly ulong increment;

    /// <summary>
    /// Default increment value for PCG32 algorithm.
    /// This value ensures good statistical properties and full period.
    /// </summary>
    private const ulong defaultIncrement = 1442695040888963407ul;

    /// <summary>
    /// multiplier constant for the LCG step.
    /// </summary>
    private const ulong multiplier = 6364136223846793005ul;

    /// <summary>
    /// Initialize a new PCG32 random number generator with the specified seed.
    /// </summary>
    /// <param name="seed">Seed value (0 to int.MaxValue)</param>
    public Pcg32(int seed)
    {
        state = (ulong)seed;
        increment = defaultIncrement;
        
        // Warm up the generator - advance state once to avoid initial zero correlation
        NextUInt32();
    }

    /// <summary>
    /// Initialize a PCG32 generator with explicit state and increment.
    /// Used for state restoration.
    /// </summary>
    private Pcg32(ulong state, ulong increment)
    {
        this.state = state;
        this.increment = increment;
    }

    /// <summary>
    /// Generate the next random 32-bit unsigned integer.
    /// </summary>
    /// <returns>Random value in range [0, uint.MaxValue]</returns>
    public uint NextUInt32()
    {
        ulong oldState = state;
        
        // LCG step: advance internal state
        state = oldState * multiplier + increment;
        
        // PCG-XSH-RR output function
        // XOR-shift high bits, then rotate right
        uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rotation = (int)(oldState >> 59);
        
        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }

    /// <summary>
    /// Generate a random double in the range [0.0, 1.0).
    /// </summary>
    /// <returns>Random value in range [0.0, 1.0)</returns>
    public double NextDouble()
    {
        // Convert uint32 to double in [0.0, 1.0) range
        // Divide by (uint.MaxValue + 1) to ensure result < 1.0
        return NextUInt32() / (double)(1ul << 32);
    }

    /// <summary>
    /// Generate a random integer in the range [min, max).
    /// </summary>
    /// <param name="min">Inclusive lower bound</param>
    /// <param name="max">Exclusive upper bound</param>
    /// <returns>Random value in range [min, max)</returns>
    /// <exception cref="ArgumentException">If min >= max</exception>
    public int NextInt(int min, int max)
    {
        if (min >= max)
            throw new ArgumentException($"min ({min}) must be less than max ({max})");

        uint range = (uint)(max - min);

        // Lemire's nearly divisionless algorithm for unbiased bounded random.
        // Eliminates modulo bias present in the simple (NextUInt32() % range) approach.
        // Reference: https://arxiv.org/abs/1805.10941
        ulong m = (ulong)NextUInt32() * range;
        uint l = (uint)m;
        if (l < range)
        {
            uint threshold = (uint)(-(int)range) % range;
            while (l < threshold)
            {
                m = (ulong)NextUInt32() * range;
                l = (uint)m;
            }
        }
        return min + (int)(m >> 32);
    }

    /// <summary>
    /// Create a copy of this RNG with the same internal state.
    /// The clone will produce the same sequence of numbers as the original from this point.
    /// </summary>
    /// <returns>A new Pcg32 instance with identical state</returns>
    public Pcg32 Clone()
    {
        return new Pcg32(state, increment);
    }

    /// <summary>
    /// Get the current internal state for serialization.
    /// </summary>
    /// <returns>Tuple of (state, increment)</returns>
    public (ulong State, ulong Increment) GetState()
    {
        return (state, increment);
    }

    /// <summary>
    /// Create a new Pcg32 instance from saved state.
    /// </summary>
    /// <param name="state">Tuple of (state, increment) from GetState()</param>
    /// <returns>New Pcg32 instance with restored state</returns>
    public static Pcg32 FromState((ulong State, ulong Increment) state)
    {
        return new Pcg32(state.State, state.Increment);
    }

    /// <summary>
    /// Get string representation of this RNG for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"Pcg32(state={state:X16}, increment={increment:X16})";
    }
}
