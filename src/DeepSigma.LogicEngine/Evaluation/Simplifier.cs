using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Evaluation;

/// <summary>
/// Bottom-up simplifier applying constant folding and idempotence/complementation laws.
/// The result is logically equivalent to the input.
/// </summary>
public static class Simplifier
{
    public static Formula Simplify(Formula formula)
    {
        return formula switch
        {
            BoolConst => formula,
            Variable => formula,
            Negation n => SimplifyNot(Simplify(n.Operand)),
            Conjunction a => SimplifyAnd(Simplify(a.Left), Simplify(a.Right)),
            Disjunction o => SimplifyOr(Simplify(o.Left), Simplify(o.Right)),
            Implication i => SimplifyImplies(Simplify(i.Antecedent), Simplify(i.Consequent)),
            Biconditional b => SimplifyIff(Simplify(b.Left), Simplify(b.Right)),
            _ => formula,
        };
    }

    private static Formula SimplifyNot(Formula f) => f switch
    {
        BoolConst c => Formula.Const(!c.Value),
        Negation n => n.Operand,
        _ => new Negation(f),
    };

    private static Formula SimplifyAnd(Formula a, Formula b)
    {
        if (a is BoolConst ac)
        {
            return ac.Value ? b : Formula.False;
        }
        if (b is BoolConst bc)
        {
            return bc.Value ? a : Formula.False;
        }
        if (a.Equals(b))
        {
            return a;
        }
        if (IsComplement(a, b))
        {
            return Formula.False;
        }
        return new Conjunction(a, b);
    }

    private static Formula SimplifyOr(Formula a, Formula b)
    {
        if (a is BoolConst ac)
        {
            return ac.Value ? Formula.True : b;
        }
        if (b is BoolConst bc)
        {
            return bc.Value ? Formula.True : a;
        }
        if (a.Equals(b))
        {
            return a;
        }
        if (IsComplement(a, b))
        {
            return Formula.True;
        }
        return new Disjunction(a, b);
    }

    private static Formula SimplifyImplies(Formula a, Formula b)
    {
        if (a is BoolConst ac)
        {
            return ac.Value ? b : Formula.True;
        }
        if (b is BoolConst bc)
        {
            return bc.Value ? Formula.True : SimplifyNot(a);
        }
        if (a.Equals(b))
        {
            return Formula.True;
        }
        return new Implication(a, b);
    }

    private static Formula SimplifyIff(Formula a, Formula b)
    {
        if (a is BoolConst ac)
        {
            return ac.Value ? b : SimplifyNot(b);
        }
        if (b is BoolConst bc)
        {
            return bc.Value ? a : SimplifyNot(a);
        }
        if (a.Equals(b))
        {
            return Formula.True;
        }
        if (IsComplement(a, b))
        {
            return Formula.False;
        }
        return new Biconditional(a, b);
    }

    private static bool IsComplement(Formula a, Formula b)
        => (a is Negation na && na.Operand.Equals(b))
        || (b is Negation nb && nb.Operand.Equals(a));
}
