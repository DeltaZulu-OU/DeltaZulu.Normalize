using System.Text.Json.Nodes;

namespace DeltaZulu.Parse;

/// <summary>
/// The KQL (Kusto Query Language) scalar type a field's value corresponds
/// to, so parsed output can be consumed by KQL-typed tooling (locally via
/// Tx.Kql, centrally via a transpiler) without each consumer re-inferring
/// types from raw JSON. This has no upstream liblognorm/json-c equivalent —
/// see docs/COMPARISON.md.
/// </summary>
public enum KqlType : byte
{
    /// <summary>No KQL type has been assigned (the default; also every
    /// field's value on a total non-match, e.g. "originalmsg" carries
    /// <see cref="String"/>, but a field that predates this feature's
    /// wiring reports this).</summary>
    Unknown = 0,
    Bool,
    DateTime,
    Decimal,
    Dynamic,
    Guid,
    Int,
    Long,
    Real,
    String,
    Timespan,
}

internal static class KqlTypeInference
{
    /// <summary>
    /// Infer a KQL type from a JSON value's actual runtime shape, for the
    /// cases where a field's type isn't fixed by which motif parser
    /// produced it (an object splice fanning an embedded JsonObject's own
    /// members out to the parent, or a ".." unwrap of a raw JsonObject
    /// member) — the value's shape at that point is whatever the
    /// upstream structured/custom-type content happened to contain, not a
    /// single motif's static output type.
    /// </summary>
    public static KqlType InferFromNode(JsonNode? node) => node switch
    {
        null => KqlType.Unknown,
        JsonObject or JsonArray => KqlType.Dynamic,
        JsonValue v when v.TryGetValue(out long _) => KqlType.Long,
        JsonValue v when v.TryGetValue(out double _) => KqlType.Real,
        JsonValue v when v.TryGetValue(out bool _) => KqlType.Bool,
        JsonValue => KqlType.String,
        _ => KqlType.Dynamic,
    };
}
