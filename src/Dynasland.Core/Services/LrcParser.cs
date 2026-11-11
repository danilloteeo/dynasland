using System.Globalization;
using System.Text.RegularExpressions;
using Dynasland.Core.Interfaces;

namespace Dynasland.Core.Services;

/// <summary>Lê letras no formato LRC: <c>[01:23.45] verso</c>, uma ou mais marcas de tempo por linha.</summary>
public static partial class LrcParser
{
    public static IReadOnlyList<LyricLine> Parse(string? lrc)
    {
        var lines = new List<LyricLine>();
        if (string.IsNullOrWhiteSpace(lrc))
        {
            return lines;
        }

        foreach (var raw in lrc.Split('\n'))
        {
            var matches = TimeTag().Matches(raw);
            if (matches.Count == 0)
            {
                continue;
            }

            // O texto vem depois da última marca; marcas de tempo por palavra (<00:12.30>) são descartadas.
            var last = matches[^1];
            var text = WordTag().Replace(raw[(last.Index + last.Length)..], string.Empty).Trim();
            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
                var seconds = double.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
                lines.Add(new LyricLine(TimeSpan.FromSeconds(minutes * 60 + seconds), text));
            }
        }

        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines;
    }

    /// <summary>Índice do verso em andamento na posição dada; -1 antes do primeiro.</summary>
    public static int IndexAt(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        var low = 0;
        var high = lines.Count - 1;
        var found = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (lines[middle].Time <= position)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    [GeneratedRegex(@"\[(\d{1,3}):(\d{1,2}(?:\.\d{1,3})?)\]")]
    private static partial Regex TimeTag();

    [GeneratedRegex(@"<\d{1,3}:\d{1,2}(?:\.\d{1,3})?>")]
    private static partial Regex WordTag();
}
