using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;

namespace DeepSigma.LogicEngine.Recipes;

/// <summary>
/// Sudoku solver: every cell holds exactly one digit; every row, column, and
/// 3×3 box contains each digit exactly once; the given digits are unit clauses.
/// </summary>
public static class Sudoku
{
    /// <summary>An "easy" 9×9 puzzle. Zeroes are blanks.</summary>
    public static readonly int[,] EasyPuzzle =
    {
        { 5, 3, 0, 0, 7, 0, 0, 0, 0 },
        { 6, 0, 0, 1, 9, 5, 0, 0, 0 },
        { 0, 9, 8, 0, 0, 0, 0, 6, 0 },
        { 8, 0, 0, 0, 6, 0, 0, 0, 3 },
        { 4, 0, 0, 8, 0, 3, 0, 0, 1 },
        { 7, 0, 0, 0, 2, 0, 0, 0, 6 },
        { 0, 6, 0, 0, 0, 0, 2, 8, 0 },
        { 0, 0, 0, 4, 1, 9, 0, 0, 5 },
        { 0, 0, 0, 0, 8, 0, 0, 7, 9 },
    };

    public static void Run(int[,] puzzle)
    {
        Formula Cell(int r, int c, int d) => Formula.Var($"x_{r}_{c}_{d}");

        var constraints = new List<Formula>();

        // Each cell holds exactly one digit.
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                var digits = Enumerable.Range(1, 9).Select(d => Cell(r, c, d)).ToArray();
                constraints.Add(Cardinality.ExactlyOne(digits));
            }
        }

        // Each digit appears exactly once per row.
        for (var r = 0; r < 9; r++)
        {
            for (var d = 1; d <= 9; d++)
            {
                var cells = Enumerable.Range(0, 9).Select(c => Cell(r, c, d)).ToArray();
                constraints.Add(Cardinality.ExactlyOne(cells));
            }
        }

        // Each digit appears exactly once per column.
        for (var c = 0; c < 9; c++)
        {
            for (var d = 1; d <= 9; d++)
            {
                var cells = Enumerable.Range(0, 9).Select(r => Cell(r, c, d)).ToArray();
                constraints.Add(Cardinality.ExactlyOne(cells));
            }
        }

        // Each digit appears exactly once per 3×3 box.
        for (var br = 0; br < 3; br++)
        {
            for (var bc = 0; bc < 3; bc++)
            {
                for (var d = 1; d <= 9; d++)
                {
                    var cells = new List<Formula>();
                    for (var dr = 0; dr < 3; dr++)
                    {
                        for (var dc = 0; dc < 3; dc++)
                        {
                            cells.Add(Cell(br * 3 + dr, bc * 3 + dc, d));
                        }
                    }
                    constraints.Add(Cardinality.ExactlyOne(cells));
                }
            }
        }

        // Givens.
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                if (puzzle[r, c] != 0)
                {
                    constraints.Add(Cell(r, c, puzzle[r, c]));
                }
            }
        }

        var problem = Formula.All(constraints);

        Console.WriteLine("  Puzzle:");
        PrintGrid(puzzle, indent: "    ");

        var started = Environment.TickCount;
        var model = Reasoner.FindModel(problem);
        var elapsed = Environment.TickCount - started;

        if (model is null)
        {
            Console.WriteLine($"  No solution. ({elapsed} ms)");
            return;
        }

        var solved = new int[9, 9];
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                for (var d = 1; d <= 9; d++)
                {
                    if (model.TryGetValue($"x_{r}_{c}_{d}", out var on) && on)
                    {
                        solved[r, c] = d;
                        break;
                    }
                }
            }
        }

        Console.WriteLine($"  Solution ({elapsed} ms):");
        PrintGrid(solved, indent: "    ");
    }

    private static void PrintGrid(int[,] grid, string indent)
    {
        for (var r = 0; r < 9; r++)
        {
            if (r % 3 == 0 && r > 0)
            {
                Console.WriteLine(indent + "------+-------+------");
            }
            Console.Write(indent);
            for (var c = 0; c < 9; c++)
            {
                if (c % 3 == 0 && c > 0)
                {
                    Console.Write("| ");
                }
                Console.Write(grid[r, c] == 0 ? ". " : grid[r, c] + " ");
            }
            Console.WriteLine();
        }
    }
}
