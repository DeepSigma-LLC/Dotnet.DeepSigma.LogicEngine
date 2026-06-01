using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Solvers;
using MZ3 = Microsoft.Z3;

namespace DeepSigma.LogicEngine.Z3;

/// <summary>
/// A Z3-backed <see cref="ISatSolver"/>: solves a <see cref="CnfFormula"/> with Z3 instead of the
/// native CDCL engine. Because the project's encoder logics (modal, LTL, finite-sets,
/// finite-groups) reach SAT through <see cref="Reasoning.Reasoner"/>'s <see cref="ISatSolver"/>
/// overloads, passing an instance of this class runs those logics on Z3 — with no changes to them
/// and without exposing any internals.
/// </summary>
public sealed class Z3SatSolver : ISatSolver
{
    /// <summary>
    /// Solves the CNF formula with Z3 and returns a <see cref="SatResult"/> (a satisfying
    /// <see cref="Model"/> when SAT). Throws <see cref="InvalidOperationException"/> in the
    /// theoretically-shouldn't-happen case that Z3 reports UNKNOWN for a purely propositional query.
    /// </summary>
    public SatResult Solve(CnfFormula formula)
    {
        // ISatSolver has no timeout/cancellation; reuse Z3Session purely for Context/Solver lifetime.
        using var session = new Z3Session(timeout: null, cancellationToken: default);
        var context = session.Context;
        var solver = session.Solver;

        var constants = new Dictionary<string, MZ3.BoolExpr>(StringComparer.Ordinal);
        MZ3.BoolExpr Var(string name)
        {
            if (!constants.TryGetValue(name, out var c))
            {
                c = context.MkBoolConst(name);
                constants[name] = c;
            }
            return c;
        }

        foreach (var clause in formula.Clauses)
        {
            if (clause.Literals.Count == 0)
            {
                solver.Assert(context.MkFalse());
                continue;
            }
            var literals = clause.Literals
                .Select(l => l.Negated ? context.MkNot(Var(l.Variable)) : Var(l.Variable))
                .ToArray();
            solver.Assert(context.MkOr(literals));
        }
        foreach (var variable in formula.Variables())
        {
            Var(variable); // ensure every variable is in the model, even if it appears in no clause
        }

        var status = solver.Check();
        if (status == MZ3.Status.UNSATISFIABLE)
        {
            return SatResult.Unsatisfiable;
        }
        if (status != MZ3.Status.SATISFIABLE)
        {
            throw new InvalidOperationException("Z3 returned UNKNOWN for a propositional query.");
        }

        var model = solver.Model;
        var assignment = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (name, constant) in constants)
        {
            assignment[name] = model.Eval(constant, completion: true).IsTrue;
        }
        return SatResult.Satisfiable(Model.From(assignment));
    }
}
