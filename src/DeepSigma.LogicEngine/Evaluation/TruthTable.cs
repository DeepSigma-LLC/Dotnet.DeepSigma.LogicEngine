using DeepSigma.LogicEngine.Formulas;

namespace DeepSigma.LogicEngine.Evaluation;

public readonly record struct TruthTableRow(Model Assignment, bool Value);

public static class TruthTable
{
    public const int DefaultMaxVariables = 16;

    public static IReadOnlyList<TruthTableRow> Build(Formula formula, int maxVariables = DefaultMaxVariables)
    {
        var vars = Evaluator.Variables(formula).OrderBy(v => v, StringComparer.Ordinal).ToArray();
        if (vars.Length > maxVariables)
        {
            throw new InvalidOperationException(
                $"Refusing to materialise a {1L << vars.Length}-row truth table for {vars.Length} variables (limit {maxVariables}).");
        }

        var rows = new List<TruthTableRow>(checked(1 << vars.Length));
        var assignment = new Dictionary<string, bool>(vars.Length);
        var n = 1L << vars.Length;
        for (var row = 0L; row < n; row++)
        {
            for (var i = 0; i < vars.Length; i++)
            {
                assignment[vars[i]] = (row & (1L << i)) != 0;
            }
            rows.Add(new TruthTableRow(Model.From(assignment), Evaluator.Evaluate(formula, assignment)));
        }
        return rows;
    }

    /// <summary>
    /// Format the table as a fixed-width text block with one column per variable
    /// and a final column for the formula's value.
    /// </summary>
    public static string Format(Formula formula, int maxVariables = DefaultMaxVariables)
    {
        var vars = Evaluator.Variables(formula).OrderBy(v => v, StringComparer.Ordinal).ToArray();
        var rows = Build(formula, maxVariables);
        var headers = vars.Append("=").ToArray();
        var widths = headers.Select(h => Math.Max(h.Length, 1)).ToArray();

        var sb = new System.Text.StringBuilder();
        sb.AppendJoin(" | ", headers.Select((h, i) => h.PadRight(widths[i])));
        sb.AppendLine();
        sb.AppendJoin("-+-", widths.Select(w => new string('-', w)));
        sb.AppendLine();
        foreach (var r in rows)
        {
            var cells = vars.Select((v, i) => (r.Assignment[v] ? "T" : "F").PadRight(widths[i]))
                .Append((r.Value ? "T" : "F").PadRight(widths[^1]));
            sb.AppendJoin(" | ", cells);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
