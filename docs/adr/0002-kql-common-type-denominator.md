# ADR-2: KQL as the common output-type denominator

## Status

Accepted. Phase 1 (field-level type metadata), Phase 2 (native scalar
emission for the temporal motifs), and a first slice of Phase 3 (the
`DeltaZulu.Normalize` typed-row projection) are implemented; see
Consequences for what's deliberately not yet in scope. Phase 3's framing
of schema mapping as open Phase-3 work is superseded by ADR-4
(`docs/adr/0004-normalizedrecord-column-names-are-final.md`); this ADR's
Decision/Consequences text below is left as originally written, per this
project's immutable-ADR convention.

## Context

`DeltaZulu.Parse` normalizes log messages into JSON-typed output (`string`,
`long`, `double`, `object`, `array`, `null` via `System.Text.Json`), a
faithful mirror of upstream liblognorm's own json-c type system — see
`docs/COMPARISON.md`. That was the right target for a v2-engine port, but
it isn't the org's actual requirement: KQL (Kusto Query Language)'s scalar
type set is meant to be the common denominator for parsed output, so
results can be queried locally through `Tx.Kql` and centrally through a
transpiler without each consumer re-inferring types from raw JSON. Before
this ADR, nothing in the pipeline knew or exposed "this field is a KQL
`long`" versus "this field is a KQL `string`" — that information didn't
exist past "which parser matched."

ADR-1 already anticipated a need like this: it reserves the word
"normalize" (and, implicitly, the currently-empty `src/DeltaZulu.Normalize/`
project) for a future semantic view layer, referred to there as "ADR-5",
that sits on top of parsed output and maps it onto a common schema. This
ADR is not that layer — it's the numbering gap should be read as: the
work below is a foundation `DeltaZulu.Parse` needs to expose regardless of
when the semantic view layer itself gets built, not a renumbering of it.
Whoever writes ADR-5 should treat the `KqlType` metadata described here as
an input it consumes, not something it needs to invent.

A downstream requirement sharpened the design while Phase 2 was underway:
the parsed values feed `Tx.Kql` for filtering/enriching/projecting, and
from there a MessagePack envelope, using "the same data type contracts" —
i.e. the goal isn't just a `KqlType` *label* on a field, it's a value the
`KqlType` label is actually true of, so neither consumer has to cast,
parse, or convert. A `KqlType.Long` field backed by a real `long`, a
`KqlType.DateTime` field backed by a real `DateTimeOffset`, and so on —
not a `KqlType.DateTime` label on a value that's still secretly a Unix
epoch number requiring a `todatetime()`-style conversion downstream. This
governs every value-shape choice below and should govern Phase 3's as
well.

## Decision

Split the work into three phases so the cheap, safe, in-scope-for-this-
library part isn't blocked on decisions this codebase has no visibility
into.

**Phase 1 (implemented by this ADR): tag every extracted field with a KQL
scalar type, as pure metadata.** `KqlType` (`src/DeltaZulu.Parse/KqlType.cs`)
is the 10 canonical Kusto scalar types plus an `Unknown` default. The tag
travels on `FieldValue` itself (not a side table), computed once per rule
edge at PDAG-compile time in `PdagCompiler.ClassifyKqlType` — a sibling of
the existing `ClassifyExtract` method, from the same `(PrsId, Data)`
inputs — and exposed via `ParseResult.TryGetKqlType(name, out KqlType)`.
Putting the tag on `FieldValue` means the existing "." splice and ".."
unwrap logic in `PdagWalker.CommitField` propagates it for free: a
user-defined type's instantiation defaults to `Dynamic`, but when its
pattern collapses to a single scalar via the ".." unwrap, the parent field
reports that inner value's own type instead, with no special-casing
needed. The two places a field's type isn't fixed by which motif matched
(a "." splice fanning a structured motif's embedded object out to the
parent, and the ".." unwrap of a raw `JsonObject` member) infer the type
from the actual JSON value's runtime shape instead
(`KqlTypeInference.InferFromNode`). This phase changes no JSON output —
`Parse(out JsonObject)`, `ToJsonString()`, `WriteTo()`, and the CLI are
untouched — it is purely additive metadata riding along the existing
pipeline. The compiled-PDAG binary persistence format
(`CompiledPdagBinary.cs`) version was bumped (1 → 2) to persist the new
per-edge tag; a v1 binary cache must be recompiled.

**Phase 2 (implemented): native scalar emission for the still-string-only
temporal motifs.** `date-iso`, `time-24hr`, `time-12hr`, `duration`, and
`kernel-timestamp` were semantically `DateTime`/`Timespan` but had no
`format=`-style opt-out, so Phase 1 conservatively tagged them `String`.
Each gained a `Construct` function accepting `format="string"` (default,
unchanged behavior) or a native keyword — `"datetime"` for `date-iso`,
`"timespan"` for the other four — that produces a genuine
`DateTimeOffset`/`TimeSpan` CLR value (UTC midnight of the parsed date;
the parsed hour/minute/second as an elapsed duration), per the "same data
type contracts" principle above: not an epoch/total-seconds `long`
encoding of one. `System.Text.Json.Nodes.JsonValue.Create<T>` already
supports both types with a zero-copy `GetValue<T>()` round trip and
serializes them as ISO-8601-ish text (`"2024-01-15T00:00:00+00:00"`,
`"01:30:00"`), so JSON/CLI output stays sensible with no bespoke
serialization code. `date-rfc3164`/`date-rfc5424`'s existing
`timestamp-unix[-ms]` modes are deliberately **not** changed to match —
that's pre-existing, already-shipped engine behavior unrelated to this
work, and changing it is a separate, larger decision than adding new
opt-in capability to five previously-string-only motifs; the resulting
inconsistency (those two report `KqlType.Long` for an epoch encoding,
`date-iso` reports `KqlType.DateTime` for a native value) is a known,
flagged gap, not an oversight. `PdagCompiler.ClassifyExtract`/
`ClassifyKqlType` and `CompiledPdagBinary` (version bumped 2 → 3, since
these five motifs now always carry non-null `Data`) were updated to
match.

**Phase 3 (first slice implemented): the `DeltaZulu.Normalize` typed-row
projection.** Scaffolded the `DeltaZulu.Normalize` project ADR-1 reserved,
as its own assembly referencing only `DeltaZulu.Parse` (not yet packaged
for NuGet). `RecordNormalizer.Normalize(ParseResult)` walks every
committed field (`ParseResult.Count`/`GetName`/`TryGetKqlType`/`GetValue`/
`TryGetRawText` — the library's existing public surface; no new internals
access needed) and produces a `NormalizedRecord`: an ordered
`IReadOnlyList<NormalizedField>` where each `NormalizedField(Name, Type,
Value)`'s `Value` is already the native CLR type `Type` promises (`long`,
`double`, `bool`, `string`, `DateTimeOffset`, `TimeSpan`, `Guid`,
`decimal`, `int` — the `KqlType.Guid`/`Decimal`/`Int` branches exist for
completeness though no motif emits them yet), materialized via
`JsonNode.GetValue<T>()` (a zero-copy read-back, not a parse, since the
underlying value was already constructed as that exact type — see Phase
1/2 above) with a `TryGetRawText`-first fast path for `String` fields.
`KqlType.Dynamic` (and the defensive `Unknown` fallback) fields are
converted into a plain `Dictionary<string, object?>`/`List<object?>`/
primitive object graph rather than exposed as a `JsonNode`, so a generic
MessagePack resolver — or any other serializer — can walk them with no
`System.Text.Json` dependency and no custom formatter, per this ADR's
"same data type contracts" requirement.

This is a first slice, not the finished layer: it answers "what's an
in-process typed row" but not the two questions that need the actual
`Tx.Kql`/MessagePack-envelope integration in hand to answer correctly —
a rule/tag → target-table/column-name schema mapping (right now a
`NormalizedRecord`'s columns are exactly the parsed field names, with no
renaming/routing), and whatever `Tx.Kql`-specific interface (beyond a
plain `IReadOnlyList<NormalizedField>`) its filtering/enriching/
projecting actually wants to consume.

## Consequences

- New public API surface: `KqlType` enum, `ParseResult.TryGetKqlType`, and
  five new rulebase `format=` values (`date-iso:format=datetime`,
  `duration`/`time-24hr`/`time-12hr`/`kernel-timestamp:format=timespan`).
  Purely additive — every motif's default (no `format=`) behavior is
  byte-for-byte unchanged, so this doesn't affect this port's liblognorm
  parity claims.
- `docs/COMPARISON.md` gains a "New capability" entry (§3): json-c has no
  concept of a KQL type or of emitting a native `DateTimeOffset`/`TimeSpan`
  into a JSON value, so this is a pure addition, not a deviation to
  reconcile against upstream.
- Compiled-PDAG binary caches from before this change are incompatible
  (version bumped 1 → 2 → 3 across Phase 1 and Phase 2) and must be
  recompiled; this is the same cost any `CompiledEdge`/parser-`Data`
  schema change would carry, not specific to this feature.
- The `date-rfc3164`/`date-rfc5424` vs. `date-iso` inconsistency noted
  under Phase 2 (epoch `long` vs. native `DateTimeOffset` for
  conceptually the same "give me a real timestamp" request) is a known,
  intentionally-deferred gap — revisiting `date-rfc3164`/`date-rfc5424`'s
  existing `timestamp-unix[-ms]` modes to also support a native-value
  option is reasonable future work, but is a change to pre-existing
  shipped behavior and needs its own explicit sign-off, not something to
  fold silently into this ADR.
- New project/package surface: `DeltaZulu.Normalize` (`NormalizedField`,
  `NormalizedRecord`, `RecordNormalizer`), `tests/DeltaZulu.Normalize.Tests/`,
  both added to `DeltaZulu.Parse.slnx`. `docs/COMPARISON.md` is
  deliberately not touched by this — that document's whole scope is a
  comparison against upstream liblognorm's v2 engine, and
  `DeltaZulu.Normalize` has no liblognorm/json-c analog at all to compare
  against (see ADR-1: "normalize" was reserved for exactly this,
  non-comparable layer).
- Phase 3 beyond this first slice is still follow-on work: the schema
  mapping (rule/tag → target table/column) and the exact `Tx.Kql`-facing
  interface both need the real integration in hand to get right, not
  guessed at from this repo alone.
