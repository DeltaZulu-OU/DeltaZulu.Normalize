# Contributing to DeltaZulu.Parse

## Monitor upstream parity

DeltaZulu.Parse ports the v2 rulebase engine from
[`rsyslog/liblognorm`](https://github.com/rsyslog/liblognorm). Changes to the
port should preserve upstream parsing behavior unless there is a deliberate
reason to diverge. KQL type metadata has no C equivalent, but it does not alter
the CLI JSON for the discovered upstream fixtures.

The corpus is now committed under `tests/fixtures/liblognorm-parity/` — 384
cases, pinned to liblognorm `13e89c3f`. Earlier figures of 374 and 381 were not
wrong so much as unpinned: `parity.yml` checks out `rsyslog/liblognorm` with no
ref, so the count was a property of upstream's default branch on the day of each
run. Treat a change in the count as something to explain, not to absorb.

Before merging a change to a motif parser or the PDAG builder, compiler, or
walker:

1. Run the unit tests.
2. Run `scratch/parity_check.py` against the upstream C fixtures and the real
   C `lognormalizer` executable. Do not replace the C-generated results with
   expected output maintained in this repository.
3. Investigate every mismatch. If a difference is intentional, add an entry to
   `docs/COMPARISON.md` in the same pull request, including the rationale and
   focused regression-test coverage. An undocumented mismatch is not an
   acceptable baseline update.

The **Upstream parity** workflow enforces the second step for changes to the
parsers and PDAG compilation/walking path. Its fixture count is not a fixed
acceptance threshold: the harness discovers runnable cases directly from the
checked-out upstream test suite, so additions to that suite are exercised as
they become available.

`docs/COMPARISON.md` is the living record of intentional semantic differences,
not a one-time audit artifact. Update it before an intentional deviation is
merged, rather than documenting the behavior only after a bug report.

## The deletion rule

**Deleting a project, a public type, or a line of work requires either a status
note on the governing Decision, or a new Decision with status `Rejected`.**

Deleting the code is the easy half. Recording why it stopped being the plan is
the half that keeps the next reader from rebuilding it, or from concluding that
something was abandoned when it was never started.

Both failure modes have happened in this repository. `DeltaZulu.Normalize` was
deleted with three public types and no Decision recording it. And the semantic
view layer it was a first slice of is *reserved but never built* — reserved by
`ADR-1`, narrowed by `ADR-3` — which has repeatedly been mistaken for a layer
that was removed. There is no ADR 0005 and never has been.

The rule applies to a project, a public type, or a line of work — not to a
private helper or a behaviour-preserving refactor.

## Governance

Decisions (`DEC-NNNN`) and Constraints (`CON-NNNN`) live in
[`DeltaZulu-OU/docs`](https://github.com/DeltaZulu-OU/docs), not here. See
`docs/README.md` for the ones governing this repository.

A **Constraint** is a fact the estate does not control, and is immutable. A
**Decision** is a choice, and carries both the alternatives it rejected and a
revisit trigger — the named condition under which it should be reopened.

`governs-check` in the docs repository fails when an `Accepted` Decision names a
symbol or path here that no longer exists. If it fails against your change,
either the Decision needs updating or the deletion needs recording. Do not
silence it by trimming the Decision's `governs:` block.
