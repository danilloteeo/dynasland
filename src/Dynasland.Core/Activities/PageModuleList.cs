using System.Collections;

namespace Dynasland.Core.Activities;

/// <summary>
/// Módulos das abas do estado interativo, na ordem das abas. Compara por valor, para que
/// <see cref="IslandSnapshot"/> continue igual quando nada mudou (e o coordenador não avise à toa).
/// </summary>
public sealed class PageModuleList : IReadOnlyList<string>, IEquatable<PageModuleList>
{
    private readonly string[] _items;

    public PageModuleList(IEnumerable<string> moduleIds) => _items = [.. moduleIds];

    public static PageModuleList Empty { get; } = new([]);

    public int Count => _items.Length;

    public string this[int index] => _items[index];

    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(PageModuleList? other) =>
        other is not null && _items.AsSpan().SequenceEqual(other._items);

    public override bool Equals(object? obj) => Equals(obj as PageModuleList);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items)
        {
            hash.Add(item, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
