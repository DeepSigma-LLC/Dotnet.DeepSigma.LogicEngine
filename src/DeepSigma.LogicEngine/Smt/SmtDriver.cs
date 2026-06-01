using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Solvers.Cdcl;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// The theory-agnostic lazy DPLL(T) loop. Abstracts an <see cref="SmtFormula"/>
/// to a propositional skeleton, solves it with the incremental CDCL solver, and
/// checks each candidate model with the supplied <see cref="ITheory"/>; theory
/// conflicts become permanent blocking clauses until a consistent model is found
/// or the skeleton is unsatisfiable.
/// </summary>
internal static class SmtDriver
{
    public static SmtResult Solve(SmtFormula formula, ITheory theory, CancellationToken cancellationToken = default)
    {
        var (skeleton, atoms) = Abstraction.Abstract(formula);
        var prepared = CnfPreparer.Prepare(skeleton);
        var solver = new IncrementalCdclSolver(prepared.Cnf, atoms.Names);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var propositional = solver.Solve(cancellationToken);
            if (!propositional.IsSatisfiable || propositional.Model is null)
            {
                return SmtResult.Unsatisfiable;
            }

            var asserted = ReadAssignment(propositional.Model, atoms);
            var conflict = theory.Check(asserted);
            if (conflict is null)
            {
                return SmtResult.Satisfiable(BuildModel(asserted));
            }

            var blocking = conflict.Select(i => new Literal(atoms[i].Name, asserted[i].Value)).ToList();
            if (!solver.AddClause(blocking))
            {
                return SmtResult.Unsatisfiable;
            }
        }
    }

    /// <summary>
    /// Check a conjunction of theory literals directly and return the minimal
    /// conflict core (atoms / negated atoms), or null if consistent. Each input
    /// must be an atom or a negated atom.
    /// </summary>
    public static IReadOnlyList<SmtFormula>? ConflictCore(IEnumerable<SmtFormula> literals, ITheory theory)
    {
        var list = literals.ToList();
        var asserted = new (SmtFormula Atom, bool Value)[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            asserted[i] = ToAtomValue(list[i]);
        }
        var core = theory.Check(asserted);
        return core?.OrderBy(i => i).Select(i => list[i]).ToList();
    }

    private static (SmtFormula Atom, bool Value)[] ReadAssignment(IReadOnlyDictionary<string, bool> model, AtomTable atoms)
    {
        var asserted = new (SmtFormula, bool)[atoms.Entries.Count];
        for (var i = 0; i < asserted.Length; i++)
        {
            var entry = atoms[i];
            asserted[i] = (entry.Atom, model.TryGetValue(entry.Name, out var v) && v);
        }
        return asserted;
    }

    private static (SmtFormula Atom, bool Value) ToAtomValue(SmtFormula literal)
        => literal is SmtNot n ? (n.Operand, false) : (literal, true);

    private static SmtModel BuildModel(IReadOnlyList<(SmtFormula Atom, bool Value)> asserted)
    {
        var map = new Dictionary<SmtFormula, bool>();
        foreach (var (atom, value) in asserted)
        {
            map[atom] = value;
        }
        return new SmtModel(map);
    }
}
