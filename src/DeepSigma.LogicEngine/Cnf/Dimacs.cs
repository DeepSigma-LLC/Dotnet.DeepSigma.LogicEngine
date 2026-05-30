using System.Globalization;

namespace DeepSigma.LogicEngine.Cnf;

/// <summary>
/// Reader and writer for the DIMACS CNF text format. Variables in DIMACS are
/// 1-indexed positive integers; a negative integer denotes the negated literal;
/// each clause is terminated by 0. Lines beginning with <c>c</c> are comments
/// and the file begins with a problem line <c>p cnf &lt;vars&gt; &lt;clauses&gt;</c>.
/// </summary>
public static class Dimacs
{
    /// <summary>Variable name used for DIMACS variable <c>i</c> when reading: <c>x{i}</c>.</summary>
    public static string DefaultName(int dimacsId) => "x" + dimacsId.ToString(CultureInfo.InvariantCulture);

    public static CnfFormula ReadString(string text, Func<int, string>? nameFor = null)
    {
        using var reader = new StringReader(text);
        return Read(reader, nameFor);
    }

    public static CnfFormula ReadFile(string path, Func<int, string>? nameFor = null)
    {
        using var reader = new StreamReader(path);
        return Read(reader, nameFor);
    }

    public static CnfFormula Read(TextReader reader, Func<int, string>? nameFor = null)
    {
        nameFor ??= DefaultName;
        var clauses = new List<Clause>();
        var currentLiterals = new List<Literal>();
        var seenHeader = false;
        var lineNumber = 0;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.AsSpan().TrimStart();
            if (trimmed.IsEmpty)
            {
                continue;
            }
            var first = trimmed[0];
            if (first == 'c')
            {
                continue;
            }
            if (first == '%')
            {
                // Legacy SAT-Competition end-of-instance marker.
                break;
            }
            if (first == 'p')
            {
                if (seenHeader)
                {
                    throw new FormatException($"Duplicate 'p' header at line {lineNumber}.");
                }
                ValidateHeader(line, lineNumber);
                seenHeader = true;
                continue;
            }

            foreach (var token in Tokenize(line))
            {
                if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    throw new FormatException($"Non-integer token '{token}' at line {lineNumber}.");
                }
                if (value == 0)
                {
                    clauses.Add(new Clause(currentLiterals));
                    currentLiterals.Clear();
                }
                else
                {
                    var id = Math.Abs(value);
                    currentLiterals.Add(new Literal(nameFor(id), value < 0));
                }
            }
        }

        if (currentLiterals.Count != 0)
        {
            throw new FormatException("Trailing clause without terminating 0.");
        }

        return new CnfFormula(clauses);
    }

    public static string WriteToString(CnfFormula cnf, IEnumerable<string>? header = null)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        Write(writer, cnf, header);
        return writer.ToString();
    }

    public static void WriteFile(string path, CnfFormula cnf, IEnumerable<string>? header = null)
    {
        using var writer = new StreamWriter(path);
        Write(writer, cnf, header);
    }

    /// <summary>
    /// Write <paramref name="cnf"/> in DIMACS form. Variable names are mapped to
    /// stable positive integer ids in the order they first appear; the mapping
    /// is written as a comment block for human readers.
    /// </summary>
    public static void Write(TextWriter writer, CnfFormula cnf, IEnumerable<string>? header = null)
    {
        if (header is not null)
        {
            foreach (var line in header)
            {
                writer.Write("c ");
                writer.WriteLine(line);
            }
        }

        var nameToId = AssignIds(cnf);
        writer.Write("c variable map:");
        writer.WriteLine();
        foreach (var (name, id) in nameToId.OrderBy(kv => kv.Value))
        {
            writer.Write("c   ");
            writer.Write(id.ToString(CultureInfo.InvariantCulture));
            writer.Write(' ');
            writer.WriteLine(name);
        }

        writer.Write("p cnf ");
        writer.Write(nameToId.Count.ToString(CultureInfo.InvariantCulture));
        writer.Write(' ');
        writer.WriteLine(cnf.Clauses.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var clause in cnf.Clauses)
        {
            var first = true;
            foreach (var lit in clause.Literals)
            {
                if (!first)
                {
                    writer.Write(' ');
                }
                first = false;
                var id = nameToId[lit.Variable];
                writer.Write((lit.Negated ? -id : id).ToString(CultureInfo.InvariantCulture));
            }
            if (!first)
            {
                writer.Write(' ');
            }
            writer.WriteLine('0');
        }
    }

    private static Dictionary<string, int> AssignIds(CnfFormula cnf)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        var next = 1;
        foreach (var clause in cnf.Clauses)
        {
            foreach (var lit in clause.Literals)
            {
                if (!map.ContainsKey(lit.Variable))
                {
                    map[lit.Variable] = next++;
                }
            }
        }
        return map;
    }

    private static void ValidateHeader(string line, int lineNumber)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || parts[0] != "p" || parts[1] != "cnf")
        {
            throw new FormatException($"Malformed problem line at line {lineNumber}: '{line}'.");
        }
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            throw new FormatException($"Header counts are not integers at line {lineNumber}: '{line}'.");
        }
    }

    private static IEnumerable<string> Tokenize(string line)
    {
        var start = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                if (start >= 0)
                {
                    yield return line.Substring(start, i - start);
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }
        if (start >= 0)
        {
            yield return line.Substring(start);
        }
    }

}
