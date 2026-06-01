using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// Converts a formula to Negation Normal Form: only AND, OR, NOT remain;
/// implications and biconditionals are eliminated, and negations are pushed
/// to the leaves.
/// </summary>
public static class NnfTransformer
{
    /// <summary>Return a logically equivalent formula in negation normal form.</summary>
    public static Formula ToNnf(Formula formula) => ToNnf(formula, negate: false);

    private static Formula ToNnf(Formula formula, bool negate)
    {
        switch (formula)
        {
            case BoolConst c:
                return Formula.Const(negate ? !c.Value : c.Value);
            case Variable v:
                return negate ? new Negation(v) : v;
            case Negation n:
                return ToNnf(n.Operand, !negate);
            case Conjunction a:
                return negate
                    ? new Disjunction(ToNnf(a.Left, true), ToNnf(a.Right, true))
                    : new Conjunction(ToNnf(a.Left, false), ToNnf(a.Right, false));
            case Disjunction o:
                return negate
                    ? new Conjunction(ToNnf(o.Left, true), ToNnf(o.Right, true))
                    : new Disjunction(ToNnf(o.Left, false), ToNnf(o.Right, false));
            case Implication i:
                // a -> b  ==  !a | b
                return negate
                    ? new Conjunction(ToNnf(i.Antecedent, false), ToNnf(i.Consequent, true))
                    : new Disjunction(ToNnf(i.Antecedent, true), ToNnf(i.Consequent, false));
            case Biconditional b:
                // a <-> b  ==  (a & b) | (!a & !b)
                var leftPos = ToNnf(b.Left, false);
                var leftNeg = ToNnf(b.Left, true);
                var rightPos = ToNnf(b.Right, false);
                var rightNeg = ToNnf(b.Right, true);
                if (negate)
                {
                    // !(a <-> b) == (a & !b) | (!a & b)
                    return new Disjunction(
                        new Conjunction(leftPos, rightNeg),
                        new Conjunction(leftNeg, rightPos));
                }
                return new Disjunction(
                    new Conjunction(leftPos, rightPos),
                    new Conjunction(leftNeg, rightNeg));
            default:
                throw new InvalidOperationException($"Unknown formula node: {formula.GetType().Name}");
        }
    }
}
