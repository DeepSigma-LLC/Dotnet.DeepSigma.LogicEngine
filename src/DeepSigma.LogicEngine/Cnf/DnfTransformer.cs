using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// Classical disjunctive-normal-form transformation by distribution.
/// Returns a logically equivalent formula structured as OR of ANDs of literals.
/// </summary>
public static class DnfTransformer
{
    public static Formula ToDnf(Formula formula)
    {
        var nnf = NnfTransformer.ToNnf(formula);
        var simplified = Evaluation.Simplifier.Simplify(nnf);
        return Distribute(simplified);
    }

    private static Formula Distribute(Formula formula)
    {
        switch (formula)
        {
            case Disjunction o:
                return new Disjunction(Distribute(o.Left), Distribute(o.Right));
            case Conjunction a:
                return DistributeAnd(Distribute(a.Left), Distribute(a.Right));
            default:
                return formula;
        }
    }

    private static Formula DistributeAnd(Formula left, Formula right)
    {
        if (left is Disjunction lo)
        {
            return new Disjunction(
                DistributeAnd(lo.Left, right),
                DistributeAnd(lo.Right, right));
        }
        if (right is Disjunction ro)
        {
            return new Disjunction(
                DistributeAnd(left, ro.Left),
                DistributeAnd(left, ro.Right));
        }
        return new Conjunction(left, right);
    }
}
