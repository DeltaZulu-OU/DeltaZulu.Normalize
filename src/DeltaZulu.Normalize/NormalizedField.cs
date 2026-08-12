using DeltaZulu.Parse;

namespace DeltaZulu.Normalize;

/// <summary>
/// One column of a <see cref="NormalizedRecord"/>: a field name, its KQL
/// scalar type, and a value whose CLR type already matches that type — a
/// <see cref="KqlType.Long"/> field's <see cref="Value"/> is a real
/// <see cref="long"/>, a <see cref="KqlType.DateTime"/> field's is a real
/// <see cref="DateTimeOffset"/>, and so on (see
/// docs/adr/0002-kql-common-type-denominator.md's "same data type
/// contracts" discussion) — so a consumer (Tx.Kql, a MessagePack envelope)
/// needs no cast, parse, or conversion step of its own.
/// <see cref="KqlType.Dynamic"/> fields carry a plain object graph (nested
/// <see cref="Dictionary{TKey, TValue}"/>/<see cref="List{T}"/>/primitives),
/// not a <c>JsonNode</c>, for the same reason: a generic MessagePack
/// resolver serializes that graph natively, with no custom formatter.
/// </summary>
/// <param name="Name">Field name, as committed by the parser (see <see cref="ParseResult.GetName"/>).</param>
/// <param name="Type">The field's KQL scalar type (see <see cref="ParseResult.TryGetKqlType"/>).</param>
/// <param name="Value">
/// The field's value as the native CLR type <paramref name="Type"/> promises,
/// or <see langword="null"/> for a committed JSON null.
/// </param>
public readonly record struct NormalizedField(string Name, KqlType Type, object? Value);
