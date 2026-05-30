using System.Diagnostics;

namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// Internal consistency checks, compiled only in DEBUG builds (the calls vanish
/// entirely in Release). They turn silent CDCL state-corruption bugs into loud,
/// located failures during testing.
/// </summary>
internal static class CdclInvariants
{
    [Conditional("DEBUG")]
    public static void AssertTrailLevelsMonotonic(Trail trail)
    {
        var previous = 0;
        for (var i = 0; i < trail.AssignedCount; i++)
        {
            var level = trail.LevelOf(CdclLiterals.Variable(trail.LiteralAt(i)));
            Debug.Assert(level >= previous, "Trail decision levels must be non-decreasing.");
            previous = level;
        }
    }

    [Conditional("DEBUG")]
    public static void AssertBackjumpProgress(LearnedClause learned, int conflictLevel)
    {
        Debug.Assert(
            learned.BackjumpLevel < conflictLevel,
            "Backjump level must be strictly below the conflict level to guarantee progress.");
    }

    [Conditional("DEBUG")]
    public static void AssertModelComplete(Trail trail)
    {
        for (var v = 0; v < trail.VariableCount; v++)
        {
            Debug.Assert(trail.Value(v) != LBool.Unassigned, "Every variable must be assigned in a model.");
        }
    }
}
