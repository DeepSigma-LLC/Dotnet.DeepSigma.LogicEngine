using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Solvers.Cdcl;

namespace DeepSigma.LogicEngine.Smt;

/// <summary>
/// A lazy DPLL(T) satisfiability solver for the theory of equality with
/// uninterpreted functions and predicates. The boolean structure is abstracted
/// to a propositional skeleton solved by the incremental CDCL solver; each
/// candidate model is checked by <see cref="CongruenceClosure"/>, and theory
/// conflicts are fed back as blocking clauses until a consistent model is found
/// or the skeleton becomes unsatisfiable.
/// </summary>
public static class EufSolver
{
    public static bool IsSatisfiable(SmtFormula formula) => Solve(formula).IsSatisfiable;

    public static bool IsUnsatisfiable(SmtFormula formula) => !IsSatisfiable(formula);

    /// <summary>True if the formula holds in every theory model.</summary>
    public static bool IsValid(SmtFormula formula) => !Solve(new SmtNot(formula)).IsSatisfiable;

    /// <summary>True if the knowledge base theory-entails the query.</summary>
    public static bool Entails(IEnumerable<SmtFormula> knowledgeBase, SmtFormula query)
        => !Solve(new SmtAnd(SmtFormula.All(knowledgeBase), new SmtNot(query))).IsSatisfiable;

    public static SmtResult Solve(SmtFormula formula)
    {
        var (skeleton, atoms) = Abstraction.Abstract(formula);
        var prepared = CnfPreparer.Prepare(skeleton);
        var solver = new IncrementalCdclSolver(prepared.Cnf, atoms.Names);

        while (true)
        {
            var propositional = solver.Solve();
            if (!propositional.IsSatisfiable || propositional.Model is null)
            {
                return SmtResult.Unsatisfiable;
            }

            var assignment = ReadAssignment(propositional.Model, atoms);
            var conflict = CheckTheory(assignment, atoms);
            if (conflict is null)
            {
                return SmtResult.Satisfiable(BuildModel(assignment, atoms));
            }

            var blocking = BlockingClause(conflict, assignment, atoms);
            if (!solver.AddClause(blocking))
            {
                return SmtResult.Unsatisfiable;
            }
        }
    }

    /// <summary>
    /// Check a conjunction of theory literals directly and return the minimal
    /// conflict core (as atoms / negated atoms), or null if it is consistent.
    /// Each input formula must be an atom or a negated atom.
    /// </summary>
    public static IReadOnlyList<SmtFormula>? ConflictCore(IEnumerable<SmtFormula> literals)
    {
        var list = literals.ToList();
        var cc = new CongruenceClosure();
        for (var i = 0; i < list.Count; i++)
        {
            cc.Assert(ToLiteral(list[i], i));
        }
        var core = cc.FindConflict();
        return core?.OrderBy(id => id).Select(id => list[id]).ToList();
    }

    private static bool[] ReadAssignment(IReadOnlyDictionary<string, bool> model, AtomTable atoms)
    {
        var values = new bool[atoms.Entries.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = model.TryGetValue(atoms[i].Name, out var v) && v;
        }
        return values;
    }

    private static IReadOnlySet<int>? CheckTheory(bool[] assignment, AtomTable atoms)
    {
        var cc = new CongruenceClosure();
        for (var i = 0; i < assignment.Length; i++)
        {
            var entry = atoms[i];
            cc.Assert(new EufLiteral(i, assignment[i], entry.Kind, entry.First, entry.Second));
        }
        return cc.FindConflict();
    }

    private static IReadOnlyList<Literal> BlockingClause(IReadOnlySet<int> conflict, bool[] assignment, AtomTable atoms)
    {
        // Forbid the current assignment of the conflicting atoms: each clause
        // literal is false under the model, i.e. its Negated flag equals the
        // value the atom-variable currently has.
        return conflict.Select(id => new Literal(atoms[id].Name, assignment[id])).ToList();
    }

    private static SmtModel BuildModel(bool[] assignment, AtomTable atoms)
    {
        var map = new Dictionary<SmtFormula, bool>();
        for (var i = 0; i < assignment.Length; i++)
        {
            map[atoms[i].Atom] = assignment[i];
        }
        return new SmtModel(map);
    }

    private static EufLiteral ToLiteral(SmtFormula literal, int atomId)
    {
        var positive = true;
        var atom = literal;
        if (atom is SmtNot n)
        {
            positive = false;
            atom = n.Operand;
        }
        return atom switch
        {
            EqualityAtom e => new EufLiteral(atomId, positive, EufAtomKind.Equality, e.Left, e.Right),
            PredicateAtom p => new EufLiteral(atomId, positive, EufAtomKind.Predicate, new Term(p.Symbol, p.Arguments), new Term(p.Symbol, p.Arguments)),
            _ => throw new ArgumentException($"Not a theory literal: {literal}"),
        };
    }
}
