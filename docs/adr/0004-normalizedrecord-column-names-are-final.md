# ADR-4: `NormalizedRecord` column naming is final, not open Phase-3 work

## Status

Accepted. Narrows ADR-2's Phase 3 framing; does not change ADR-2's Phase
1/Phase 2 decisions or its implemented Phase 3 code, which stand.

## Context

ADR-2 (`docs/adr/0002-kql-common-type-denominator.md`) described its
Phase 3 first slice as leaving open "a rule/tag → target-table/
column-name schema mapping" — i.e. it framed `NormalizedRecord`'s
field-name-preserving columns as an incomplete answer, still owed by
Phase 3, rather than the correct and final answer.

ADR-3 (`docs/adr/0003-no-schema-mapping.md`) has since settled the
substance of this: `DeltaZulu.Normalize` stops at KQL-typed,
field-name-preserving records, and schema mapping is not work this
repository does at any layer. ADR-2's own Phase 3 prose predates that
decision and reads as though the question were still open.

## Decision

`NormalizedRecord`'s columns being exactly the parsed field names — no
renaming, no routing to a target table/column — is final, per ADR-3, not
an open item Phase 3 still owes. Treat ADR-2's Phase 3 section's framing
of this as unresolved as superseded by ADR-3's decision; no further
schema-mapping work is planned against that "gap."

## Consequences

- ADR-2's Status is updated (metadata only, per this project's
  immutable-ADR convention) to point here; its own text is otherwise
  unchanged.
- No code changes — `RecordNormalizer`'s existing behavior (columns as
  parsed field names) already matches this decision.
- This ADR exists only to correct ADR-2's prose in light of ADR-3; ADR-3
  is the actual decision record for "why no schema mapping." Read that
  one for the reasoning.
