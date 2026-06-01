using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// Classical conjunctive-normal-form transformation by distribution.
/// Logically equivalent to the input but may be exponentially larger.
/// For preserving only satisfiability, see <see cref="TseitinTransformer"/>.
/// </summary>
public static class CnfTransformer
{
    /// <summary>Return a logically equivalent CNF (may be exponentially larger than the input).</summary>
    public static CnfFormula ToCnf(Formula formula)
    {
        var nnf = NnfTransformer.ToNnf(formula);
        var simplified = Evaluation.Simplifier.Simplify(nnf);
        var distributed = Distribute(simplified);
        return FlattenToCnf(distributed);
    }

    private static Formula Distribute(Formula formula)
    {
        switch (formula)
        {
            case Conjunction a:
                return new Conjunction(Distribute(a.Left), Distribute(a.Right));
            case Disjunction o:
                return DistributeOr(Distribute(o.Left), Distribute(o.Right));
            default:
                return formula;
        }
    }

    private static Formula DistributeOr(Formula left, Formula right)
    {
        if (left is Conjunction la)
        {
            return new Conjunction(
                DistributeOr(la.Left, right),
                DistributeOr(la.Right, right));
        }
        if (right is Conjunction ra)
        {
            return new Conjunction(
                DistributeOr(left, ra.Left),
                DistributeOr(left, ra.Right));
        }
        return new Disjunction(left, right);
    }

    private static CnfFormula FlattenToCnf(Formula formula)
    {
        var clauses = new List<Clause>();
        foreach (var conj in FlattenAnd(formula))
        {
            if (conj is BoolConst bc)
            {
                if (bc.Value)
                {
                    continue;
                }
                return CnfFormula.False;
            }
            var literals = new List<Literal>();
            var clauseOk = true;
            foreach (var disj in FlattenOr(conj))
            {
                switch (disj)
                {
                    case BoolConst dc:
                        if (dc.Value)
                        {
                            clauseOk = false;
                            break;
                        }
                        continue;
                    case Variable v:
                        literals.Add(Literal.Positive(v.Name));
                        break;
                    case Negation { Operand: Variable nv }:
                        literals.Add(Literal.Negative(nv.Name));
                        break;
                    default:
                        throw new InvalidOperationException($"Non-literal disjunct after distribution: {disj}");
                }
                if (!clauseOk)
                {
                    break;
                }
            }
            if (!clauseOk)
            {
                continue;
            }
            var clause = new Clause(literals);
            if (!clause.IsTautology())
            {
                clauses.Add(clause);
            }
        }
        return new CnfFormula(clauses);
    }

    private static IEnumerable<Formula> FlattenAnd(Formula f)
    {
        if (f is Conjunction a)
        {
            foreach (var sub in FlattenAnd(a.Left))
            {
                yield return sub;
            }
            foreach (var sub in FlattenAnd(a.Right))
            {
                yield return sub;
            }
        }
        else
        {
            yield return f;
        }
    }

    private static IEnumerable<Formula> FlattenOr(Formula f)
    {
        if (f is Disjunction o)
        {
            foreach (var sub in FlattenOr(o.Left))
            {
                yield return sub;
            }
            foreach (var sub in FlattenOr(o.Right))
            {
                yield return sub;
            }
        }
        else
        {
            yield return f;
        }
    }
}
