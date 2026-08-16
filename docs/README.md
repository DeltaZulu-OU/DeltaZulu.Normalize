# DeltaZulu.Parse — documentation

Architecture, Decisions, Constraints and roadmaps for the DeltaZulu estate live
in **[`DeltaZulu-OU/docs`](https://github.com/DeltaZulu-OU/docs)**, not here.

| Looking for | Go to |
|---|---|
| Decisions governing this repository | [`architecture/GOVERNING-DECISIONS.md`](https://github.com/DeltaZulu-OU/docs/blob/main/architecture/GOVERNING-DECISIONS.md) |
| The estate-wide pipeline architecture | [`architecture/PIPELINE.md`](https://github.com/DeltaZulu-OU/docs/blob/main/architecture/PIPELINE.md) — read with `PIPELINE-ERRATA.md` |
| Facts the estate does not control | [`constraints/`](https://github.com/DeltaZulu-OU/docs/tree/main/constraints) |
| This repository's historical ADRs | [`archive/DeltaZulu.Parse/`](https://github.com/DeltaZulu-OU/docs/tree/main/archive/DeltaZulu.Parse) |
| Roadmaps | [`roadmaps/`](https://github.com/DeltaZulu-OU/docs/tree/main/roadmaps) |
| Verification evidence | [`reports/`](https://github.com/DeltaZulu-OU/docs/tree/main/reports) |

Decisions are numbered globally across the estate. The per-repository scheme this
replaced produced collisions that citations could not resolve — `DeltaZulu.Agent`
ADR 0014 and `DeltaZulu.Platform` ADR 0014 decide opposite things, and the Agent
carried two different ADR 0003 documents, so "ADR 0003" did not resolve even
within one repository.

## What remains here

- `COMPARISON.md` — how this port differs from upstream liblognorm.
- `RULEBASE_SEMANTICS.md` — PDAG disambiguation derived from the C source:
  priority packing, first-match-wins, and the guards that make literal path
  compaction safe. Read it before changing the walker.
- `PACKAGE_README.md` — shipped inside the NuGet package.

The parity corpus is committed under `tests/fixtures/liblognorm-parity/` (384
cases, pinned to a named liblognorm commit). It used to be scraped at run time
from an unpinned upstream, which is why recorded case counts of 374, 381 and 384
can all have been correct on their respective days.
