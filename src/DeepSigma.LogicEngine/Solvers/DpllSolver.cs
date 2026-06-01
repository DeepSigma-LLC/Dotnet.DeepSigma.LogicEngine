using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Solvers;

/// <summary>
/// DPLL (Davis–Putnam–Logemann–Loveland) SAT solver with unit propagation and
/// pure-literal elimination. Operates on CNF formulas.
/// </summary>
public sealed class DpllSolver : ISatSolver
{
    /// <summary>
    /// Solve a formula directly: prepare it to CNF and project the resulting
    /// model back onto the original variables. See <see cref="CnfPreparer"/>.
    /// </summary>
    public SatResult Solve(Formula formula, CancellationToken cancellationToken = default)
    {
        var prepared = CnfPreparer.Prepare(formula);
        var result = Solve(prepared.Cnf, cancellationToken);
        return result is { IsSatisfiable: true, Model: not null }
            ? SatResult.Satisfiable(CnfPreparer.Project(result.Model, prepared.OriginalVariables))
            : result;
    }

    /// <summary>Solve a CNF formula, returning satisfiability and (if satisfiable) a model. A cancelled token aborts with <see cref="OperationCanceledException"/>.</summary>
    public SatResult Solve(CnfFormula formula, CancellationToken cancellationToken = default)
    {
        var clauses = new List<HashSet<Literal>>(formula.Clauses.Count);
        foreach (var clause in formula.Clauses)
        {
            if (clause.IsTautology())
            {
                continue;
            }
            clauses.Add(new HashSet<Literal>(clause.Literals));
        }

        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        var result = Search(clauses, assignment, cancellationToken);
        if (result is null)
        {
            return SatResult.Unsatisfiable;
        }

        foreach (var v in formula.Variables())
        {
            if (!result.ContainsKey(v))
            {
                result[v] = false;
            }
        }
        return SatResult.Satisfiable(Model.From(result));
    }

    private static Dictionary<string, bool>? Search(List<HashSet<Literal>> clauses, Dictionary<string, bool> assignment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        while (true)
        {
            var unit = FindUnit(clauses);
            if (unit is null)
            {
                break;
            }
            if (!AssignAndSimplify(ref clauses, unit.Value))
            {
                return null;
            }
            assignment[unit.Value.Variable] = !unit.Value.Negated;
        }

        while (true)
        {
            var pure = FindPureLiteral(clauses);
            if (pure is null)
            {
                break;
            }
            if (!AssignAndSimplify(ref clauses, pure.Value))
            {
                return null;
            }
            assignment[pure.Value.Variable] = !pure.Value.Negated;
        }

        if (clauses.Count == 0)
        {
            return assignment;
        }
        if (clauses.Any(c => c.Count == 0))
        {
            return null;
        }

        var branchVar = PickBranchVariable(clauses);
        foreach (var value in new[] { true, false })
        {
            var lit = value ? Literal.Positive(branchVar) : Literal.Negative(branchVar);
            var nextClauses = CopyClauses(clauses);
            if (!AssignAndSimplify(ref nextClauses, lit))
            {
                continue;
            }
            var nextAssignment = new Dictionary<string, bool>(assignment, StringComparer.Ordinal)
            {
                [branchVar] = value,
            };
            var result = Search(nextClauses, nextAssignment, cancellationToken);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }

    private static Literal? FindUnit(List<HashSet<Literal>> clauses)
    {
        foreach (var clause in clauses)
        {
            if (clause.Count == 1)
            {
                return clause.First();
            }
        }
        return null;
    }

    private static Literal? FindPureLiteral(List<HashSet<Literal>> clauses)
    {
        var seenPos = new HashSet<string>(StringComparer.Ordinal);
        var seenNeg = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clause in clauses)
        {
            foreach (var lit in clause)
            {
                if (lit.Negated)
                {
                    seenNeg.Add(lit.Variable);
                }
                else
                {
                    seenPos.Add(lit.Variable);
                }
            }
        }
        foreach (var v in seenPos)
        {
            if (!seenNeg.Contains(v))
            {
                return Literal.Positive(v);
            }
        }
        foreach (var v in seenNeg)
        {
            if (!seenPos.Contains(v))
            {
                return Literal.Negative(v);
            }
        }
        return null;
    }

    private static string PickBranchVariable(List<HashSet<Literal>> clauses)
    {
        // Pick the variable in the smallest clause first — a simple heuristic
        // that tends to maximise propagation after each decision.
        HashSet<Literal>? smallest = null;
        foreach (var clause in clauses)
        {
            if (smallest is null || clause.Count < smallest.Count)
            {
                smallest = clause;
            }
        }
        return smallest!.First().Variable;
    }

    private static bool AssignAndSimplify(ref List<HashSet<Literal>> clauses, Literal lit)
    {
        var result = new List<HashSet<Literal>>(clauses.Count);
        var negated = lit.Negate();
        foreach (var clause in clauses)
        {
            if (clause.Contains(lit))
            {
                continue;
            }
            if (clause.Contains(negated))
            {
                var smaller = new HashSet<Literal>(clause);
                smaller.Remove(negated);
                if (smaller.Count == 0)
                {
                    clauses = result;
                    return false;
                }
                result.Add(smaller);
            }
            else
            {
                result.Add(clause);
            }
        }
        clauses = result;
        return true;
    }

    private static List<HashSet<Literal>> CopyClauses(List<HashSet<Literal>> clauses)
    {
        var copy = new List<HashSet<Literal>>(clauses.Count);
        foreach (var clause in clauses)
        {
            copy.Add(new HashSet<Literal>(clause));
        }
        return copy;
    }
}
