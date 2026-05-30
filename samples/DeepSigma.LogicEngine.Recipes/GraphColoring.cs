using DeepSigma.LogicEngine.Encoding;
using DeepSigma.LogicEngine.Formulas;
using DeepSigma.LogicEngine.Reasoning;

namespace DeepSigma.LogicEngine.Recipes;

/// <summary>
/// k-colour a graph so that no two adjacent vertices share a colour. Encoding:
/// <c>c[v,k] = true</c> means vertex v has colour k. ExactlyOne colour per
/// vertex; for each edge (u, v) and colour k, not both c[u,k] and c[v,k].
/// </summary>
public static class GraphColoring
{
    /// <summary>Russell &amp; Norvig's Australia-map example: WA, NT, SA, Q, NSW, V, T.</summary>
    public static (IReadOnlyList<string> Vertices, IReadOnlyList<(string, string)> Edges) AustraliaMap()
    {
        var v = new[] { "WA", "NT", "SA", "Q", "NSW", "V", "T" };
        var e = new[]
        {
            ("WA", "NT"), ("WA", "SA"),
            ("NT", "SA"), ("NT", "Q"),
            ("SA", "Q"), ("SA", "NSW"), ("SA", "V"),
            ("Q", "NSW"),
            ("NSW", "V"),
        };
        return (v, e);
    }

    public static void Run(
        IReadOnlyList<string> vertices,
        IReadOnlyList<(string, string)> edges,
        int colourCount,
        IReadOnlyList<string>? colourNames = null)
    {
        colourNames ??= Enumerable.Range(0, colourCount).Select(i => "C" + i).ToArray();
        Formula Has(string v, int k) => Formula.Var($"col_{v}_{k}");

        var constraints = new List<Formula>();

        // Exactly one colour per vertex.
        foreach (var v in vertices)
        {
            var perVertex = Enumerable.Range(0, colourCount).Select(k => Has(v, k)).ToArray();
            constraints.Add(Cardinality.ExactlyOne(perVertex));
        }

        // Adjacent vertices differ in colour.
        foreach (var (u, v) in edges)
        {
            for (var k = 0; k < colourCount; k++)
            {
                constraints.Add(new Negation(Has(u, k)) | new Negation(Has(v, k)));
            }
        }

        var problem = Formula.All(constraints);
        var model = Reasoner.FindModel(problem);

        Console.WriteLine($"  Vertices: {vertices.Count}, edges: {edges.Count}, colours: {colourCount}");
        if (model is null)
        {
            Console.WriteLine($"  Not {colourCount}-colourable.");
            return;
        }

        Console.WriteLine("  Colouring:");
        foreach (var v in vertices)
        {
            for (var k = 0; k < colourCount; k++)
            {
                if (model.TryGetValue($"col_{v}_{k}", out var on) && on)
                {
                    Console.WriteLine($"    {v,-4} = {colourNames[k]}");
                    break;
                }
            }
        }
    }
}
