namespace DeepSigma.LogicEngine.Formulas;

/// <summary>
/// Structural rewrites over formulas. Currently provides variable renaming,
/// used by constructions that copy a formula into a new namespace (e.g. the
/// time-indexed unrolling of a transition system).
/// </summary>
public static class FormulaRewriter
{
    /// <summary>
    /// Return a copy of <paramref name="formula"/> with every variable name
    /// mapped through <paramref name="rename"/>. Boolean structure is preserved.
    /// </summary>
    public static Formula RenameVariables(Formula formula, Func<string, string> rename) => formula switch
    {
        BoolConst => formula,
        Variable v => new Variable(rename(v.Name)),
        Negation n => new Negation(RenameVariables(n.Operand, rename)),
        Conjunction c => new Conjunction(RenameVariables(c.Left, rename), RenameVariables(c.Right, rename)),
        Disjunction d => new Disjunction(RenameVariables(d.Left, rename), RenameVariables(d.Right, rename)),
        Implication i => new Implication(RenameVariables(i.Antecedent, rename), RenameVariables(i.Consequent, rename)),
        Biconditional b => new Biconditional(RenameVariables(b.Left, rename), RenameVariables(b.Right, rename)),
        _ => throw new InvalidOperationException($"Unknown formula node: {formula.GetType().Name}"),
    };
}
