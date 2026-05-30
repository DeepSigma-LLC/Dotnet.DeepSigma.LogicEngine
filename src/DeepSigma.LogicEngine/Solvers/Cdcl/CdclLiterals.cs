namespace DeepSigma.LogicEngine.Solvers.Cdcl;

/// <summary>
/// Branch-free helpers for the dense integer literal encoding used throughout
/// the CDCL core: <c>lit = var * 2 + sign</c>, where sign 0 is the positive
/// literal and sign 1 the negated one. Variables are 0-based dense ids.
/// </summary>
internal static class CdclLiterals
{
    public static int Make(int variable, bool negated) => (variable << 1) | (negated ? 1 : 0);

    public static int Positive(int variable) => variable << 1;

    public static int Negative(int variable) => (variable << 1) | 1;

    public static int Negate(int literal) => literal ^ 1;

    public static int Variable(int literal) => literal >> 1;

    public static bool IsNegated(int literal) => (literal & 1) != 0;
}
