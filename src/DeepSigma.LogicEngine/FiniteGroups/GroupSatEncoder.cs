using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.FiniteGroups;

/// <summary>
/// Encodes "there is a group of order n (optionally abelian/non-abelian)" as a
/// propositional formula. The product is represented by one-hot variables
/// <c>m_i_j_k</c> meaning <c>i · j = k</c>. Clause families:
/// <list type="number">
/// <item>each cell has exactly one product (a total, well-defined operation);</item>
/// <item>the identity is fixed at element 0 (a sound symmetry break — every group
/// has an identity and elements are unlabeled, so we may name it 0);</item>
/// <item>associativity, as <c>¬m_a_b_p ∨ ¬m_b_c_q ∨ ¬m_p_c_r ∨ m_a_q_r</c> over all
/// a,b,c,p,q,r (sound and complete given family 1);</item>
/// <item>every element has a left and a right inverse;</item>
/// <item>redundant Latin-square constraints (each row/column a permutation) — these
/// are theorems for groups and greatly help propagation.</item>
/// </list>
/// </summary>
internal static class GroupSatEncoder
{
    public static Formula Encode(int order, GroupSpec? spec)
    {
        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Group order must be ≥ 1.");
        }

        var n = order;
        var clauses = new List<Formula>();

        // (1) Each cell i·j has exactly one result.
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                clauses.Add(Cardinality.ExactlyOne(Targets(i, j, n)));
            }
        }

        // (2) Identity fixed at 0: 0·i = i and i·0 = i.
        for (var i = 0; i < n; i++)
        {
            clauses.Add(M(0, i, i));
            clauses.Add(M(i, 0, i));
        }

        // (3) Associativity: (a·b)·c = a·(b·c).
        for (var a = 0; a < n; a++)
        {
            for (var b = 0; b < n; b++)
            {
                for (var c = 0; c < n; c++)
                {
                    for (var p = 0; p < n; p++)
                    {
                        for (var q = 0; q < n; q++)
                        {
                            for (var r = 0; r < n; r++)
                            {
                                clauses.Add(Formula.Any(new[]
                                {
                                    Formula.Not(M(a, b, p)),
                                    Formula.Not(M(b, c, q)),
                                    Formula.Not(M(p, c, r)),
                                    M(a, q, r),
                                }));
                            }
                        }
                    }
                }
            }
        }

        // (4) Inverses with respect to the identity 0.
        for (var i = 0; i < n; i++)
        {
            clauses.Add(Formula.Any(Enumerable.Range(0, n).Select(j => M(i, j, 0))));
            clauses.Add(Formula.Any(Enumerable.Range(0, n).Select(j => M(j, i, 0))));
        }

        // (5) Latin-square redundancy: each row and column is a permutation.
        for (var k = 0; k < n; k++)
        {
            for (var i = 0; i < n; i++)
            {
                clauses.Add(Cardinality.ExactlyOne(Enumerable.Range(0, n).Select(j => M(i, j, k)).ToList()));
                clauses.Add(Cardinality.ExactlyOne(Enumerable.Range(0, n).Select(j => M(j, i, k)).ToList()));
            }
        }

        if (spec?.UseLexLeader == true)
        {
            AddLexLeaderConstraints(clauses, n);
        }

        AddSpecConstraints(clauses, n, spec);
        return Formula.All(clauses);
    }

    /// <summary>
    /// Sound partial symmetry break: for each adjacent transposition π = (k, k+1) of
    /// the non-identity element labels, require flatten(T) ≤ₗₑₓ flatten(Tᵖ). Every
    /// isomorphism class's lex-minimal table satisfies all of these, so at least one
    /// representative per class survives — counting still deduplicates by canonical
    /// form, so this only prunes relabelings (never changes the answer).
    /// </summary>
    private static void AddLexLeaderConstraints(List<Formula> clauses, int n)
    {
        for (var k = 1; k < n - 1; k++)
        {
            AddLexLessOrEqual(clauses, n, k);
        }
    }

    private static void AddLexLessOrEqual(List<Formula> clauses, int n, int k)
    {
        int Pi(int x) => x == k ? k + 1 : x == k + 1 ? k : x; // the transposition (k, k+1), an involution
        var cells = n * n;
        Formula prefixEqual = Formula.True;
        for (var c = 0; c < cells; c++)
        {
            int i = c / n, j = c % n, pi = Pi(i), pj = Pi(j);
            // Forbid a strict "greater" at the first position the two tables differ.
            clauses.Add(Formula.Implies(prefixEqual, Formula.Not(GreaterAt(n, k, i, j, pi, pj))));
            if (c < cells - 1)
            {
                var next = Formula.Var($"__lex_{k}_{c + 1}");
                clauses.Add(Formula.Iff(next, prefixEqual & EqualAt(n, k, i, j, pi, pj)));
                prefixEqual = next;
            }
        }
    }

    // value(T at (i,j)) > value(Tᵖ at (i,j)), where Tᵖ[i,j] = π(T[πi,πj]).
    private static Formula GreaterAt(int n, int k, int i, int j, int pi, int pj)
    {
        int Pi(int x) => x == k ? k + 1 : x == k + 1 ? k : x;
        var terms = new List<Formula>();
        for (var va = 0; va < n; va++)
        {
            for (var vb = 0; vb < va; vb++)
            {
                terms.Add(M(i, j, va) & M(pi, pj, Pi(vb)));
            }
        }
        return Formula.Any(terms);
    }

    // value(T at (i,j)) == value(Tᵖ at (i,j)).
    private static Formula EqualAt(int n, int k, int i, int j, int pi, int pj)
    {
        int Pi(int x) => x == k ? k + 1 : x == k + 1 ? k : x;
        var terms = new List<Formula>();
        for (var v = 0; v < n; v++)
        {
            terms.Add(M(i, j, v) & M(pi, pj, Pi(v)));
        }
        return Formula.Any(terms);
    }

    private static void AddSpecConstraints(List<Formula> clauses, int n, GroupSpec? spec)
    {
        if (spec?.Abelian is not bool abelian)
        {
            return;
        }
        if (abelian)
        {
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    for (var k = 0; k < n; k++)
                    {
                        clauses.Add(Formula.Iff(M(i, j, k), M(j, i, k)));
                    }
                }
            }
        }
        else
        {
            // ∃ i,j,k with i·j = k but j·i ≠ k (a non-commuting pair).
            var witnesses = new List<Formula>();
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    for (var k = 0; k < n; k++)
                    {
                        witnesses.Add(M(i, j, k) & Formula.Not(M(j, i, k)));
                    }
                }
            }
            clauses.Add(Formula.Any(witnesses));
        }
    }

    private static List<Formula> Targets(int i, int j, int n)
        => Enumerable.Range(0, n).Select(k => M(i, j, k)).ToList();

    internal static Formula M(int i, int j, int k) => Formula.Var(VarName(i, j, k));

    internal static string VarName(int i, int j, int k) => $"m_{i}_{j}_{k}";
}
