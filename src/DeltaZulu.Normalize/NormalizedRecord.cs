using System.Collections;
using DeltaZulu.Parse;

namespace DeltaZulu.Normalize;

/// <summary>
/// A parsed message projected onto KQL-typed columns (see
/// <see cref="NormalizedField"/>), in the same order <see cref="ParseResult"/>
/// committed them. Built by <see cref="RecordNormalizer.Normalize"/>.
/// </summary>
public sealed class NormalizedRecord : IReadOnlyList<NormalizedField>
{
    private readonly NormalizedField[] _fields;

    internal NormalizedRecord(NormalizedField[] fields, bool matched)
    {
        _fields = fields;
        Matched = matched;
    }

    public int Count => _fields.Length;

    /// <summary>Whether the source message matched a rule (mirrors <see cref="ParseResult.Matched"/>).
    /// On a non-match, fields hold "originalmsg"/"unparsed-data", exactly like <see cref="ParseResult"/>.</summary>
    public bool Matched { get; }

    public NormalizedField this[int index] => _fields[index];

    public IEnumerator<NormalizedField> GetEnumerator() => ((IEnumerable<NormalizedField>)_fields).GetEnumerator();

    /// <summary>Look up a field by name (ordinal linear scan — realistic records have few fields;
    /// same tradeoff <c>FieldCollector</c> makes internally).</summary>
    public bool TryGetField(string name, out NormalizedField field)
    {
        foreach (var f in _fields)
        {
            if (f.Name == name)
            {
                field = f;
                return true;
            }
        }

        field = default;
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
