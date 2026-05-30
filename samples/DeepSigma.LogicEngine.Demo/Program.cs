using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;

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
