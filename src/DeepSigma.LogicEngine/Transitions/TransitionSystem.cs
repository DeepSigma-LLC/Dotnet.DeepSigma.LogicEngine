using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Transitions;

/// <summary>
/// A finite-state transition system over boolean state variables: an initial-state
/// predicate and a transition relation. The transition relation refers to the
/// current state via the plain variable name <c>x</c> and to the next state via
/// the <b>primed</b> name <c>x'</c> (the variable name suffixed with an
/// apostrophe). Used as the substrate for bounded model checking.
/// </summary>
public sealed record TransitionSystem(
    IReadOnlyList<string> StateVariables,
    Formula Initial,
    Formula Transition)
{
    /// <summary>The primed (next-state) name for a state variable.</summary>
    public static string Prime(string variable) => variable + "'";
}
