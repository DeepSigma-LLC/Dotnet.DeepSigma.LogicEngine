# Dotnet.DeepSigma.LogicEngine

A .NET 10 logic engine for **propositional logic** and **SMT-lite (EUF)**. It started as a propositional satisfiability / entailment library and has grown into a small but real reasoning stack:

- A **formula language** with a text parser, pretty-printer, evaluator, simplifier, and truth tables.
- Normal-form transformations — **NNF, CNF (classical + Tseitin), DNF**.
- Three SAT solvers: a brute-force **truth-table** oracle, classic **DPLL**, and a modern **CDCL** solver (watched literals, 1-UIP clause learning, VSIDS, Luby restarts, learned-clause deletion) with an **incremental** variant supporting assumptions.
- High-level **reasoning**: satisfiability, validity, equivalence, entailment, model finding, model enumeration, and model counting.
- A **resolution** refutation prover with set-of-support and subsumption, producing readable proofs.
- **Horn-clause** forward and backward chaining with proof trees.
- **Cardinality constraints** — pairwise/binomial (model-counting safe) and a linear **sequential-counter** encoding.
- **DIMACS** CNF read/write.
- **SMT-lite for EUF** — equality with uninterpreted functions and predicates — via a lazy **DPLL(T)** loop over the CDCL solver and a proof-producing **congruence closure**.

Everything is pure managed code, no native dependencies. The solution builds warnings-as-errors and ships with **241 tests**.

---

## Table of contents

- [Install / getting started](#install--getting-started)
- [Project layout](#project-layout)
- [Quick tour](#quick-tour)
- [Building formulas](#building-formulas)
- [Solving: SAT, validity, entailment](#solving-sat-validity-entailment)
- [Models, enumeration, counting](#models-enumeration-counting)
- [Truth tables and normal forms](#truth-tables-and-normal-forms)
- [SAT solvers directly](#sat-solvers-directly)
- [Incremental solving and assumptions](#incremental-solving-and-assumptions)
- [Cardinality constraints](#cardinality-constraints)
- [DIMACS](#dimacs)
- [Resolution proofs](#resolution-proofs)
- [Horn clauses](#horn-clauses)
- [SMT-lite: equality + uninterpreted functions (EUF)](#smt-lite-equality--uninterpreted-functions-euf)
- [Worked examples (samples)](#worked-examples-samples)
- [Operator and syntax reference](#operator-and-syntax-reference)
- [Performance notes](#performance-notes)
- [Limitations](#limitations)
- [Building and testing](#building-and-testing)
- [Roadmap](#roadmap)
- [License](#license)

---

## Install / getting started

Requires the **.NET 10 SDK**.

The library is not yet published to NuGet; reference the project directly:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/src/DeepSigma.LogicEngine/DeepSigma.LogicEngine.csproj" />
</ItemGroup>
```

Smallest possible program — is a formula a tautology?

```csharp
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;

var deMorgan = Formula.Parse("!(p & q) <-> (!p | !q)");
Console.WriteLine(Reasoner.IsValid(deMorgan));   // True
```

---

## Project layout

```
src/DeepSigma.LogicEngine/        the library
  Formulas/        Formula AST, Model, operator overloads, factories
  Parsing/         recursive-descent parser
  Printing/        precedence-aware printer
  Evaluation/      Evaluator, Simplifier, TruthTable
  Cnf/             Literal, Clause, CnfFormula, NNF/CNF/DNF/Tseitin, DIMACS
  Solvers/         ISatSolver, TruthTableSolver, DpllSolver, SatResult, options/stats
  Solvers/Cdcl/    CDCL solver internals + IncrementalCdclSolver
  Reasoning/       Reasoner, ResolutionRefuter, Horn chaining
  Encoding/        Cardinality, SequentialCounter
  Smt/             Term, SmtFormula, parser, CongruenceClosure, EufSolver
tests/DeepSigma.LogicEngine.Tests/    xUnit v3 test suite (+ DIMACS benchmarks)
samples/DeepSigma.LogicEngine.Demo/    guided feature walkthrough
samples/DeepSigma.LogicEngine.Recipes/ N-queens, Sudoku, graph coloring
```

Common namespaces:

```csharp
using DeepSigma.LogicEngine.Formulas;     // Formula, Model
using DeepSigma.LogicEngine.Reasoning;    // Reasoner, ResolutionRefuter, Horn*
using DeepSigma.LogicEngine.Evaluation;   // TruthTable, Evaluator, Simplifier
using DeepSigma.LogicEngine.Cnf;          // Dimacs, CnfTransformer, Literal, CnfFormula
using DeepSigma.LogicEngine.Solvers;      // SatResult, DpllSolver, TruthTableSolver
using DeepSigma.LogicEngine.Solvers.Cdcl; // CdclSolver, IncrementalCdclSolver
using DeepSigma.LogicEngine.Encoding;     // Cardinality, SequentialCounter
using DeepSigma.LogicEngine.Smt;          // EufSolver, SmtFormula, Term
```

---

## Quick tour

```csharp
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;

// Validity (tautology)
Reasoner.IsValid(Formula.Parse("(p -> q) <-> (!q -> !p)"));      // True (contrapositive)

// Entailment: does the knowledge base prove the query?
var kb = new[] { Formula.Parse("p"), Formula.Parse("p -> q") };
Reasoner.Entails(kb, Formula.Parse("q"));                        // True  (modus ponens)

// Satisfiability + a model
var f = Formula.Parse("(p | q) & (!p | r) & (!q | !r)");
Model? m = Reasoner.FindModel(f);                                // e.g. { p=F, q=T, r=F }

// Model counting
Reasoner.CountModels(Formula.Parse("p | q"));                    // 3
```

---

## Building formulas

Three interchangeable ways to construct a `Formula`:

```csharp
using DeepSigma.LogicEngine.Formulas;

// 1. Parse from text
Formula a = Formula.Parse("p & (q | !r) -> s");

// 2. Operator overloads (! & |) plus factory helpers
Formula p = Formula.Var("p"), q = Formula.Var("q"), r = Formula.Var("r"), s = Formula.Var("s");
Formula b = Formula.Implies(p & (q | !r), s);

// 3. Big conjunctions / disjunctions over a sequence
Formula all = Formula.All(new[] { p, q, r });   // p & q & r
Formula any = Formula.Any(new[] { p, q, r });    // p | q | r

Console.WriteLine(b);   // p & (q | !r) -> s   (precedence-aware printing, round-trips)
```

The AST is a sealed record hierarchy (`Variable`, `BoolConst`, `Negation`, `Conjunction`, `Disjunction`, `Implication`, `Biconditional`) with structural equality, so formulas compare and hash by structure.

Evaluate under an assignment, collect variables, or simplify:

```csharp
using DeepSigma.LogicEngine.Evaluation;

var assignment = new Dictionary<string, bool> { ["p"] = true, ["q"] = false };
bool value = Evaluator.Evaluate(Formula.Parse("p & !q"), assignment);   // True
IReadOnlySet<string> vars = Evaluator.Variables(Formula.Parse("p | (q & r)"));   // { p, q, r }
Formula simpler = Simplifier.Simplify(Formula.Parse("p & true & (q | !q)"));     // p
```

---

## Solving: SAT, validity, entailment

`Reasoner` is the high-level entry point. Every method runs through the CDCL solver by default.

```csharp
using DeepSigma.LogicEngine.Reasoning;

Reasoner.IsSatisfiable(Formula.Parse("p & !p"));        // False
Reasoner.IsUnsatisfiable(Formula.Parse("p & !p"));      // True
Reasoner.IsValid(Formula.Parse("p | !p"));              // True
Reasoner.AreEquivalent(Formula.Parse("!(p & q)"),
                       Formula.Parse("!p | !q"));        // True

// Entailment from a list of premises, or from a single combined KB formula
Reasoner.Entails(new[] { Formula.Parse("p | q"), Formula.Parse("!p") },
                 Formula.Parse("q"));                    // True (disjunctive syllogism)
```

---

## Models, enumeration, counting

```csharp
using DeepSigma.LogicEngine.Reasoning;

var f = Formula.Parse("p -> q");

Model? one = Reasoner.FindModel(f);                  // a single satisfying assignment, or null

foreach (Model model in Reasoner.EnumerateModels(f)) // every model over {p, q}
    Console.WriteLine(model);                        // { p=F, q=F }, { p=F, q=T }, { p=T, q=T }

long count = Reasoner.CountModels(f);                // 3
```

A `Model` is an immutable `IReadOnlyDictionary<string, bool>`:

```csharp
Model m = Reasoner.FindModel(Formula.Parse("p & q"))!;
bool p = m["p"];                 // true
Console.WriteLine(m);            // { p=T, q=T }
```

Enumeration uses an incremental CDCL solver under the hood (one solver instance, a blocking clause per solution, learned clauses reused across iterations).

---

## Truth tables and normal forms

```csharp
using DeepSigma.LogicEngine.Evaluation;
using DeepSigma.LogicEngine.Cnf;

Console.WriteLine(TruthTable.Format(Formula.Parse("p -> q")));
// p | q | =
// --+---+--
// F | F | T
// T | F | F
// F | T | T
// T | T | T

Formula nnf = NnfTransformer.ToNnf(Formula.Parse("!(p & q)"));     // !p | !q
Formula dnf = DnfTransformer.ToDnf(Formula.Parse("p & (q | r)"));  // p & q | p & r

CnfFormula classical = CnfTransformer.ToCnf(Formula.Parse("(p <-> q)"));  // equivalent CNF
CnfFormula tseitin   = TseitinTransformer.ToCnf(Formula.Parse("..."));    // equisatisfiable, linear size
```

Use classical CNF when you want a logically equivalent result on small formulas; use Tseitin to avoid exponential blow-up when you only need satisfiability.

---

## SAT solvers directly

All solvers implement `ISatSolver` (`SatResult Solve(CnfFormula)`) and also accept a `Formula` directly.

```csharp
using DeepSigma.LogicEngine.Solvers;
using DeepSigma.LogicEngine.Solvers.Cdcl;

var formula = Formula.Parse("(p | q) & (!p | r)");

SatResult r1 = new CdclSolver().Solve(formula);          // modern CDCL (default everywhere)
SatResult r2 = new DpllSolver().Solve(formula);          // classic DPLL
SatResult r3 = new TruthTableSolver().Solve(formula);    // brute-force oracle (<= 20 vars)

if (r1.IsSatisfiable)
    Console.WriteLine(r1.Model);   // a satisfying assignment

// CDCL exposes search statistics
var cdcl = new CdclSolver();
cdcl.Solve(formula);
Console.WriteLine(cdcl.Statistics);   // decisions=…, propagations=…, conflicts=…, restarts=…, learned=…
```

Tune CDCL via `SolverOptions` (variable/clause decay, restart unit, learned-clause limits):

```csharp
var solver = new CdclSolver(SolverOptions.Default with { RestartUnit = 50, VariableDecay = 0.9 });
```

You can swap the default engine used by `Reasoner` (for example, to use DPLL as a differential oracle in tests):

```csharp
Reasoner.UseSolver(() => new DpllSolver());
```

---

## Incremental solving and assumptions

`IncrementalCdclSolver` keeps its learned clauses and variable activity across solves. Add clauses as the problem grows, and solve repeatedly — optionally under temporary **assumptions** (literals forced true for a single call).

```csharp
using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers.Cdcl;

CnfFormula cnf = CnfTransformer.ToCnf(Formula.Parse("p | q"));
var solver = new IncrementalCdclSolver(cnf);

solver.Solve().IsSatisfiable;                                       // True

// Solve under an assumption — what if p is false?
SatResult underNotP = solver.SolveUnder(new[] { Literal.Negative("p") });
Console.WriteLine(underNotP.Model!["q"]);                           // True (q is forced)

// A failed assumption set never poisons later solves
solver.SolveUnder(new[] { Literal.Negative("p"), Literal.Negative("q") }).IsSatisfiable;  // False
solver.Solve().IsSatisfiable;                                       // still True

// Add a permanent clause and continue
solver.AddClause(new[] { Literal.Negative("p") });                 // now q must hold
```

`Literal.Positive(name)` / `Literal.Negative(name)` build literals; assumptions and added clauses may only reference variables present when the solver was constructed.

---

## Cardinality constraints

Encode "at most / at least / exactly *k* of these are true" as formulas you can combine with anything else.

```csharp
using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Reasoning;

var xs = new[] { Formula.Var("a"), Formula.Var("b"), Formula.Var("c"), Formula.Var("d") };

Formula atMostOne = Cardinality.AtMostOne(xs);
Formula exactlyTwo = Cardinality.ExactlyK(xs, 2);

Reasoner.CountModels(exactlyTwo);   // 6  (C(4,2))
```

`Cardinality` uses pairwise / binomial encodings: no auxiliary variables, so model counts stay exact — but the size is exponential in `min(k, n-k)`. For large `k`, use the linear **sequential-counter** (Sinz) encoding, which adds auxiliary variables and is meant for solving (not counting):

```csharp
CardinalityEncoding enc = SequentialCounter.AtMostK(xs, 2);
Formula constraint = enc.Constraint;                  // O(n·k) CNF
IReadOnlySet<string> aux = enc.AuxiliaryVariables;    // the introduced counter variables
```

---

## DIMACS

Read and write the standard DIMACS CNF format used by SAT competitions and other tools.

```csharp
using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers.Cdcl;

CnfFormula cnf = Dimacs.ReadString(
    "c a small instance\n" +
    "p cnf 3 2\n" +
    "1 -2 3 0\n" +
    "-1 2 0\n");

bool sat = new CdclSolver().Solve(cnf).IsSatisfiable;

string text = Dimacs.WriteToString(cnf);    // round-trips (with a variable-name comment block)
// also: Dimacs.ReadFile(path), Dimacs.WriteFile(path, cnf)
```

---

## Resolution proofs

`ResolutionRefuter` proves `KB ⊨ query` by refuting `KB ∧ ¬query`, and hands back a proof you can print. It uses the set-of-support strategy and subsumption, and classical CNF, so proofs read in the original variable names.

```csharp
using DeepSigma.LogicEngine.Reasoning;

var kb = new[] { Formula.Parse("p"), Formula.Parse("p -> q"), Formula.Parse("q -> r") };
ResolutionResult result = ResolutionRefuter.Refute(kb, Formula.Parse("r"));

Console.WriteLine(result.IsRefuted);   // True
if (result.Proof is not null)
    Console.WriteLine(result.Proof.Render());   // the derivation down to the empty clause []
```

---

## Horn clauses

For definite-Horn knowledge bases (facts + rules `p1 & … & pn -> q`), forward and backward chaining are linear-time and produce proofs.

```csharp
using DeepSigma.LogicEngine.Reasoning;

var kb = new HornClause[]
{
    new(Array.Empty<string>(), "rains"),               // fact: rains
    new(new[] { "rains" }, "wet_ground"),              // rains -> wet_ground
    new(new[] { "wet_ground" }, "slippery"),           // wet_ground -> slippery
};

IReadOnlySet<string> inferred = ForwardChainer.Infer(kb);   // { rains, wet_ground, slippery }
bool slippery = ForwardChainer.Entails(kb, "slippery");     // True

ProofNode? proof = BackwardChainer.Prove(kb, "slippery");   // goal-directed proof tree (cycle-safe)
Console.WriteLine(proof?.Render());

// Detect whether an arbitrary formula is Horn and convert it
HornConverter.TryConvert(Formula.Parse("(a -> b) & a"), out var clauses);   // true
```

---

## SMT-lite: equality + uninterpreted functions (EUF)

Reason about terms — constants, uninterpreted functions, and predicates — combined with the usual connectives. `EufSolver` runs a lazy **DPLL(T)** loop: the boolean structure goes to the CDCL solver, and a proof-producing **congruence closure** decides the equality theory.

```csharp
using DeepSigma.LogicEngine.Smt;

// Congruence is a tautology: equal inputs give equal outputs
EufSolver.IsValid(SmtFormula.Parse("a = b & b = c -> f(a) = f(c)"));   // True

// Satisfiability with boolean structure
var f = SmtFormula.Parse("(a = b | c = d) & f(a) != f(b)");
SmtResult result = EufSolver.Solve(f);
Console.WriteLine(result.IsSatisfiable);                 // True
Console.WriteLine(string.Join(", ", result.Model!.TrueAtoms));   // c = d   (a=b is ruled out)

// Predicate congruence: a = b ∧ P(a) ∧ ¬P(b) is unsatisfiable
EufSolver.IsSatisfiable(SmtFormula.Parse("a = b & P(a) & !P(b)"));   // False

// Theory entailment
var kb = new[] { SmtFormula.Parse("a = b"), SmtFormula.Parse("b = c") };
EufSolver.Entails(kb, SmtFormula.Parse("f(a) = f(c)"));   // True
```

Get a **minimal conflict core** — the smallest responsible subset — for an inconsistent set of literals:

```csharp
var literals = new[]
{
    SmtFormula.Parse("a = b"),
    SmtFormula.Parse("c = d"),       // irrelevant
    SmtFormula.Parse("b = c"),
    SmtFormula.Parse("f(a) != f(c)"),
};
IReadOnlyList<SmtFormula>? core = EufSolver.ConflictCore(literals);
// { a = b, b = c, f(a) != f(c) }  —  c = d is excluded
```

You can also build terms and formulas programmatically:

```csharp
Term a = Term.Constant("a"), b = Term.Constant("b");
Term fa = Term.Func("f", a);
SmtFormula phi = SmtFormula.Eq(a, b) & SmtFormula.Distinct(fa, Term.Func("f", b));
```

---

## Worked examples (samples)

Two runnable console projects:

```bash
# Guided walkthrough of every feature, including a resolution proof and EUF
dotnet run --project samples/DeepSigma.LogicEngine.Demo

# SAT-encoded puzzles: 8-queens, a 9x9 Sudoku, and graph coloring
dotnet run --project samples/DeepSigma.LogicEngine.Recipes -c Release
```

The recipes show how to model real problems with the cardinality helpers and solve them through `Reasoner.FindModel` (e.g. a 9×9 Sudoku solves in well under a second).

---

## Operator and syntax reference

The propositional parser (`Formula.Parse`) and the EUF parser (`SmtFormula.Parse`) share the same connective syntax and precedence (low → high): `<->`, `->` (right-associative), `|`, `&`, `!`, then atoms/parentheses.

| Meaning      | Accepted spellings                |
|--------------|-----------------------------------|
| not          | `!`  `~`  `not`                   |
| and          | `&`  `&&`  `/\`  `and`             |
| or           | `\|`  `\|\|`  `\/`  `or`           |
| implies      | `->`  `=>`                        |
| iff          | `<->`  `<=>`                      |
| true / false | `true` / `false`                  |

EUF atoms additionally use `=` and `!=` between terms, and predicates are written `P(x, y)` (or bare `P`); functions nest as `f(g(a), b)`.

---

## Performance notes

- **CDCL** is the default engine and handles thousands of variables on structured problems; it solves instances (e.g. larger pigeonhole) that plain DPLL cannot finish.
- **DPLL** is kept as a simple reference and differential-test oracle.
- **TruthTableSolver** is exhaustive and capped at 20 variables; it exists as a ground-truth oracle.
- **Model enumeration/counting** is bounded by the number of models (blocking-clause loop on an incremental solver).
- **Resolution** and the **binomial cardinality** encoding can blow up on hard inputs; resolution is bounded by a configurable clause cap, and a linear cardinality encoding is available for large `k`.

---

## Limitations

- **Propositional + EUF only.** No first-order quantifiers, no arithmetic, arrays, or bit-vectors.
- EUF is **quasi-decidable in practice** but uses a rebuild-per-check theory solver and a non-amortized `Explain` — fine for teaching and modest problems, not tuned for large industrial instances.
- The solvers are **single-threaded** and allocate managed objects; they are not a drop-in replacement for MiniSAT/Z3 on competition benchmarks.
- No incremental *theory* propagation yet (the SMT loop checks complete propositional models).

These are deliberate scope boundaries, not bugs — see the roadmap.

---

## Building and testing

```bash
dotnet build                                   # warnings-as-errors, net10.0
dotnet test                                    # 241 tests
dotnet run --project samples/DeepSigma.LogicEngine.Demo
```

The test suite includes a differential check (CDCL vs DPLL vs the truth-table oracle over hundreds of random formulas), DIMACS benchmark instances with known verdicts, and congruence-closure / EUF correctness tests against canonical facts.

---

## Roadmap

Documented future directions (some noted as hooks in the code):

- Eager **theory propagation** and partial-assignment checks in the DPLL(T) loop.
- Push/pop **incremental EUF** state instead of rebuild-per-check; near-linear `Explain`.
- Additional theories (e.g. **linear arithmetic**) behind a shared theory-solver interface.
- CDCL refinements: clause minimization, LBD-based deletion, glucose-style restarts.
- **MaxSAT / optimization** on top of the incremental solver and cardinality encoders.

---

## License

MIT © 2026 DeepSigma LLC. See [LICENSE](LICENSE).
