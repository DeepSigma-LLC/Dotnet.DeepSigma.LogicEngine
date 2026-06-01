# Architecture

This document explains how `DeepSigma.LogicEngine` is put together internally: the
core idea that unifies the breadth, the layer map, the shared front-end template
(parser/printer/AST/facade) every logic follows, the facade conventions and *why*
they differ, and a step-by-step guide for adding a new logic.

For *what* the library can do and runnable usage examples, see [README.md](README.md).
This file is for contributors who want to understand or extend the codebase.

---

## The one idea: encode → solve → decode

The engine supports a dozen logics — propositional, SMT (EUF/LRA/LIA/arrays/combined),
first-order, LTL, CTL, modal, fuzzy, PSAT, finite-set, finite-group — but it is **not** a
dozen independent solvers. Almost every front-end follows the same three-step pattern:

1. **Encode** the problem into a lower-level core the engine already solves well —
   usually propositional CNF (for SAT), an `SmtFormula` over a theory (for DPLL(T)),
   or a linear program / clause set.
2. **Solve** with the shared heavy machinery: the **CDCL** SAT solver, the lazy
   **DPLL(T)** theory loop, the exact-rational **simplex**/**LP optimizer** (in
   `DeepSigma.Mathematics`), or the **resolution** saturation loop.
3. **Decode** the core's answer (a SAT model, a theory model, an LP solution, a
   refutation) back into the front-end's vocabulary (a set, a group table, a Kripke
   trace, a probability bound).

The payoff: the hard, performance-critical, easy-to-get-wrong code (CDCL with watched
literals and clause learning, the exact simplex, congruence closure) is written **once**
and shared. Each new logic is a comparatively thin, well-tested front-end. When you read
a solver facade, look for these three steps — they are almost always there.

Examples of the pattern in practice:

| Front-end        | Encodes into                                        | Decodes back to              |
|------------------|-----------------------------------------------------|------------------------------|
| Finite sets      | membership-bit vectors → SAT                         | sets + element slots         |
| Finite groups    | one-hot Cayley table + axioms → SAT                  | a `GroupTable`               |
| Fuzzy            | piecewise-linear t-norm constraints → LRA            | a [0,1] valuation            |
| PSAT             | possible-world masses → exact LP                     | probability bounds           |
| LTL (BMC)        | bounded lasso semantics → SAT                        | an ultimately-periodic trace |
| Modal            | bounded Kripke frame + frame conditions → SAT        | a countermodel               |
| SMT theories     | boolean skeleton → CDCL; atoms → theory solver       | a theory model / conflict    |

CTL is the main exception: it is a direct explicit-state fixpoint computation over a
finite Kripke structure, not an encoding into SAT. First-order logic is the other: it
runs its own resolution saturation rather than reducing to the propositional core.

---

## Layer map

From the bottom up:

```
┌───────────────────────────────────────────────────────────────────────────┐
│ DeepSigma.Mathematics  (separate repo / package)                            │
│   exact Rational + DeltaRational · simplex (LRA) · LP optimizer w/ duals    │
│   (PSAT) · GroupTable algebra · discrete Bayesian-network inference         │
└───────────────────────────────────────────────────────────────────────────┘
                                   ▲ consumed by
┌───────────────────────────────────────────────────────────────────────────┐
│ Core solving substrate                                                      │
│   Cnf/       Literal, Clause, CnfFormula, NNF/CNF/DNF/Tseitin, DIMACS        │
│   Solvers/   ISatSolver, TruthTableSolver, DpllSolver, SatResult            │
│   Solvers/Cdcl/   CDCL internals + IncrementalCdclSolver (assumptions/cores) │
│   Solvers/MaxSat/ core-guided weighted partial MaxSAT                        │
└───────────────────────────────────────────────────────────────────────────┘
                                   ▲
┌───────────────────────────────────────────────────────────────────────────┐
│ Theory layer (SMT)                                                          │
│   Smt/   ITheory + lazy DPLL(T) loop; EUF (congruence closure), LRA          │
│          (simplex adapter), LIA (branch-and-bound), arrays (read-over-write  │
│          → EUF), CombinedTheory (Nelson–Oppen EUF+LRA)                        │
└───────────────────────────────────────────────────────────────────────────┘
                                   ▲
┌───────────────────────────────────────────────────────────────────────────┐
│ Reasoning & front-ends                                                      │
│   Reasoning/  Reasoner, WeightedModelCounter, ResolutionRefuter, Horn        │
│   FirstOrder/ resolution + paramodulation prover                             │
│   Temporal/ Ctl/ Modal/ Fuzzy/ Probabilistic/ FiniteSets/ FiniteGroups/      │
│   Transitions/  TransitionSystem + Unroller (BMC substrate)                  │
└───────────────────────────────────────────────────────────────────────────┘
```

Foundational, shared by everyone:

- **`Formulas/`** — the propositional `Formula` AST (sealed records: `Variable`,
  `BoolConst`, `Negation`, `Conjunction`, `Disjunction`, `Implication`,
  `Biconditional`), `Model`, operator overloads, and factories (`All`/`Any`).
- **`Parsing/` + `Printing/`** — the propositional parser/printer, plus the shared
  front-end **infrastructure** described below.
- **`Common/`** — small cross-cutting helpers (`BalancedFold`, `StructuralEquality`).

**Convention: folder = namespace.** Everything under `src/DeepSigma.LogicEngine/Smt/`
is in `DeepSigma.LogicEngine.Smt`, and so on. Keep new files in the folder matching
their namespace.

---

## The front-end template

Every logic front-end is built from the same four parts. Most live together in one
folder named for the logic.

1. **AST** — a sealed record hierarchy for the logic's formulas (and terms/expressions
   if it has them). Sealed records give you structural equality, hashing, and exhaustive
   `switch` for free. Example: `Modal/ModalFormula.cs`, `Ctl/CtlFormula.cs`.
2. **Parser** — a recursive-descent parser from text to AST. Static entry `Parse(string)`
   (and usually `TryParse`). See the shared parsing infrastructure below.
3. **Printer** — a precedence-aware (or fully-parenthesized) pretty-printer back to text,
   wired into the AST's `ToString()`. See the shared printing infrastructure below.
4. **Solver facade** — a static class exposing the logic's high-level questions
   (`IsValid`, `IsSatisfiable`, `FindModel`, `Entails`, …). This is where
   **encode → solve → decode** happens.

### Shared parsing infrastructure (`Parsing/Infrastructure/`)

The boolean-connective grammar (`<-> -> | & !`, with the same precedence and
associativity everywhere) was byte-for-byte duplicated across eight parsers. It is now
captured by two `internal` helpers; each parser keeps its own tokenizer, `Token`/`Kind`
enum, and all atom/term grammar.

- **`TokenReader<TToken, TKind>`** — a generic token cursor. A parser's inner `State`
  class derives from it, passing `(tokens, kindOf, textOf)` selectors, and inherits
  `Peek(offset)`, `PeekKind(offset)`, `Advance()`, `Check(kind)`, `Accept(kind)`,
  `Expect(kind, label)`, plus `Position`/`Reset(int)` for backtracking. `PeekKind`
  clamps past the end to the `End` sentinel, so lookahead loops terminate naturally.
- **`ConnectiveChain`** — the exact control flow for connective levels:
  - `LeftAssoc(operand, matchAndConsume, make)` — the `while` loop used for `iff`/`or`/`and`.
  - `RightAssoc(operand, matchAndConsume, self, make)` — the `if`+self-recursion used for
    right-associative `implies`.

  `matchAndConsume` is a thunk like `() => Accept(Kind.Or)` that both parser styles reduce
  to. The **operand hook** is just the `Func<TF>` you pass in — it differs per logic
  (`ParseNot`, `ParseUnary`, `ParsePrimary`, LTL's `ParseTemporalBinary`) and the helper
  never enters it, so per-logic quirks (FiniteSets `|A|` vs boolean-or and its
  backtracking; LRA `(`-disambiguation and linear grammar; FOL quantifier scope; CTL
  bracket forms `E[φ U ψ]`) stay local and untouched.
- **`CharScanner`** — stateless character-level helpers for the hand-written tokenizers
  (`Peek(s, i, offset)`, `Matches(s, i, literal)`, `IsIdentifierStart`/`IsIdentifierPart`,
  and `ReadWhile(s, start, cont)`). `TokenReader`/`ConnectiveChain` operate *after*
  tokenization; `CharScanner` removes the duplicated lookahead, identifier/number scanning,
  and literal matching from the `Tokenize(string)` methods. Each lexer keeps its own
  `i++ / continue` loop, its `Token`/`Kind` type, its keyword map, and its multi-character
  operator dispatch (which has per-parser ordering, e.g. LRA matches `<=` before `<->`), so
  accepted syntax and error positions are unchanged.

Two parser styles coexist: `Peek()`-method parsers (`Parser`, `Modal`, `Smt`, `Ltl`,
`Lra`) derive from `TokenReader`; `Peek`-property parsers (`Fol`, `Ctl`) keep their own
cursor plumbing and adopt **only** `ConnectiveChain`. Both are fine — pick whichever fits
the parser you are touching, and don't force-migrate one style to the other.

### Shared printing infrastructure (`Printing/Infrastructure/`)

Two `internal` helpers; each printer keeps its own per-node dispatch and its own
"is this operand atomic?" predicate.

- **`PrecedencePrinter`** (minimal parentheses) — `WriteWithParens(sb, prec, outerPrec, body)`
  and `WriteBinary(sb, outerPrec, prec, left, op, right, rightAssoc, write)`. Used by the
  two precedence-aware printers: `Printing/Printer.cs` (propositional) and
  `Smt/SmtPrinter.cs`. `SmtPrinter` keeps a bespoke `WriteNot` (it wraps an equality
  operand in extra parens for readability) — a genuine, intentional difference.
- **`ParenPrinter`** (fully parenthesized) — `WriteBinary(sb, left, op, right, write)`
  emits `(l op r)`, and `WriteOperand(sb, operand, isAtomic, write)` parenthesizes a
  non-atomic operand. Used by the five fully-parenthesized printers: `Modal`, `Ltl`,
  `Ctl`, `Fol`, `FiniteSets`. Each supplies its `Write` delegate and an `IsAtomic`
  predicate; bespoke nodes (FOL quantifier prefixes/terms, FiniteSets `∈`/cardinality
  and set-expression printing) stay local.

### Other shared helpers (`Common/`)

- **`BalancedFold.Combine<T>(items, combine)`** — folds a list into a **balanced** binary
  tree (depth O(log n)) instead of a left-nested chain (depth O(n)). Used by
  `Formula.All/Any` and `SmtFormula.All/Any` so that large conjunctions built by
  `ArraySolver`/`CombinedSolver`/`SequentialCounter` don't risk stack overflow during
  later recursive traversal. **Prefer this over a hand-rolled `Aggregate`** when building
  a connective from a sequence.
- **`StructuralEquality.ListEquals` / `.Hash`** — structural equality and hashing for
  `(symbol, args)`-shaped AST nodes. Used by `Term`, `PredicateAtom`, `FolFunc`,
  `FolPredicate` instead of hand-rolled loops.

In the SMT layer, **`Smt/TheoryAtoms`** holds the shared conversions from abstracted
`SmtFormula` atoms to the representations the theory solvers consume: `ToEufLiteral` (used
by `EufTheory` and the EUF half of `CombinedTheory`) and `LinearTerms`/`Polarized` (used by
`LraTheory`, `LiaTheory`, and the LRA half of `CombinedTheory`). Each theory keeps its own
"wrong kind of atom" guard text and its own solver-specific add call. Relatedly, all four
theories now report **1-minimal conflict cores**: `LiaTheory`/`CombinedTheory` already wrapped
their raw check in `Smt/ConflictMinimizer`, and `EufTheory`/`LraTheory` now do the same, so
the DPLL(T) loop learns equally strong blocking clauses across theories.

---

## Facade conventions — and why they diverge

Front-end facades **deliberately do not share a single return type**. The shape of each
facade follows the *decidability and semantics* of its logic. This divergence is
principled; do not "unify" it.

| Family | Return shape | Why |
|--------|--------------|-----|
| **Decidable** (propositional, EUF, LRA, finite-set, modal-within-bound, CTL) | `bool` for `IsValid`/`IsSatisfiable`; `Model?`/state-set for witnesses | The question has a definite yes/no answer the engine can always produce. |
| **Semi-decidable** (first-order) | a status enum: `Proved` / `Saturated` / `Unknown` | FO validity is only semi-decidable; "not proved within budget" must be honestly distinguished from "disproved". |
| **Model-checking** (CTL, LTL) | sets of states / a counterexample trace | The natural answer is *which states satisfy φ* or *a violating run*, not a single bit. |
| **Bounded** (LTL BMC, modal, finite-set cardinality, LIA) | results carry an explicit `bound` / `maxWorlds` / `universe` parameter | Completeness holds only up to the search bound; the bound is part of the contract, surfaced in the signature. |
| **Optimization** (MaxSAT, PSAT) | a cost / a `(low, high)` bound pair | The answer is a number or an interval, not satisfiability. |

The common nouns are shared (`Model`, `SatResult`, `Formula`), but the verbs return what
each logic can honestly promise. When you add a facade, match the family it belongs to
rather than forcing a uniform signature.

The same honesty appears in the caveats baked into names and docs: BMC/modal/finite-set/LIA
are bounded; FOL is semi-decidable; arrays are non-extensional; theory combination is
EUF+LRA only. Keep those caveats visible — they are features, not omissions.

**Discovery aids (additive, not unifying).** Two consumer-facing conveniences smooth the
above without collapsing the principled divergence:
- Every formula type exposes both `Parse` and `TryParse` (the `TryParse` delegates to the
  parser's `try { Parse } catch (FormatException)`), so `XxxFormula.Parse`/`TryParse` is a
  uniform construction entry. LRA still parses via `LraParser.Parse` because it and EUF
  produce the *same* `SmtFormula` from different surface syntaxes.
- `Smt/SmtSolver.cs` is a single front door for the quantifier-free SMT theories: an
  `SmtTheory` enum (`Euf`/`Lra`/`Combined`/`Arrays`) selects which dedicated facade
  `SmtSolver.{IsSatisfiable,IsValid,Entails,Solve}(f, theory)` dispatches to. It adds no new
  semantics (the `Arrays` case reuses `ArraySolver.WithArrayAxioms` → EUF). The dedicated
  facades stay public for richer outputs (`ConflictCore`); LIA stays on `LiaSolver` because
  its integer-variable set and search bound don't fit the uniform signature.

---

## How to add a new logic

Say you want to add logic *Foo*. The mechanical steps:

1. **Create a folder** `src/DeepSigma.LogicEngine/Foo/` → namespace
   `DeepSigma.LogicEngine.Foo`.
2. **AST** — `FooFormula.cs`: a sealed `abstract record FooFormula` with a sealed record
   per node. Add factories/operator overloads if ergonomic. Reuse
   `Common.BalancedFold` for any `All`/`Any` helpers and `Common.StructuralEquality` for
   `(symbol, args)` nodes.
3. **Parser** — `FooParser.cs`: a static `Parse(string)`/`TryParse`. Write a small
   tokenizer producing your own `Token`/`Kind`, using `CharScanner` for the lookahead,
   identifier/number scanning, and literal matching. For the boolean connectives, derive an
   inner `State` from `TokenReader<Token, Kind>` and build the connective levels with
   `ConnectiveChain.LeftAssoc`/`RightAssoc`. Put *Foo*-specific grammar in the operand
   hook you pass in.
4. **Printer** — `FooPrinter.cs`: dispatch per node; delegate binary/operand emission to
   `ParenPrinter` (fully parenthesized) or `PrecedencePrinter` (minimal parens) with an
   `IsAtomic` predicate. Wire it into `FooFormula.ToString()`.
5. **Solver facade** — `FooSolver.cs`: implement **encode → solve → decode**. Reuse the
   core (CDCL via `Reasoner`/`ISatSolver`, DPLL(T) via the `Smt` theory loop, or the
   exact LP/simplex in `DeepSigma.Mathematics`) rather than writing new search.
6. **Tests** — in `tests/DeepSigma.LogicEngine.Tests/`:
   - **Known-theorem tests**: a handful of textbook validities/satisfiabilities with
     hand-checked answers.
   - **An independent brute-force oracle**: a *separately written* reference
     implementation (e.g. enumerate all interpretations within a small bound) that the
     facade is differentially tested against on random inputs. **Do not share code
     between the facade and its oracle** — the independence *is* the correctness
     guarantee.
   - **Round-trip tests**: `Parse(Print(ast)) == ast` and `Parse` of canonical strings.

### Conventions to respect

- **Folder = namespace.** Keep new types in the matching folder.
- **`internal` by default** for infrastructure/helpers; the assembly has
  `InternalsVisibleTo("DeepSigma.LogicEngine.Tests")`, so tests can still reach them.
  Make `public` only the surface a consumer needs (the AST, the parser entry, the facade).
- **Reuse the substrate.** New heavy search/arithmetic almost always belongs in the core
  or in `DeepSigma.Mathematics`, consumed here — not reimplemented per logic.
- **Warnings are errors.** The build fails on warnings (including unused members), so
  remove dead code as you go.

---

## Optional Z3 backend

`src/DeepSigma.LogicEngine.Z3/` is an **optional, parallel** engine that reuses the existing
front-end (parsers + public ASTs) and solves with [Z3](https://github.com/Z3Prover/z3) instead of
the native solvers. It is a *whole-formula translation* — `Formula`/`SmtFormula` → Z3 expressions,
solve, translate the model back — not an `ITheory` plugged into the native DPLL(T) loop.

- **Two engines, one front-end.** Native = pure-managed, exact, bounded; Z3 = complete (e.g.
  unbounded integers), fast at scale, but a **native dependency** (`Microsoft.Z3` bundles `libz3`).
  The core project takes no dependency on Z3; consumers opt in by referencing the Z3 project.
- **Parallel facades, not a hidden swap.** `Z3Reasoner` (propositional), `Z3SmtSolver` (EUF/LRA/LIA/
  arrays/combined; `Z3SmtTheory` adds unbounded `Lia`), and `Z3MaxSatSolver` mirror the native APIs and
  return a tri-valued `Z3Result` (`Satisfiable`/`Unsatisfiable`/**`Unknown`** — Z3 is honest about
  not deciding quantified/nonlinear queries, mirroring `FolProofStatus`).
- **Encoder logics ride the `ISatSolver` seam.** Passing a `Z3SatSolver` to the (additive)
  `ISatSolver` overloads of `ModalSolver`/`BoundedModelChecker`/`FiniteSetsSolver`/`GroupFinder`
  runs those logics on Z3 with no internals exposed; fuzzy routes through the public `FuzzyEncoder`.
- **New theories live in a `Sorted/` layer** (`Sort` + `SortedExpr`, translated by `SortedToZ3`,
  solved by `Z3SortedSolver`) — typed expressions for things the native engine cannot represent:
  **bit-vectors (QF_BV)**, unbounded **int/real** arithmetic (quantifiers + nonlinear), and
  **strings**. This layer carries its own text front-end (`Z3SortedParser`/`Z3SortedPrinter`,
  surfaced as `SortedExpr.Parse`/`ToString`) — a self-contained tokenizer/parser rather than the
  core's shared `CharScanner`/`TokenReader`/`PrecedencePrinter` infrastructure, because that
  infrastructure is `internal` to the core assembly and the Z3 project deliberately has no
  `InternalsVisibleTo`. Being multi-sorted, its syntax opens with a variable-declaration prefix
  (`bv8 x;`, `int n;`, …) that the single-sorted core front-ends never need.
- **Deliberately native-only:** CTL (explicit-state fixpoint, not an SMT query), model
  counting/PSAT (Z3 isn't a #SAT counter), and the FOL resolution prover (Z3 quantifiers are
  incomplete for validity and emit no resolution proofs).
- **Differential oracle.** `tests/DeepSigma.LogicEngine.Z3.Tests/` cross-checks the two engines on
  the shared fragment — any disagreement is a real bug in one of them. The native test project
  stays `libz3`-free.

See the README's "Projects and packages" section for the consumer-facing capability matrix.

## Testing philosophy

Correctness rests on **differential testing**: each engine is checked against an
independent, brute-force oracle (truth-table for SAT, lasso simulator for LTL, Kripke
enumerator for modal, integer-box enumerator for LIA, finite function-model enumerator for
arrays, finite-model oracle for FOL, possible-world enumeration for PSAT, and so on). The
oracles are intentionally simple and independent of the optimized code paths they check.

Run the suite (Debug runs the CDCL invariant assertions):

```bash
dotnet test tests/DeepSigma.LogicEngine.Tests
```

The companion `DeepSigma.Mathematics` repo has its own suite for the exact arithmetic,
simplex, LP optimizer, and group algebra this library depends on.

---

## Where to look first

- New to the codebase? Read `Formulas/Formula.cs`, then `Parsing/Parser.cs` +
  `Printing/Printer.cs` (the simplest complete front-end), then `Solvers/Cdcl/` for the
  core engine.
- Adding a theory? Read `Smt/ITheory.cs` (or the DPLL(T) loop) and an existing theory like
  `Smt/LraTheory.cs`.
- Adding a logic? Copy the shape of a small front-end — `Modal/` or `Ctl/` are good
  templates — and follow the steps above.
