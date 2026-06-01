using System.Text;
using DeepSigma.LogicEngine.Cnf;

namespace DeepSigma.LogicEngine.Reasoning;

/// <summary>A single step in a resolution proof: a clause together with the parents it was derived from and the pivot variable resolved on.</summary>
public sealed record ResolutionStep(
    int Index,
    Clause Resolvent,
    int? LeftParent,
    int? RightParent,
    string? Pivot);

/// <summary>
/// A resolution refutation proof. <see cref="Steps"/> begins with the input
/// clauses (no parents) followed by derivations that culminate in the empty
/// clause.
/// </summary>
public sealed class ResolutionProof
{
    /// <summary>The proof steps: input clauses followed by their derivations.</summary>
    public IReadOnlyList<ResolutionStep> Steps { get; }
    /// <summary>The index in <see cref="Steps"/> of the derived empty clause.</summary>
    public int EmptyClauseIndex { get; }

    /// <summary>Creates a resolution proof from its steps and the index of the empty clause.</summary>
    public ResolutionProof(IReadOnlyList<ResolutionStep> steps, int emptyClauseIndex)
    {
        Steps = steps;
        EmptyClauseIndex = emptyClauseIndex;
    }

    /// <summary>Renders the proof as text, showing only the steps reachable from the empty clause.</summary>
    public string Render()
    {
        var relevant = new SortedSet<int>();
        var stack = new Stack<int>();
        stack.Push(EmptyClauseIndex);
        while (stack.Count > 0)
        {
            var i = stack.Pop();
            if (!relevant.Add(i))
            {
                continue;
            }
            var step = Steps[i];
            if (step.LeftParent is int lp)
            {
                stack.Push(lp);
            }
            if (step.RightParent is int rp)
            {
                stack.Push(rp);
            }
        }

        var sb = new StringBuilder();
        foreach (var i in relevant)
        {
            var step = Steps[i];
            sb.Append('[').Append(step.Index).Append("] ").Append(step.Resolvent);
            if (step.LeftParent is null)
            {
                sb.Append("  (premise)");
            }
            else
            {
                sb.Append("  (resolve [").Append(step.LeftParent).Append("] and [").Append(step.RightParent)
                    .Append("] on ").Append(step.Pivot).Append(')');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

/// <summary>Outcome of a resolution attempt: whether the clause set was refuted, the proof if so, and an optional explanatory reason.</summary>
public sealed record ResolutionResult(bool IsRefuted, ResolutionProof? Proof, string? Reason = null);
