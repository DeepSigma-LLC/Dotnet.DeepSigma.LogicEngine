using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.FiniteGroups;
using DeepSigma.LogicEngine.FiniteSets;
using DeepSigma.LogicEngine.Fuzzy;
using DeepSigma.LogicEngine.Probabilistic;
using DeepSigma.LogicEngine.Reasoning;
using DeepSigma.LogicEngine.Smt;
using DeepSigma.LogicEngine.Modal;
using DeepSigma.LogicEngine.Solvers.MaxSat;
using DeepSigma.LogicEngine.Temporal;
using DeepSigma.LogicEngine.Transitions;
using DeepSigma.Mathematics.Algebra;

Section("1. Parsing and pretty-printing");
{
    var f = Formula.Parse("(p -> q) & (q -> r) -> (p -> r)");
    Console.WriteLine($"  parsed: {f}");
    Console.WriteLine($"  variables: [{string.Join(", ", Evaluator.Variables(f))}]");
}

Section("2. Truth table");
{
    var f = Formula.Parse("p -> (q | !r)");
    Console.WriteLine(Indent(TruthTable.Format(f)));
}

Section("3. Satisfiability and one model");
{
    var f = Formula.Parse("(p | q) & (!p | r) & (!q | !r)");
    Console.WriteLine($"  formula: {f}");
    var model = Reasoner.FindModel(f);
    Console.WriteLine($"  satisfiable: {model is not null}");
    if (model is not null)
    {
        Console.WriteLine($"  model: {model}");
    }
}

Section("4. Validity, equivalence, and model counting");
{
    var contrapositive = Formula.Parse("(p -> q) <-> (!q -> !p)");
    Console.WriteLine($"  contrapositive is valid? {Reasoner.IsValid(contrapositive)}");
    var deMorgan = (Formula.Parse("!(p & q)"), Formula.Parse("!p | !q"));
    Console.WriteLine($"  De Morgan equivalent? {Reasoner.AreEquivalent(deMorgan.Item1, deMorgan.Item2)}");
    var countable = Formula.Parse("p | q");
    Console.WriteLine($"  models of (p | q): {Reasoner.CountModels(countable)}");
    foreach (var m in Reasoner.EnumerateModels(countable))
    {
        Console.WriteLine($"    {m}");
    }
}

Section("5. Entailment with a small knowledge base");
{
    var kb = new[]
    {
        Formula.Parse("rains -> wet_ground"),
        Formula.Parse("sprinkler -> wet_ground"),
        Formula.Parse("rains | sprinkler"),
    };
    Console.WriteLine("  KB:");
    foreach (var f in kb)
    {
        Console.WriteLine($"    {f}");
    }
    Console.WriteLine($"  KB |= wet_ground ? {Reasoner.Entails(kb, Formula.Parse("wet_ground"))}");
    Console.WriteLine($"  KB |= rains       ? {Reasoner.Entails(kb, Formula.Parse("rains"))}");
}

Section("6. Resolution refutation proof");
{
    var kb = new[]
    {
        Formula.Parse("p"),
        Formula.Parse("p -> q"),
        Formula.Parse("q -> r"),
    };
    var query = Formula.Parse("r");
    var result = ResolutionRefuter.Refute(kb, query);
    Console.WriteLine($"  KB |= r ? {result.IsRefuted}");
    if (result.Proof is not null)
    {
        Console.Write(Indent(result.Proof.Render()));
    }
}

Section("7. Horn forward chaining");
{
    var kb = new HornClause[]
    {
        new(Array.Empty<string>(), "rains"),
        new(new[] { "rains" }, "wet_ground"),
        new(new[] { "wet_ground" }, "slippery"),
        new(new[] { "slippery", "running" }, "fall"),
    };
    Console.WriteLine("  KB:");
    foreach (var c in kb)
    {
        Console.WriteLine($"    {c}");
    }
    var inferred = ForwardChainer.Infer(kb);
    Console.WriteLine($"  inferred: {{ {string.Join(", ", inferred.OrderBy(s => s))} }}");
}

Section("8. Horn backward chaining proof tree");
{
    var kb = new HornClause[]
    {
        new(Array.Empty<string>(), "battery_ok"),
        new(Array.Empty<string>(), "fuel_ok"),
        new(new[] { "battery_ok", "fuel_ok" }, "engine_starts"),
        new(new[] { "engine_starts" }, "car_runs"),
    };
    var proof = BackwardChainer.Prove(kb, "car_runs");
    Console.WriteLine($"  proved car_runs? {proof is not null}");
    if (proof is not null)
    {
        Console.Write(Indent(proof.Render()));
    }
}

Section("9. SMT (EUF): equality with uninterpreted functions");
{
    var valid = SmtFormula.Parse("a = b & b = c -> f(a) = f(c)");
    Console.WriteLine($"  {valid}");
    Console.WriteLine($"  valid? {EufSolver.IsValid(valid)}");

    var sat = SmtFormula.Parse("(a = b | c = d) & f(a) != f(b)");
    var result = EufSolver.Solve(sat);
    Console.WriteLine($"  {sat}");
    Console.WriteLine($"  satisfiable? {result.IsSatisfiable}");
    if (result.Model is not null)
    {
        Console.WriteLine($"  true atoms: {{ {string.Join(", ", result.Model.TrueAtoms)} }}");
    }
}

Section("10. SMT (EUF): minimal conflict core");
{
    var literals = new[]
    {
        SmtFormula.Parse("a = b"),
        SmtFormula.Parse("c = d"),      // irrelevant to the conflict
        SmtFormula.Parse("b = c"),
        SmtFormula.Parse("f(a) != f(c)"),
    };
    Console.WriteLine($"  literals: {string.Join(", ", literals.Select(l => l.ToString()))}");
    var core = EufSolver.ConflictCore(literals);
    Console.WriteLine(core is null
        ? "  consistent"
        : $"  inconsistent; minimal core: {{ {string.Join(", ", core)} }}");
}

Section("11. SMT (LRA): linear real arithmetic");
{
    var unsat = LraParser.Parse("x >= 1 & y >= 1 & x + y <= 1");
    Console.WriteLine($"  {unsat}");
    Console.WriteLine($"  satisfiable? {LraSolver.IsSatisfiable(unsat)}");

    var valid = LraParser.Parse("x <= 5 -> x <= 6");
    Console.WriteLine($"  (x <= 5 -> x <= 6) valid? {LraSolver.IsValid(valid)}");
}

Section("12. MaxSAT: optimize over soft constraints");
{
    // Hard: a or b. Soft: prefer !a (w1) and !b (w1). Best gives up one → cost 1.
    var hard = new[] { new[] { Literal.Positive("a"), Literal.Positive("b") } };
    var soft = new[]
    {
        new SoftClause(new[] { Literal.Negative("a") }, 1),
        new SoftClause(new[] { Literal.Negative("b") }, 1),
    };
    var result = new MaxSatSolver(hard, soft).Solve();
    Console.WriteLine($"  optimum cost: {result.Cost}");
    Console.WriteLine($"  a={result.Model["a"]}, b={result.Model["b"]}");
}

Section("13. Temporal (LTL): bounded model checking");
{
    var f = LtlFormula.Parse("G F a");   // a holds infinitely often
    var sat = BoundedModelChecker.CheckSatisfiable(f, maxBound: 4);
    Console.WriteLine($"  '{f}' satisfiable? {sat.Found} (lasso bound {sat.Bound}, loops at {sat.Trace?.LoopStart})");

    // A toggle system; check whether 'x is never true' holds (it doesn't).
    var toggle = new TransitionSystem(
        new[] { "x" },
        Initial: new DeepSigma.LogicEngine.Formulas.Negation(Formula.Var("x")),
        Transition: new DeepSigma.LogicEngine.Formulas.Biconditional(
            Formula.Var("x'"), new DeepSigma.LogicEngine.Formulas.Negation(Formula.Var("x"))));
    var counterexample = BoundedModelChecker.FindCounterexample(toggle, LtlFormula.Parse("G !x"), maxBound: 4);
    var trace = counterexample is null ? "none" :
        string.Join(" -> ", counterexample.States.Select(s => s["x"] ? "x" : "!x"));
    Console.WriteLine($"  toggle violates 'G !x'? {counterexample is not null}  trace: {trace}");
}

Section("14. Modal logic (K/T/S4/S5)");
{
    var t = ModalParser.Parse("[]p -> p");        // T axiom
    Console.WriteLine($"  '{t}' valid in K?  {ModalSolver.IsValid(t, ModalSystem.K)}");
    Console.WriteLine($"  '{t}' valid in T?  {ModalSolver.IsValid(t, ModalSystem.T)}");

    var five = ModalParser.Parse("<>p -> []<>p"); // 5 axiom
    Console.WriteLine($"  '{five}' valid in S4? {ModalSolver.IsValid(five, ModalSystem.S4)}");
    Console.WriteLine($"  '{five}' valid in S5? {ModalSolver.IsValid(five, ModalSystem.S5)}");
}

Section("15. Fuzzy logic (Gödel / Łukasiewicz)");
{
    var p = FuzzyFormula.Var("p");
    var excludedMiddle = p | !p;        // p ∨ ¬p
    Console.WriteLine($"  'p | !p' a tautology in Gödel?      {FuzzySolver.IsValid(excludedMiddle, FuzzyLogic.Godel)}");
    Console.WriteLine($"  'p | !p' a tautology in Łukasiewicz? {FuzzySolver.IsValid(excludedMiddle, FuzzyLogic.Lukasiewicz)}");

    // Can a Gödel conjunction reach 1/2? (min(p,q) ≥ 1/2 is achievable)
    var conj = FuzzyFormula.Var("p") & FuzzyFormula.Var("q");
    Console.WriteLine($"  Gödel 'p & q' reaches 1/2? {FuzzySolver.IsSatisfiable(conj, FuzzyLogic.Godel, Rational.Of(1, 2))}");
}

Section("16. Probabilistic SAT (PSAT): coherence and bounds");
{
    // P(p) = 1/2 and P(p -> q) = 1 pin P(q) into [1/2, 1].
    var kb = new[]
    {
        ProbabilityConstraint.Exactly(Formula.Parse("p"), Rational.Of(1, 2)),
        ProbabilityConstraint.Exactly(Formula.Parse("p -> q"), Rational.Of(1, 1)),
    };
    Console.WriteLine($"  coherent (enumeration)?      {PsatSolver.IsConsistent(kb)}");
    Console.WriteLine($"  coherent (column generation)? {PsatSolver.IsConsistentScalable(kb)}");
    var bounds = PsatSolver.Bounds(kb, Formula.Parse("q"));
    Console.WriteLine($"  P(q) ∈ [{bounds!.Value.Low}, {bounds.Value.High}]  (probabilistic modus ponens, enumeration)");
    var scalable = PsatSolver.BoundsScalable(kb, Formula.Parse("q"));
    Console.WriteLine($"  P(q) ∈ [{scalable!.Value.Low}, {scalable.Value.High}]  (column generation)");

    var incoherent = new[]
    {
        ProbabilityConstraint.Exactly(Formula.Parse("p"), Rational.Of(3, 10)),
        ProbabilityConstraint.Exactly(Formula.Parse("!p"), Rational.Of(1, 2)),
    };
    Console.WriteLine($"  P(p)=3/10 & P(!p)=1/2 coherent? {PsatSolver.IsConsistent(incoherent)}");
}

Section("17. Finite set logic");
{
    var deMorgan = SetFormula.Parse("~(A ∪ B) = ~A ∩ ~B");
    Console.WriteLine($"  '{deMorgan}' valid? {FiniteSetsSolver.IsValid(deMorgan)}");

    // A ⊆ B forces |A| ≤ |B|, so this is unsatisfiable.
    var clash = SetFormula.Parse("A subset B & |A| = 3 & |B| = 2");
    Console.WriteLine($"  '{clash}' satisfiable? {FiniteSetsSolver.IsSatisfiable(clash, universe: 4)}");

    var model = FiniteSetsSolver.FindModel(SetFormula.Parse("x in A & A subset B & |B| = 2"), universe: 3);
    if (model is not null)
    {
        var a = string.Join(",", model.Sets["A"]);
        var b = string.Join(",", model.Sets["B"]);
        Console.WriteLine($"  model: x={model.Elements["x"]}, A={{{a}}}, B={{{b}}} (universe {model.Universe})");
    }
}

Section("18. Finite group theory (SAT model finding)");
{
    Console.Write("  groups up to isomorphism, orders 1..6:");
    foreach (var order in Enumerable.Range(1, 6))
    {
        Console.Write($" {GroupFinder.CountGroupsUpToIsomorphism(order)}");
    }
    Console.WriteLine("   (= 1 1 1 2 1 2)");

    var s3 = GroupFinder.FindGroup(6, new GroupSpec { Abelian = false });
    Console.WriteLine($"  smallest non-abelian group (order 6) found? {s3 is not null}; ≅ S₃? {s3?.IsIsomorphicTo(GroupTables.SymmetricGroup(3))}");
}

return;

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(title);
    Console.WriteLine(new string('-', title.Length));
}

static string Indent(string text)
{
    var lines = text.Replace("\r\n", "\n").Split('\n');
    return string.Join(Environment.NewLine, lines.Select(l => l.Length == 0 ? l : "  " + l));
}
