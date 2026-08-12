# ADR-2: KQL as the common output-type denominator

## Status

Accepted (Phase 1 — field-level type metadata — implemented; see
Consequences for what's deliberately not yet in scope).

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

**Phase 2 (not yet done): native scalar emission for the still-string-only
temporal motifs.** `date-iso`, `time-24hr`, `time-12hr`, `duration`, and
`kernel-timestamp` are semantically `DateTime`/`Timespan` but have no
`format=`-style opt-out today, so Phase 1 conservatively tags them
`String`. Extending the existing `format=` convention
(`number`/`float`/`hexnumber`/`date-rfc3164`/`date-rfc5424` already have
it) to these five motifs is real parser-behavior work with its own parity
burden, deliberately kept out of this phase.

**Phase 3 (not yet scoped): the actual `Tx.Kql`/transpiler-facing layer.**
This is the `DeltaZulu.Normalize` project ADR-1 reserved. It needs to
consume `ParseResult` + Phase 1's `KqlType` tags + a schema mapping (which
rule/tag maps to which target table/column names) and produce whatever
`Tx.Kql` and the central transpiler actually need — most likely a typed
row abstraction. This repo has no visibility into either contract, so
Phase 3 is intentionally left unscoped until that input is available,
rather than guessed at.

## Consequences

- New public API surface: `KqlType` enum, `ParseResult.TryGetKqlType`.
  Purely additive — no existing member changed shape or behavior.
- `docs/COMPARISON.md` gains a "New capability" entry (§3): json-c has no
  concept of a KQL type, so this is a pure addition, not a deviation to
  reconcile against upstream.
- Compiled-PDAG binary caches from before this change are incompatible
  (version bump 1 → 2) and must be recompiled; this is the same cost any
  `CompiledEdge` schema change would carry, not specific to this feature.
- Phase 2 and Phase 3 are follow-on work, not implied to be done by
  accepting this ADR. Phase 3 in particular needs the `Tx.Kql`/transpiler
  contracts pinned down before it can be scoped, let alone implemented.
