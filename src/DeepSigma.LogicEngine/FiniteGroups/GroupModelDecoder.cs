using DeepSigma.LogicEngine.Formulas;
using DeepSigma.Mathematics.Algebra;

namespace DeepSigma.LogicEngine.FiniteGroups;

/// <summary>Turns a SAT model of the group encoding into a <see cref="GroupTable"/>.</summary>
internal static class GroupModelDecoder
{
    public static GroupTable Decode(Model model, int order)
    {
        var n = order;
        var flat = new int[n * n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                flat[i * n + j] = ProductOf(model, n, i, j);
            }
        }

        var table = GroupTable.FromRows(n, flat);
        if (!table.IsGroup)
        {
            // The encoding guarantees a group; a failure here is an encoder bug.
            throw new InvalidOperationException("Decoded table is not a group — the SAT encoding is unsound.");
        }
        return table;
    }

    private static int ProductOf(Model model, int n, int i, int j)
    {
        for (var k = 0; k < n; k++)
        {
            if (model.TryGetValue(GroupSatEncoder.VarName(i, j, k), out var value) && value)
            {
                return k;
            }
        }
        throw new InvalidOperationException($"No product assigned for {i}·{j} — the one-hot constraint was violated.");
    }
}
