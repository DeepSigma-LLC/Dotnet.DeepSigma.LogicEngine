using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Evaluation;

/// <summary>Evaluates propositional formulas under a variable assignment and collects their variables.</summary>
public static class Evaluator
{
    /// <summary>
    /// Evaluate the formula under the given assignment.
    /// Throws when a free variable has no value.
    /// </summary>
    public static bool Evaluate(Formula formula, IReadOnlyDictionary<string, bool> assignment)
    {
        return formula switch
        {
            BoolConst c => c.Value,
            Variable v => assignment.TryGetValue(v.Name, out var b)
                ? b
                : throw new InvalidOperationException($"Variable '{v.Name}' has no value in the supplied assignment."),
            Negation n => !Evaluate(n.Operand, assignment),
            Conjunction a => Evaluate(a.Left, assignment) && Evaluate(a.Right, assignment),
            Disjunction o => Evaluate(o.Left, assignment) || Evaluate(o.Right, assignment),
            Implication i => !Evaluate(i.Antecedent, assignment) || Evaluate(i.Consequent, assignment),
            Biconditional b => Evaluate(b.Left, assignment) == Evaluate(b.Right, assignment),
            _ => throw new InvalidOperationException($"Unknown formula node: {formula.GetType().Name}"),
        };
    }

    /// <summary>Collect the set of variable names appearing in the formula.</summary>
    public static IReadOnlySet<string> Variables(Formula formula)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        Walk(formula, set);
        return set;
    }

    private static void Walk(Formula f, HashSet<string> acc)
    {
        switch (f)
        {
            case BoolConst:
                return;
            case Variable v:
                acc.Add(v.Name);
                return;
            case Negation n:
                Walk(n.Operand, acc);
                return;
            case Conjunction a:
                Walk(a.Left, acc);
                Walk(a.Right, acc);
                return;
            case Disjunction o:
                Walk(o.Left, acc);
                Walk(o.Right, acc);
                return;
            case Implication i:
                Walk(i.Antecedent, acc);
                Walk(i.Consequent, acc);
                return;
            case Biconditional b:
                Walk(b.Left, acc);
                Walk(b.Right, acc);
                return;
        }
    }
}
