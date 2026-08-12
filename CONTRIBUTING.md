# Contributing to DeltaZulu.Parse

## Monitor upstream parity

DeltaZulu.Parse ports the v2 rulebase engine from
[`rsyslog/liblognorm`](https://github.com/rsyslog/liblognorm). Changes to the
port should preserve upstream parsing behavior unless there is a deliberate
reason to diverge. KQL type metadata has no C equivalent, but it does not alter
the CLI JSON for the currently discovered upstream fixtures: the recorded
parity run passes all 374 cases.

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
