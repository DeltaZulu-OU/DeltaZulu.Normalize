# liblognorm parity corpus

384 upstream test cases, extracted and **committed** on 2026-08-16.

## Why this exists

`scratch/parity_check.py` discovered these cases by scraping liblognorm's
`tests/*.sh` at run time. Nothing was committed, and `.github/workflows/parity.yml`
checks out `rsyslog/liblognorm` **with no ref pinned**, so it took whatever the
default branch happened to be on the day it ran.

Two consequences, and the second explains a long-standing discrepancy:

1. Deleting the workflow would have deleted the corpus, silently and with no diff.
2. The case count was never a fixed number. Recorded figures of **374** and **381**
   have both been cited; this extraction, at liblognorm `13e89c3f` (2026-08-12),
   yields **384**. All three can have been correct on their respective days. The
   count was a run-time outcome of an unpinned upstream, not a property of this
   repository.

## Provenance

| | |
|---|---|
| Repository | `https://github.com/rsyslog/liblognorm` |
| Commit | `13e89c3f4314dde9a0b649ddd4e3159c7e641946` |
| Commit date | 2026-08-12 |
| Extracted | 2026-08-16 |
| Cases | 384 |

Discovery statistics, per-case metadata, and motif coverage are in
`manifest.json`.

## Layout

One directory per case, named from its upstream label (`fixture.sh#N`):

```
<case>/main.rulebase     the main rulebase
<case>/message.txt       the input line
<case>/<name>.rulebase   auxiliary rulebases, when the case uses them
```

## What the corpus does not cover

Recorded because a green parity run says nothing about these:

- **Cross-file custom `@type` references: 0 cases.** The 3 cases with auxiliary
  rulebases all use an `include=` directive, and all three are the same trivial
  `mac48` rule.
- **Explicit `"priority"`: 6 cases.** This is the mechanism that overrides every
  built-in parser priority, so it is thinly covered relative to its power.

See `docs/RULEBASE_SEMANTICS.md` for the disambiguation rules these cases exercise.

## Do not regenerate casually

Re-running discovery against a newer liblognorm will change the corpus and the
count. That is a deliberate act: update the commit recorded above, and treat any
case-count change as something to explain rather than absorb.
