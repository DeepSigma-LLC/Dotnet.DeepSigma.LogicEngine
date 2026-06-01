using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Smt;
using DeepSigma.LogicEngine.Z3;
using DeepSigma.LogicEngine.Z3.Sorted;

Console.WriteLine("DeepSigma.LogicEngine — Z3 backend demo");
Console.WriteLine("=======================================");
Console.WriteLine("The optional Z3 engine solves the existing ASTs completely and fast.\n");

// 1. Propositional, via Z3.
Console.WriteLine("1. Propositional");
Console.WriteLine($"  (p -> q) <-> (!q -> !p) valid? {Z3Reasoner.IsValid(Formula.Parse("(p -> q) <-> (!q -> !p)"))}");

// 2. EUF.
Console.WriteLine("\n2. EUF (equality + uninterpreted functions)");
Console.WriteLine($"  a=b & b=c -> f(a)=f(c) valid? {Z3SmtSolver.IsValid(SmtFormula.Parse("a = b & b = c -> f(a) = f(c)"), Z3SmtTheory.Euf)}");

// 3. LRA — exact rational model.
Console.WriteLine("\n3. LRA (linear real arithmetic)");
var lra = Z3SmtSolver.Solve(LraParser.Parse("2*x = 1"), Z3SmtTheory.Lra);
Console.WriteLine($"  2*x = 1 -> {lra.Status}, x = {lra.Model!["x"]}");

// 4. LIA — UNBOUNDED, unlike the native bounded box.
Console.WriteLine("\n4. LIA (linear integer arithmetic) — unbounded");
var lia = Z3SmtSolver.Solve(LraParser.Parse("x = 100000"), Z3SmtTheory.Lia, new[] { "x" });
Console.WriteLine($"  x = 100000 -> {lia.Status}, x = {lia.Model!["x"]}  (native LIA box would miss this)");
Console.WriteLine($"  2*x = 1 over integers -> {Z3SmtSolver.Solve(LraParser.Parse("2*x = 1"), Z3SmtTheory.Lia, new[] { "x" }).Status}");

// 5. Combined EUF + LRA.
Console.WriteLine("\n5. Combined (EUF + LRA)");
var mixed = new SmtAnd(LraParser.Parse("x <= y & y <= x"), SmtParser.Parse("f(x) != f(y)"));
Console.WriteLine($"  x<=y & y<=x & f(x)!=f(y) satisfiable? {Z3SmtSolver.IsSatisfiable(mixed, Z3SmtTheory.Combined)}");

// 6. Arrays.
Console.WriteLine("\n6. Arrays (select / store)");
Console.WriteLine($"  select(store(a,i,v),i) = v valid? {Z3SmtSolver.IsValid(SmtParser.Parse("select(store(a, i, v), i) = v"), Z3SmtTheory.Arrays)}");

// 7. Cancellation / timeout knob (a capability the native engine lacks).
Console.WriteLine("\n7. Cancellation / timeout");
var timed = Z3SmtSolver.Solve(SmtFormula.Parse("a = b"), Z3SmtTheory.Euf, timeout: TimeSpan.FromSeconds(1));
Console.WriteLine($"  a = b (1s budget) -> {timed.Status}");

// 8. Bit-vectors (QF_BV) — a theory the native engine cannot express at all.
Console.WriteLine("\n8. Bit-vectors (8-bit) — Z3-only");
var x = SortedExpr.BitVecVar("x", 8);
var bv = Z3SortedSolver.Solve(SortedExpr.Eq(x + SortedExpr.BitVec(1, 8), SortedExpr.BitVec(0, 8)));
Console.WriteLine($"  x + 1 == 0 (mod 256) -> {bv.Status}, x = {bv.Model!["x"]}  (overflow wraps)");
Console.WriteLine($"  x & x == x valid? {Z3SortedSolver.IsValid(SortedExpr.Eq(x & x, x))}");

// 9. Nonlinear arithmetic + quantifiers — full SMT, Z3-only.
Console.WriteLine("\n9. Nonlinear arithmetic + quantifiers — Z3-only");
var n = SortedExpr.IntVar("n");
var sq = Z3SortedSolver.Solve(SortedExpr.And(SortedExpr.Eq(n * n, SortedExpr.Int(49)), SortedExpr.Gt(n, SortedExpr.Int(0))));
Console.WriteLine($"  n*n == 49 & n > 0 -> {sq.Status}, n = {sq.Model!["n"]}");
var q = SortedExpr.ForAll(n, SortedExpr.Gt(n + SortedExpr.Int(1), n));
Console.WriteLine($"  forall n. n + 1 > n valid? {Z3SortedSolver.IsValid(q)}");

// 10. Strings / sequences — Z3-only.
Console.WriteLine("\n10. Strings — Z3-only");
Console.WriteLine($"  \"foo\" ++ \"bar\" == \"foobar\" valid? {Z3SortedSolver.IsValid(SortedExpr.Eq(SortedExpr.StringConcat(SortedExpr.Str("foo"), SortedExpr.Str("bar")), SortedExpr.Str("foobar")))}");
var s = SortedExpr.StringVar("s");
var strQuery = SortedExpr.And(
    SortedExpr.Eq(SortedExpr.StringConcat(s, SortedExpr.Str("bar")), SortedExpr.Str("foobar")),
    SortedExpr.Eq(SortedExpr.Length(s), SortedExpr.Int(3)));
Console.WriteLine($"  exists s. s ++ \"bar\" == \"foobar\" & |s| == 3 satisfiable? {Z3SortedSolver.IsSatisfiable(strQuery)}  (s = \"foo\")");

// 11. Text syntax — parse the sorted theories from a string, and print them back.
Console.WriteLine("\n11. Text syntax (parse + print)");
var parsed = SortedExpr.Parse("bv8 x; x + 1 == 0");
Console.WriteLine($"  parse \"bv8 x; x + 1 == 0\" -> {Z3SortedSolver.Solve(parsed).Model!["x"]}");
Console.WriteLine($"  print round-trip: {SortedExpr.Parse("int n; n*n == 49 & n > 0")}");
Console.WriteLine($"  valid? forall int n . n + 1 > n -> {Z3SortedSolver.IsValid(SortedExpr.Parse("forall int n . n + 1 > n"))}");

Console.WriteLine("\nDone.");
