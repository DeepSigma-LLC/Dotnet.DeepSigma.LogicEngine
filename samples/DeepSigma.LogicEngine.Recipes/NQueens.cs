using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;

namespace DeepSigma.LogicEngine.Recipes;

/// <summary>
/// N-queens: place n queens on an n×n board so that no two attack each other.
/// Encoding: x[r,c] = "a queen is on row r, column c". Each row has exactly one
/// queen; each column has at most one; each diagonal has at most one.
/// </summary>
public static class NQueens
{
    public static void Run(int n)
    {
        Formula Cell(int r, int c) => Formula.Var($"q_{r}_{c}");

        var constraints = new List<Formula>();

        // Exactly one queen per row.
        for (var r = 0; r < n; r++)
        {
            var row = Enumerable.Range(0, n).Select(c => Cell(r, c)).ToArray();
            constraints.Add(Cardinality.ExactlyOne(row));
        }

        // At most one queen per column.
        for (var c = 0; c < n; c++)
        {
            var col = Enumerable.Range(0, n).Select(r => Cell(r, c)).ToArray();
            constraints.Add(Cardinality.AtMostOne(col));
        }

        // At most one queen per diagonal — both directions.
        for (var d = -(n - 1); d <= n - 1; d++)
        {
            var ne = new List<Formula>();
            var nw = new List<Formula>();
            for (var r = 0; r < n; r++)
            {
                var cNw = r - d;
                if (cNw >= 0 && cNw < n)
                {
                    nw.Add(Cell(r, cNw));
                }
            }
            for (var r = 0; r < n; r++)
            {
                var cNe = d - r;
                if (cNe >= 0 && cNe < n)
                {
                    ne.Add(Cell(r, cNe));
                }
            }
            if (nw.Count > 1)
            {
                constraints.Add(Cardinality.AtMostOne(nw));
            }
            if (ne.Count > 1)
            {
                constraints.Add(Cardinality.AtMostOne(ne));
            }
        }

        var problem = Formula.All(constraints);
        var model = Reasoner.FindModel(problem);

        Console.WriteLine($"  Board: {n}x{n}");
        if (model is null)
        {
            Console.WriteLine("  No solution.");
            return;
        }
        Console.WriteLine("  Solution:");
        for (var r = 0; r < n; r++)
        {
            Console.Write("    ");
            for (var c = 0; c < n; c++)
            {
                Console.Write(model.TryGetValue($"q_{r}_{c}", out var on) && on ? "Q " : ". ");
            }
            Console.WriteLine();
        }
    }
}
