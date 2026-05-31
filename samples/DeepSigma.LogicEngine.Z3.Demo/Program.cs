using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Smt;
using DeepSigma.LogicEngine.Z3;

Console.WriteLine("DeepSigma.LogicEngine — Z3 backend demo");
Console.WriteLine("=======================================");
Console.WriteLine("The optional Z3 engine solves the existing ASTs completely and fast.\n");

// 1. Propositional, via Z3.
Console.WriteLine("1. Propositional");
Console.WriteLine($"  (p -> q) <-> (!q -> !p) valid? {Z3Reasoner.IsValid(Formula.Parse("(p -> q) <-> (!q -> !p)"))}");

// 2. EUF.
Console.WriteLine("\n2. EUF (equality + uninterpreted functions)");
Console.WriteLine($"  a=b & b=c -> f(a)=f(c) valid? {Z3Smt.IsValid(SmtFormula.Parse("a = b & b = c -> f(a) = f(c)"), Z3SmtTheory.Euf)}");

// 3. LRA — exact rational model.
Console.WriteLine("\n3. LRA (linear real arithmetic)");
var lra = Z3Smt.Solve(LraParser.Parse("2*x = 1"), Z3SmtTheory.Lra);
Console.WriteLine($"  2*x = 1 -> {lra.Status}, x = {lra.Model!["x"]}");

// 4. LIA — UNBOUNDED, unlike the native bounded box.
Console.WriteLine("\n4. LIA (linear integer arithmetic) — unbounded");
var lia = Z3Smt.Solve(LraParser.Parse("x = 100000"), Z3SmtTheory.Lia, new[] { "x" });
Console.WriteLine($"  x = 100000 -> {lia.Status}, x = {lia.Model!["x"]}  (native LIA box would miss this)");
Console.WriteLine($"  2*x = 1 over integers -> {Z3Smt.Solve(LraParser.Parse("2*x = 1"), Z3SmtTheory.Lia, new[] { "x" }).Status}");

// 5. Combined EUF + LRA.
Console.WriteLine("\n5. Combined (EUF + LRA)");
var mixed = new SmtAnd(LraParser.Parse("x <= y & y <= x"), SmtParser.Parse("f(x) != f(y)"));
Console.WriteLine($"  x<=y & y<=x & f(x)!=f(y) satisfiable? {Z3Smt.IsSatisfiable(mixed, Z3SmtTheory.Combined)}");

// 6. Arrays.
Console.WriteLine("\n6. Arrays (select / store)");
Console.WriteLine($"  select(store(a,i,v),i) = v valid? {Z3Smt.IsValid(SmtParser.Parse("select(store(a, i, v), i) = v"), Z3SmtTheory.Arrays)}");

// 7. Cancellation / timeout knob (a capability the native engine lacks).
Console.WriteLine("\n7. Cancellation / timeout");
var timed = Z3Smt.Solve(SmtFormula.Parse("a = b"), Z3SmtTheory.Euf, timeout: TimeSpan.FromSeconds(1));
Console.WriteLine($"  a = b (1s budget) -> {timed.Status}");

Console.WriteLine("\nDone.");
