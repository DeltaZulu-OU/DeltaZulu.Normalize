using System.Text.Json.Nodes;
using DeltaZulu.Parse;

namespace DeltaZulu.Normalize;

/// <summary>
/// Projects a <see cref="ParseResult"/> onto a <see cref="NormalizedRecord"/> —
/// the concrete first slice of the semantic view layer
/// docs/adr/0001-naming.md reserved the word "normalize" for. See
/// docs/adr/0002-kql-common-type-denominator.md (Phase 3) for the design
/// rationale.
/// </summary>
public static class RecordNormalizer
{
    /// <summary>Build a <see cref="NormalizedRecord"/> from a parse result. Every
    /// field's <see cref="NormalizedField.Value"/> is the native CLR type its
    /// <see cref="NormalizedField.Type"/> promises — see <see cref="NormalizedField"/>.</summary>
    public static NormalizedRecord Normalize(ParseResult result)
    {
        var fields = new NormalizedField[result.Count];
        for (var i = 0; i < result.Count; ++i)
        {
            var name = result.GetName(i);
            result.TryGetKqlType(name, out var type);
            fields[i] = new NormalizedField(name, type, NativeValue(result, name, type));
        }

        return new NormalizedRecord(fields, result.Matched);
    }

    /// <summary>
    /// Materialize one field's value as the CLR type <paramref name="type"/>
    /// promises. String fields prefer <see cref="ParseResult.TryGetRawText"/>
    /// (a zero-copy slice of the input message) over materializing a
    /// <see cref="JsonNode"/> first; every other scalar type round-trips
    /// through <see cref="JsonNode.GetValue{T}"/> with no parsing involved,
    /// since the underlying value was already constructed as that exact CLR
    /// type by the engine (see <c>DeltaZulu.Parse.KqlTypeTable</c> and the
    /// Phase 2 native-emission motifs in docs/adr/0002-*.md). Dynamic (and
    /// the defensive Unknown fallback) convert the JSON value into a plain
    /// object graph instead of exposing <see cref="JsonNode"/> directly.
    /// </summary>
    private static object? NativeValue(ParseResult result, string name, KqlType type)
    {
        if (type == KqlType.String && result.TryGetRawText(name, out var text))
        {
            return text.ToString();
        }

        var node = result.GetValue(name);
        return type switch {
            KqlType.String => node?.GetValue<string>(),
            KqlType.Long => node?.GetValue<long>(),
            KqlType.Int => node?.GetValue<int>(),
            KqlType.Real => node?.GetValue<double>(),
            KqlType.Decimal => node?.GetValue<decimal>(),
            KqlType.Bool => node?.GetValue<bool>(),
            KqlType.DateTime => node?.GetValue<DateTimeOffset>(),
            KqlType.Timespan => node?.GetValue<TimeSpan>(),
            KqlType.Guid => node?.GetValue<Guid>(),
            /* Dynamic, and Unknown as a defensive fallback (shouldn't occur
             * for a field the engine itself committed) */
            _ => ToPlainObject(node),
        };
    }

    /// <summary>
    /// Convert a <see cref="JsonNode"/> into a plain CLR object graph
    /// (<see cref="Dictionary{TKey, TValue}"/>/<see cref="List{T}"/>/
    /// primitives) that a generic MessagePack resolver — or any other
    /// serializer — can walk with no <c>System.Text.Json</c> dependency and
    /// no custom formatter. Scalar precedence mirrors (but does not share
    /// code with — that logic is internal to DeltaZulu.Parse)
    /// <c>KqlTypeInference.InferFromNode</c>.
    /// </summary>
    private static object? ToPlainObject(JsonNode? node) => node switch {
        null => null,
        JsonObject obj => obj.ToDictionary(static kv => kv.Key, static kv => ToPlainObject(kv.Value)),
        JsonArray arr => arr.Select(ToPlainObject).ToList(),
        JsonValue v when v.TryGetValue(out long l) => l,
        JsonValue v when v.TryGetValue(out double d) => d,
        JsonValue v when v.TryGetValue(out bool b) => b,
        JsonValue v when v.TryGetValue(out string? s) => s,
        _ => node.ToJsonString(),
    };
}
