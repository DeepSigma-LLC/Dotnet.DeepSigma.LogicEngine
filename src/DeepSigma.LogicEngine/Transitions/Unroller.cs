using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Transitions;

/// <summary>
/// Unrolls a <see cref="TransitionSystem"/> over a finite number of steps into a
/// single propositional formula whose variables are time-indexed (<c>x@t</c>).
/// This is the core of bounded model checking: a satisfying assignment of the
/// unrolling is a concrete execution trace of length <c>steps</c>.
/// </summary>
public static class Unroller
{
    /// <summary>The name of a state variable at time step <paramref name="step"/>.</summary>
    public static string At(string variable, int step) => $"{variable}@{step}";

    /// <summary>
    /// Build <c>Initial@0 ∧ Transition(0→1) ∧ … ∧ Transition(steps−1→steps)</c>.
    /// The result ranges over variables <c>x@0 … x@steps</c> for each state
    /// variable <c>x</c>; read a trace from a model with <see cref="At"/>.
    /// </summary>
    public static Formula Unroll(TransitionSystem system, int steps)
    {
        if (steps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), "Step count must be non-negative.");
        }

        var result = FormulaRewriter.RenameVariables(system.Initial, name => At(name, 0));
        for (var t = 0; t < steps; t++)
        {
            var step = FormulaRewriter.RenameVariables(system.Transition, name => RenameTransitionVariable(name, t));
            result = new Conjunction(result, step);
        }
        return result;
    }

    /// <summary>Map a transition-relation variable to its time-indexed name: current at t, primed at t+1.</summary>
    private static string RenameTransitionVariable(string name, int t)
        => name.EndsWith('\'')
            ? At(name[..^1], t + 1)
            : At(name, t);
}
