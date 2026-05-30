# Dotnet.DeepSigma.LogicEngine

A .NET 10 **multi-logic reasoning engine**. It started as a propositional satisfiability / entailment library and grew into a broad reasoning stack — propositional, SMT, optimization, temporal, modal, fuzzy, probabilistic, finite-set, and finite-group — built on a shared SAT/SMT core:

- A **formula language** with a text parser, pretty-printer, evaluator, simplifier, and truth tables.
- Normal-form transformations — **NNF, CNF (classical + Tseitin), DNF**.
- Three SAT solvers: a brute-force **truth-table** oracle, classic **DPLL**, and a modern **CDCL** solver (watched literals, 1-UIP clause learning with recursive **clause minimization**, VSIDS, **LBD**-based clause deletion, Luby or adaptive **Glucose** restarts) with an **incremental** variant supporting assumptions and unsat cores.
- High-level **reasoning**: satisfiability, validity, equivalence, entailment, model finding/enumeration, model counting, and **weighted model counting / satisfaction probability**.
- A **resolution** refutation prover with set-of-support and subsumption, producing readable proofs.
- **Horn-clause** forward and backward chaining with proof trees.
- **Cardinality constraints** — pairwise/binomial (model-counting safe) and a linear **sequential-counter** encoding.
- **DIMACS** CNF read/write.
- **SMT** via a generic lazy **DPLL(T)** framework with two theories: **EUF** (equality + uninterpreted functions + predicates, proof-producing congruence closure) and **LRA** (linear real arithmetic, exact-rational simplex).
- **MaxSAT** — core-guided weighted partial optimization (find the *best* model, not just any).
- **Temporal logic** — **LTL** bounded model checking (satisfiability and counterexample traces over transition systems).
- **Modal logic** — **K / T / B / S4 / S5** validity and satisfiability via bounded Kripke-model construction.
- **Fuzzy logic** — many-valued **Gödel** and **Łukasiewicz** validity/satisfiability, reduced to linear real arithmetic over the existing LRA stack (exact, [0,1]-valued).
- **Probabilistic SAT (PSAT)** — coherence checking and exact probability **bounds** for constraints over logical formulas, with no independence assumptions; solved as an exact linear program, and scalably via **column generation** (LP duals + MaxSAT pricing).
- **Finite set logic** — set algebra (∪ ∩ complement \ Δ, ∅, U), relations (∈, ⊆, ⊂, =, disjoint), **cardinality** bounds, and named-element membership over a bounded universe, reduced to SAT (sets as membership-bit vectors).
- **Finite group theory** — a SAT **group model finder** (existence, find-a-group, enumeration, and counting groups **up to isomorphism**) over a reusable `GroupTable` algebra in DeepSigma.Mathematics.

Most capabilities follow one pattern — **encode into the SAT/SMT core, solve, decode** — so the heavy machinery (CDCL, DPLL(T), the exact simplex) is shared and the breadth is mostly thin, well-tested front-ends.

Pure managed code; its one dependency, [DeepSigma.Mathematics](https://github.com/DeepSigma-LLC/Dotnet.DeepSigma.Mathematics) (exact-rational arithmetic, a simplex for LRA, an exact LP optimizer with duals for PSAT, finite-group `GroupTable` algebra, and discrete Bayesian-network inference), is also managed. Builds warnings-as-errors and ships with **398 tests** (plus the exact-arithmetic, LP-optimizer, group-algebra, and graphical-model tests in DeepSigma.Mathematics). Correctness is anchored by **differential testing** — each engine is checked against an independent brute-force oracle.

---

## Table of contents

- [Install / getting started](#install--getting-started)
- [Project layout](#project-layout)
- [Quick tour](#quick-tour)
- [Building formulas](#building-formulas)
- [Solving: SAT, validity, entailment](#solving-sat-validity-entailment)
- [Models, enumeration, counting](#models-enumeration-counting)
- [Weighted model counting and probability](#weighted-model-counting-and-probability)
- [Truth tables and normal forms](#truth-tables-and-normal-forms)
- [SAT solvers directly](#sat-solvers-directly)
- [Incremental solving and assumptions](#incremental-solving-and-assumptions)
- [Cardinality constraints](#cardinality-constraints)
- [DIMACS](#dimacs)
- [Resolution proofs](#resolution-proofs)
- [Horn clauses](#horn-clauses)
- [SMT-lite: equality + uninterpreted functions (EUF)](#smt-lite-equality--uninterpreted-functions-euf)
- [SMT-lite: linear real arithmetic (LRA)](#smt-lite-linear-real-arithmetic-lra)
- [MaxSAT: optimization](#maxsat-optimization)
- [Temporal logic (LTL) and bounded model checking](#temporal-logic-ltl-and-bounded-model-checking)
- [Modal logic (K/T/B/S4/S5)](#modal-logic-ktbs4s5)
- [Fuzzy logic (Gödel / Łukasiewicz)](#fuzzy-logic-gödel--łukasiewicz)
- [Probabilistic SAT (PSAT)](#probabilistic-sat-psat)
- [Finite set logic](#finite-set-logic)
- [Finite group theory](#finite-group-theory)
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

The library depends on **DeepSigma.Mathematics** (managed; provides the exact-rational arithmetic and simplex used by the LRA theory), currently consumed via a cross-repo project reference.

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
  Solvers/MaxSat/  MaxSatSolver, SoftClause
  Reasoning/       Reasoner, WeightedModelCounter, ResolutionRefuter, Horn chaining
  Encoding/        Cardinality, SequentialCounter
  Transitions/     TransitionSystem, Unroller (BMC substrate)
  Smt/             Term, SmtFormula, EUF + LRA theories, EufSolver, LraSolver, parsers
  Temporal/        LtlFormula, LtlParser, BoundedModelChecker
  Modal/           ModalFormula, ModalParser, ModalSolver (K/T/B/S4/S5)
  Fuzzy/           FuzzyFormula, FuzzySolver (Gödel / Łukasiewicz over LRA)
  Probabilistic/   ProbabilityConstraint, PsatSolver (coherence + bounds; column generation)
  FiniteSets/      SetExpr/ElementExpr/SetFormula, parser/printer, FiniteSetsSolver
  FiniteGroups/    GroupSpec, GroupFinder (SAT existence/find/enumerate/count-up-to-iso)
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
using DeepSigma.LogicEngine.Smt;          // EufSolver, LraSolver, SmtFormula, Term, LraParser
using DeepSigma.LogicEngine.Solvers.MaxSat; // MaxSatSolver, SoftClause
using DeepSigma.LogicEngine.Transitions;  // TransitionSystem, Unroller
using DeepSigma.LogicEngine.Temporal;     // LtlFormula, LtlParser, BoundedModelChecker
using DeepSigma.LogicEngine.Modal;        // ModalFormula, ModalParser, ModalSolver, ModalSystem
using DeepSigma.LogicEngine.Fuzzy;        // FuzzyFormula, FuzzySolver, FuzzyLogic
using DeepSigma.LogicEngine.Probabilistic; // ProbabilityConstraint, PsatSolver
using DeepSigma.LogicEngine.FiniteSets;   // SetFormula, SetExpr, ElementExpr, FiniteSetsSolver
using DeepSigma.LogicEngine.FiniteGroups; // GroupFinder, GroupSpec
using DeepSigma.Mathematics.Algebra;      // GroupTable, GroupTables
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

## Weighted model counting and probability

`WeightedModelCounter` generalizes model counting: each variable carries a weight per polarity, and the count is the sum over satisfying assignments of the product of weights. With weights read as probabilities, this gives the probability a formula holds under independent variables.

```csharp
using DeepSigma.LogicEngine.Reasoning;

// Probability that (p ∨ q) holds when p,q are independently true with given probs.
var probs = new Dictionary<string, double> { ["p"] = 0.5, ["q"] = 0.25 };
double pr = WeightedModelCounter.SatisfactionProbability(Formula.Parse("p | q"), probs);  // 0.625

// Raw weighted count (weight = (whenTrue, whenFalse) per variable).
var weights = new Dictionary<string, (double WhenTrue, double WhenFalse)>();
double models = WeightedModelCounter.Count(Formula.Parse("p | q"), weights);              // 3.0
```

> For full **discrete Bayesian-network inference** (factors, variable elimination, marginal/conditional/MAP queries), see the `DeepSigma.Mathematics.Statistics.GraphicalModels` namespace in the companion package — general probabilistic-graphical-model machinery that LogicEngine consumes rather than reimplements.

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

## SMT-lite: linear real arithmetic (LRA)

Reason about linear constraints over rational/real variables — `<=`, `<`, `>=`, `>`, `=`, `!=`, with `+` and scalar `*`. The same DPLL(T) framework drives an **exact-rational simplex** theory solver (no floating-point, so verdicts are sound).

```csharp
using DeepSigma.LogicEngine.Smt;

// Infeasible system
LraSolver.IsSatisfiable(LraParser.Parse("x >= 1 & y >= 1 & x + y <= 1"));   // False

// Validity
LraSolver.IsValid(LraParser.Parse("x <= 5 -> x <= 6"));                     // True

// Boolean structure drives the theory: x must be 2, but neither disjunct holds
LraSolver.IsSatisfiable(LraParser.Parse("(x >= 3 | x <= 1) & x >= 2 & x <= 2"));  // False

// Strict inequalities are exact
LraSolver.IsSatisfiable(LraParser.Parse("x > 0 & x < 1"));                  // True
```

`=` parses as a conjunction of `≤`/`≥` and `!=` as a disjunction of `<`/`>`, so negation is handled by the boolean structure. (EUF and LRA are separate theories — a given solve uses one.)

---

## MaxSAT: optimization

Find the assignment that satisfies all hard clauses and **minimizes** the total weight of unsatisfied soft clauses (core-guided weighted partial MaxSAT, built on the incremental solver + cardinality encoders).

```csharp
using DeepSigma.LogicEngine.Cnf;
using DeepSigma.LogicEngine.Solvers.MaxSat;

// Hard: (a ∨ b). Soft: prefer ¬a (w1) and ¬b (w1) — best gives up exactly one.
var hard = new[] { new[] { Literal.Positive("a"), Literal.Positive("b") } };
var soft = new[]
{
    new SoftClause(new[] { Literal.Negative("a") }, 1),
    new SoftClause(new[] { Literal.Negative("b") }, 1),
};

MaxSatResult result = new MaxSatSolver(hard, soft).Solve();
Console.WriteLine(result.Cost);   // 1  (the optimum)
```

The incremental solver also exposes the underlying capability directly: `SolveUnderWithCore(assumptions)` returns the failed-assumption core on UNSAT.

---

## Temporal logic (LTL) and bounded model checking

Reason about time with Linear Temporal Logic — `X` (next), `F` (eventually), `G` (globally), `U` (until), `R` (release), `W` (weak until). `BoundedModelChecker` searches for an ultimately-periodic (lasso) trace, encoding the bounded semantics into SAT and increasing the bound.

```csharp
using DeepSigma.LogicEngine.Temporal;
using DeepSigma.LogicEngine.Transitions;

// Satisfiability: is there a trace where a holds infinitely often?
var result = BoundedModelChecker.CheckSatisfiable(LtlParser.Parse("G F a"), maxBound: 6);
Console.WriteLine(result.Found);   // True (a lasso witness)

// Model checking: find a counterexample trace of a transition system.
// Toggle: x starts false and flips each step, so "x is never true" fails.
var toggle = new TransitionSystem(
    new[] { "x" },
    Initial: new Negation(Formula.Var("x")),
    Transition: new Biconditional(Formula.Var("x'"), new Negation(Formula.Var("x"))));

LtlTrace? cx = BoundedModelChecker.FindCounterexample(toggle, LtlParser.Parse("G !x"), maxBound: 6);
// cx is non-null: a trace !x -> x violating "G !x".
```

A transition relation refers to the current state by name (`x`) and the next state by the primed name (`x'`). BMC is bounded: a witness found is real; "not found up to the bound" is not a proof of unsatisfiability.

---

## Modal logic (K/T/B/S4/S5)

Reason about necessity (`[]`) and possibility (`<>`) over Kripke frames. `ModalSolver` decides validity and satisfiability per modal system by constructing a bounded Kripke model (with the system's frame conditions) via SAT.

```csharp
using DeepSigma.LogicEngine.Modal;

// The T axiom []p -> p is valid in T (reflexive) but not in K.
ModalSolver.IsValid(ModalParser.Parse("[]p -> p"), ModalSystem.K);   // False
ModalSolver.IsValid(ModalParser.Parse("[]p -> p"), ModalSystem.T);   // True

// The 5 axiom <>p -> []<>p is valid only once the frame is euclidean (S5).
ModalSolver.IsValid(ModalParser.Parse("<>p -> []<>p"), ModalSystem.S4); // False
ModalSolver.IsValid(ModalParser.Parse("<>p -> []<>p"), ModalSystem.S5); // True

// Satisfiability: two distinct successors.
ModalSolver.IsSatisfiable(ModalParser.Parse("<>p & <>!p"), ModalSystem.K); // True
```

Systems: `K` (any frame), `T` (reflexive), `B` (reflexive+symmetric), `S4` (reflexive+transitive), `S5` (equivalence). Like BMC, the search is bounded by `maxWorlds`.

---

## Fuzzy logic (Gödel / Łukasiewicz)

Many-valued logic where truth values range over the real interval **[0, 1]** rather than {true, false}. Connectives are interpreted by a t-norm family, and `FuzzySolver` decides validity (the value is ≥ a threshold under *every* assignment) and satisfiability (≥ the threshold under *some* assignment). Each subformula's value becomes a real variable constrained by the piecewise-linear t-norm semantics, so the question reduces to **linear real arithmetic** and is solved exactly by the existing LRA stack.

```csharp
using DeepSigma.LogicEngine.Fuzzy;
using DeepSigma.Mathematics.Algebra;

var p = FuzzyFormula.Var("p");

// Excluded middle holds in Łukasiewicz (x + (1-x) ≥ 1) but not in Gödel (max(x, 1-x)).
FuzzySolver.IsValid(p | !p, FuzzyLogic.Lukasiewicz);   // True
FuzzySolver.IsValid(p | !p, FuzzyLogic.Godel);         // False

// Is there an assignment giving the conjunction value ≥ 1/2?
var conj = FuzzyFormula.Var("p") & FuzzyFormula.Var("q");
FuzzySolver.IsSatisfiable(conj, FuzzyLogic.Godel, Rational.Of(1, 2)); // True
```

Semantics: **Gödel** — AND = min, OR = max, → = (x ≤ y ? 1 : y); **Łukasiewicz** — AND = max(0, x+y−1), OR = min(1, x+y), → = min(1, 1−x+y); both use ¬x = 1−x. The product t-norm (nonlinear) is out of scope. The default threshold is 1 (a fuzzy tautology); pass any rational to ask about other cut levels.

---

## Probabilistic SAT (PSAT)

Reason about **probabilities of logical formulas** without independence or structural assumptions. Given constraints like *P(p) = 1/2* and *P(p → q) = 1*, `PsatSolver` decides whether they are jointly **coherent** (some probability distribution over truth assignments satisfies them all) and computes the tightest **bounds** on a query. A distribution assigns mass to each possible world; the constraints become a linear program over those masses, solved exactly by the rational LP optimizer — so verdicts and bounds are exact fractions.

```csharp
using DeepSigma.LogicEngine.Probabilistic;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.Mathematics.Algebra;

// Probabilistic modus ponens: P(p) = 1/2, P(p -> q) = 1  ⇒  P(q) ∈ [1/2, 1].
var kb = new[]
{
    ProbabilityConstraint.Exactly(Formula.Parse("p"), Rational.Of(1, 2)),
    ProbabilityConstraint.Exactly(Formula.Parse("p -> q"), Rational.Of(1, 1)),
};
PsatSolver.IsConsistent(kb);                 // True
var (low, high) = PsatSolver.Bounds(kb, Formula.Parse("q")).Value;  // 1/2 .. 1

// Incoherent: complementary events must sum to 1.
PsatSolver.IsConsistent(new[]
{
    ProbabilityConstraint.Exactly(Formula.Parse("p"), Rational.Of(3, 10)),
    ProbabilityConstraint.Exactly(Formula.Parse("!p"), Rational.Of(1, 2)),
});                                           // False
```

`Bounds` recovers classic results exactly — Fréchet inequalities for conjunction/disjunction, probabilistic modus ponens — and returns `null` when the constraints are incoherent. The possible-world LP has exponentially many columns; `IsConsistentScalable` and `BoundsScalable` avoid enumerating them via **column generation**: they solve a small restricted LP and use its exact dual prices to price the next world to add (the pricing step is a MaxSAT call), matching the enumeration verdict and bounds for equality constraints. PSAT is NP-hard but decidable; the exact LP optimizer (with duals) that powers it lives in DeepSigma.Mathematics for reuse.

---

## Finite set logic

Reason about finite sets over a bounded universe: set algebra (`∪ ∩ \ Δ`, complement, `∅`, `U`), the relations `∈ ⊆ ⊂ =` and `disjoint`, **cardinality** bounds `|S| ⋈ k`, and named-element membership — all Boolean-combined. Each set is encoded as a vector of membership bits over the universe slots, so every question reduces to SAT.

```csharp
using DeepSigma.LogicEngine.FiniteSets;

// Set identities are valid (the default universe — 2^#setvars — is a complete
// decision for the cardinality-free fragment).
FiniteSetsSolver.IsValid(SetFormula.Parse("~(A ∪ B) = ~A ∩ ~B"));            // True (De Morgan)
FiniteSetsSolver.IsValid(SetFormula.Parse("A <= B & B <= A -> A = B"));      // True (antisymmetry)

// Cardinality reasoning: A ⊆ B forces |A| ≤ |B|.
FiniteSetsSolver.IsSatisfiable(SetFormula.Parse("A subset B & |A| = 3 & |B| = 2"), universe: 4); // False

// A concrete witness, decoded back to sets and element slots.
var model = FiniteSetsSolver.FindModel(SetFormula.Parse("x in A & A subset B & |B| = 2"), universe: 3);
```

Cardinality questions are decided **relative to the universe size** (like bounded model checking) — pass an explicit `universe` to control it. The builder API (`SetExpr`/`ElementExpr` factories and operators) mirrors the parser.

---

## Finite group theory

Find finite groups by SAT: the group axioms are encoded over a one-hot Cayley table (closure, an identity fixed at element 0, associativity, inverses, plus redundant Latin-square constraints), solved by the propositional engine, and decoded into a `GroupTable`. The reusable `GroupTable` algebra — `IsGroup`, `IsAbelian`, `ElementOrder`, `IsCyclic`, and isomorphism via canonical form — lives in DeepSigma.Mathematics.

```csharp
using DeepSigma.LogicEngine.FiniteGroups;
using DeepSigma.Mathematics.Algebra;

// How many groups of each order, up to isomorphism? (1, 1, 1, 2, 1, 2, 1, 5, …)
GroupFinder.CountGroupsUpToIsomorphism(4);   // 2  (ℤ₄ and the Klein four-group)
GroupFinder.CountGroupsUpToIsomorphism(6);   // 2  (ℤ₆ and S₃)

// The smallest non-abelian group is S₃ (order 6).
var g = GroupFinder.FindGroup(6, new GroupSpec { Abelian = false });
bool isS3 = g!.Value.IsIsomorphicTo(GroupTables.SymmetricGroup(3));   // True
```

Cost scales with the O(n⁶) associativity encoding: existence/find are practical to about order 10; counting up to isomorphism enumerates the labeled groups then deduplicates by canonical form, practical to about order 8.

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

The other parsers share these connectives and add their own atoms/operators:

| Parser | Adds |
|--------|------|
| `SmtFormula.Parse` (EUF) | `=` / `!=` between terms; predicates `P(x, y)` or bare `P`; nested functions `f(g(a), b)` |
| `LraParser.Parse` (LRA) | linear constraints `2*x + 3*y <= 5`, relations `<= < >= > = !=`, numeric literals |
| `LtlParser.Parse` (LTL) | `X` (next), `F` (eventually), `G` (globally), `U`, `R`, `W` |
| `ModalParser.Parse` (modal) | `[]` (box / necessity), `<>` (diamond / possibility) |

---

## Performance notes

- **CDCL** is the default engine and handles thousands of variables on structured problems; it solves instances (e.g. larger pigeonhole) that plain DPLL cannot finish.
- **DPLL** is kept as a simple reference and differential-test oracle.
- **TruthTableSolver** is exhaustive and capped at 20 variables; it exists as a ground-truth oracle.
- **Model enumeration/counting** is bounded by the number of models (blocking-clause loop on an incremental solver).
- **Resolution** and the **binomial cardinality** encoding can blow up on hard inputs; resolution is bounded by a configurable clause cap, and a linear cardinality encoding is available for large `k`.
- **Temporal (BMC)** and **modal** search are bounded: a witness/countermodel found is real, but "not found up to the bound" is not a general proof of unsatisfiability (it relies on the finite-model property holding within the bound).
- **Fuzzy** reasoning is exact but limited to the (piecewise-linear) Gödel and Łukasiewicz t-norms; the product t-norm is nonlinear and out of scope.
- **PSAT** `IsConsistent`/`Bounds` enumerate possible worlds (exact, exponential in the atom count); `IsConsistentScalable` uses column generation for equality constraints.

---

## Limitations

- **Logics covered:** propositional, SMT (EUF, LRA), MaxSAT, LTL, modal K/T/B/S4/S5, fuzzy (Gödel/Łukasiewicz), probabilistic (PSAT), finite-set, and finite-group. No first-order quantifiers; no integer arithmetic (LIA), arrays, bit-vectors, or theory combination (an SMT solve uses one theory); no CTL/QBF/ASP.
- **Finite-set** cardinality reasoning and **finite-group** model finding are bounded/finite: set cardinality is decided relative to the universe size, and group search scales with an O(n⁶) associativity encoding (existence to ~order 10, isomorphism counting to ~order 8).
- **Bounded methods** (LTL BMC, modal) are complete only up to their search bound.
- EUF/LRA theory solvers are **rebuild-per-check** with no incremental push/pop or eager theory propagation — fine for teaching and modest problems, not tuned for large industrial instances.
- The solvers are **single-threaded** and allocate managed objects; they are not a drop-in replacement for MiniSAT/Z3 on competition benchmarks.

These are deliberate scope boundaries, not bugs — see the roadmap.

---

## Building and testing

```bash
dotnet build                                   # warnings-as-errors, net10.0
dotnet test                                    # 398 tests
dotnet run --project samples/DeepSigma.LogicEngine.Demo
```

Correctness rests on **differential testing**: each engine is checked against an independent brute-force oracle — CDCL/DPLL vs the truth-table solver, MaxSAT vs brute-force optimum, weighted counting vs enumeration, the LTL encoder vs a lasso-trace simulator, the modal encoder vs a Kripke-model enumerator, fuzzy validity vs a [0,1]-grid evaluator, PSAT column generation vs exact possible-world enumeration, the finite-set encoder vs brute-force interpretation enumeration, and the group finder vs brute-force Cayley-table enumeration, plus DIMACS benchmarks with known verdicts and the EUF/LRA conflict-core tests against canonical facts.

---

## Roadmap

Documented future directions (some noted as hooks in the code):

- **CTL** model checking; **integer arithmetic (LIA)** via branch-and-bound on the exact simplex.
- **Theory combination** (Nelson–Oppen, e.g. EUF + LRA together); eager theory propagation; push/pop incremental theory state.
- Scalable model counting via **d-DNNF** knowledge compilation.

---

## License

MIT © 2026 DeepSigma LLC. See [LICENSE](LICENSE).
