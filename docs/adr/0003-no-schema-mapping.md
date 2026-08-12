# ADR-3: The semantic view layer does not do schema mapping

## Status

Accepted. Narrows ADR-1's description of the future semantic view layer;
does not change ADR-1's naming decision, which stands.

## Context

ADR-1 (`docs/adr/0001-naming.md`, accepted before this codebase's KQL work
began) reserved the word "normalize" for "a future semantic view layer...
that will sit on top of parsed output and map it onto a common schema,"
naming "semantic normalization in the ASIM/ECS sense (mapping extracted
fields onto a common security-event schema across sources)" as the
motivating second meaning of "normalize" that collided with this library's
own use of the word.

ADR-2 (`docs/adr/0002-kql-common-type-denominator.md`) then built the
first real slice of that reserved layer: `DeltaZulu.Normalize`'s
`RecordNormalizer.Normalize(ParseResult) -> NormalizedRecord`, a KQL-typed
projection of parsed fields that leaves field names exactly as the parser
committed them — no renaming, no routing to a target table/column. While
confirming that slice's scope was correct, not just incomplete, the org
clarified directly: this codebase is a parser plus a KQL-typed record
projection, not a schema mapper. ASIM/ECS-style mapping onto a common
schema is not work this repository does, at any layer, now or in the
future.

## Decision

`DeltaZulu.Normalize` (and `DeltaZulu.Parse`, and anything added to either
later) stops at KQL-typed, field-name-preserving records. Schema mapping,
if and when it happens, is entirely the responsibility of whatever
consumes a `NormalizedRecord` — `Tx.Kql` locally, a transpiler centrally —
outside this codebase. There is no "map extracted fields onto a common
schema" layer planned inside this repository. ADR-1's description of one
was accurate as a statement of the *ambiguity problem* "normalize" needed
to be freed up for, but is now known to be inaccurate as a prediction of
what would actually get built here.

## Consequences

- ADR-1's Status is updated (metadata only — its Context/Decision text is
  left untouched, per this project's immutable-ADR convention) to note it
  is superseded in part by this ADR: the naming decision itself ("Parse,"
  not "Normalize") stands unchanged; the prediction of what the reserved
  "normalize" layer would eventually do is what's corrected here.
- No code changes. `docs/adr/0002-kql-common-type-denominator.md`'s Phase
  3 description already matches this decision (`NormalizedRecord`'s
  columns are exactly the parsed field names, and schema mapping is
  already stated there as not this layer's job) and needs no further
  change.
- If a schema-mapping need does surface later, it gets its own ADR
  proposing a new layer or an external system — not a reopening of this
  one.
