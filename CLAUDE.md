# CLAUDE.md

Orientation for AI agents (and new contributors) working in this repo.

## What this is

`DeepSigma.LogicEngine` — a .NET 10 multi-logic reasoning engine (propositional SAT/CDCL,
SMT, first-order, LTL/CTL, modal, fuzzy, PSAT, finite-set, finite-group) built on a shared
SAT/SMT core. Its one dependency, `DeepSigma.Mathematics`, is a sibling repo consumed via a
cross-repo project reference (exact-rational arithmetic, simplex, LP optimizer, group
algebra).

`src/DeepSigma.LogicEngine.Z3/` is an **optional, opt-in** parallel engine that solves the same
ASTs with Z3 (the only project with a native dependency — `Microsoft.Z3`/`libz3`). The core stays
pure-managed. See ARCHITECTURE.md "Optional Z3 backend" and the README capability matrix. Keep its
`libz3` dependency out of the core and the native test project.

## Read these first

- **[ARCHITECTURE.md](ARCHITECTURE.md)** — the encode → solve → decode convention, the
  layer map, the shared front-end template (parser/printer/AST/facade + the
  `TokenReader`/`ConnectiveChain`/`ParenPrinter`/`PrecedencePrinter` infrastructure), the
  facade conventions and why they diverge, and a step-by-step "how to add a new logic"
  guide. **Start here before changing structure.**
- **[README.md](README.md)** — the user-facing feature catalog with runnable examples.

## Build & test

```bash
dotnet build                                   # warnings-as-errors, net10.0
dotnet test tests/DeepSigma.LogicEngine.Tests  # full suite (Debug runs CDCL invariant asserts)
dotnet run --project samples/DeepSigma.LogicEngine.Demo     # guided feature walkthrough
dotnet run --project samples/DeepSigma.LogicEngine.Recipes -c Release  # N-queens, Sudoku, coloring
```

Changes that touch arithmetic/simplex/LP/group algebra may also require running the
`DeepSigma.Mathematics` suite in its own repo.

## Conventions

- **Folder = namespace.** A file under `Smt/` is in `DeepSigma.LogicEngine.Smt`. Keep new
  files in the folder matching their namespace.
- **`internal` by default** for helpers/infrastructure; only the consumer-facing surface
  (AST, parser entry, facade) is `public`. The assembly has
  `InternalsVisibleTo("DeepSigma.LogicEngine.Tests")`, so tests reach internals.
- **Warnings are errors.** The build fails on any warning, including unused members —
  remove dead code as you introduce it.
- **Reuse the substrate.** New heavy search/arithmetic belongs in the core or in
  `DeepSigma.Mathematics`, consumed here — not reimplemented per logic. Use
  `Common.BalancedFold` for `All`/`Any` over a sequence and `Common.StructuralEquality`
  for `(symbol, args)` AST nodes.
- **Differential testing is the correctness guarantee.** Each engine is checked against an
  *independently written* brute-force oracle. **Never share code between a facade and its
  oracle** — the independence is the whole point.
- **Behavior preservation.** Do not change accepted syntax, precedence, associativity,
  printed output, or public signatures without an explicit request.

## Git

**Do not commit.** The maintainer reviews and commits changes themselves. Leave the working
tree modified; do not run `git commit`, `git push`, or branch operations unless explicitly
asked.
