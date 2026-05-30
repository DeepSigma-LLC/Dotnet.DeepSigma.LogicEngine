using DeepSigma.LogicEngine.Recipes;

Section("1. N-Queens (8x8)");
NQueens.Run(8);

Section("2. Sudoku");
Sudoku.Run(Sudoku.EasyPuzzle);

Section("3. Graph coloring — Australia map, 3 colours");
{
    var (vertices, edges) = GraphColoring.AustraliaMap();
    GraphColoring.Run(vertices, edges, colourCount: 3, colourNames: new[] { "Red", "Green", "Blue" });
}

Section("4. Graph coloring — same map with 2 colours (should fail)");
{
    var (vertices, edges) = GraphColoring.AustraliaMap();
    GraphColoring.Run(vertices, edges, colourCount: 2);
}

return;

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(title);
    Console.WriteLine(new string('-', title.Length));
}
