# ADR-6: `DeltaZulu.Parse` includes a syslog envelope pre-decoder

## Status

Accepted. Adds new, deliberate scope beyond a pure v2-engine port (same
category as `KqlType`, hot reload, and directory-tree rulebase loading —
see `docs/COMPARISON.md` "New capability" sections); does not change the
PDAG/rulebase engine itself, which remains a direct port. Does not reopen
ADR-3's "no schema mapping" decision — this is transport framing, not
semantic/schema mapping.

Note on numbering: ADR-1 and ADR-2 forward-referenced "ADR-5" as a future
semantic-view layer that would map parsed fields onto a common schema.
ADR-3 subsequently decided that layer will never be built in this
repository ("If a schema-mapping need does surface later, it gets its own
ADR... not a reopening of this one."), which leaves those forward
references stale but — per this project's immutable-ADR convention —
uncorrectable in place. To avoid this decision being confused with that
retired one, it is filed as ADR-6; 5 stays retired/unused.

## Context

While debugging why a real-world Sagan rulebase failed to parse a Snort
log fed to `LogNormalizer.Cli` via `-m` with the *complete* raw syslog
line (`"Jun  2 00:41:47 demo snort: [1:19559:5] ..."`), the actual cause
turned out to be architectural, not a bug: liblognorm — and this port —
has never had any concept of syslog envelope framing. Checked directly
against upstream:

- liblognorm's own docs/repo describe it as normalizing an
  already-extracted message string; its rulebase grammar and type system
  have no field type or construct for a syslog envelope at all.
- rsyslog's `mmnormalize` module (the reference way liblognorm is actually
  invoked) normalizes `$msg` by default — the property rsyslog's own
  RFC3164/RFC5424-aware input/parser modules populate *after* stripping
  the envelope. Its `useRawMsg` parameter exists specifically as a
  non-default escape hatch, which is itself proof that `$msg`-only,
  envelope-already-gone is the normal case, not an oversight.
- Sagan's `normalize.rulebase` (the file that surfaced this) reflects that
  assumption throughout: nearly every rule begins with a bare literal
  leading space — exactly the one character rsyslog's tag-parser leaves
  behind in `$msg` once it consumes `programname: ` up through the colon
  from a classic BSD line. The rule authors assumed a syslog receiver had
  already done that stripping; liblognorm was never asked to.

That split — a separate syslog-aware receiver in front of a
protocol-agnostic message-body normalizer — is the right design for
liblognorm itself: it's meant to normalize non-syslog input just as
readily (web server logs, structured JSON, whatever a rulebase author
targets), so baking syslog-specific framing into the matcher would be
wrong there. `DeltaZulu.Parse` inherited the same split by omission, not
by decision — no ADR ever considered it, it simply was never built,
because the port's original README-stated scope was "a direct port of the
v2 engine only."

That scope statement predates this codebase's actual role. This library
is not tracked purely against upstream anymore (ADR-2's `KqlType` already
established that precedent explicitly); it is the core parsing capability
of an agent that receives real syslog traffic directly, and the friction
of that gap is exactly what surfaced here: a real rulebase, a real log
line, and a manual "strip the header by hand first" workaround before the
existing engine could do anything with it. Requiring every caller —
`LogNormalizer.Cli` used ad hoc against a raw capture, or the agent's own
ingestion path — to either reimplement RFC3164/RFC5424 framing themselves
or stand up a separate rsyslog process just to reach the point every
rulebase already assumes, is friction this codebase is now in a position
to remove, without touching the engine that everything else in this port
still tracks faithfully against upstream.

## Decision

Add a syslog envelope pre-decoder to `DeltaZulu.Parse`, as its own
self-contained, explicitly-invoked component — not a change to
`RulebaseLoader`/`PdagWalker`/the rulebase grammar, and not run
implicitly inside `Parse(...)`.

- `SyslogDecoder.TryDecode(string line, out SyslogEnvelope envelope)`
  recognizes RFC 3164 (BSD: optional `<PRI>`, `Mmm d[d] hh:mm:ss host
  tag[[pid]]: msg`, including the single-digit-day double-space form) and
  RFC 5424 (`<PRI>1 timestamp host app-name procid msgid
  structured-data msg`) framing. `SyslogEnvelope` exposes the decoded
  facility/severity/timestamp/host/app-name/procid/msgid/structured-data
  fields plus `Msg` — the remainder to hand to a rulebase.
- `Msg` preserves whatever separator character(s) actually followed the
  header in the input (for RFC3164, that's normally a single leading
  space after the tag's colon) instead of trimming it — matching what
  rsyslog's own `$msg` contains, since real-world rulebases like Sagan's
  are written against exactly that, leading space included.
- A message that doesn't match either framing decodes as
  `Framing: None`, `Msg` equal to the original line unchanged — callers
  that don't want this step, or whose input was never syslog-framed to
  begin with, are unaffected whether or not they call it.
- `LogNormalizer.Cli` gets an opt-in `--syslog` flag that runs this
  decoder ahead of `ctx.Parse(...)`, so a raw capture like the one that
  prompted this ADR can be tested directly instead of requiring manual
  preprocessing.

## Consequences

- New public API surface in `DeltaZulu.Parse`: `SyslogFraming`,
  `SyslogEnvelope`, `SyslogDecoder`. `docs/COMPARISON.md` gains a "New
  capability" entry — there is no C liblognorm equivalent to reconcile
  against, same framing already used there for `KqlType`, hot reload, and
  directory-tree loading.
- README's "Scope" section ("a direct port of the v2 engine only") is
  narrowed by this ADR the same way ADR-2's `KqlType` already narrowed it
  without contradicting it: the PDAG/rulebase engine remains a direct v2
  port; this decoder is additive scope that runs *before* it, not a
  change to it.
- Rulebase authoring is unaffected: a rule still matches against a
  message body exactly as before. This only changes what produces that
  body when the input is a raw syslog line — it is a preprocessing step
  a caller opts into, not a new rule-matching behavior.
- Does not reopen ADR-3. A syslog envelope's host/app-name/timestamp
  fields are transport metadata with a fixed, protocol-defined grammar,
  not "extracted application fields mapped onto a common security-event
  schema" — the thing ADR-3 puts out of scope. `SyslogEnvelope`'s field
  names are exactly the RFC's own field names, no schema/table mapping
  involved.
- ADR-5 stays retired per the Status note above; ADR-1/ADR-2's internal
  "ADR-5" references remain stale (as ADR-3 already left them) and are
  unrelated to this decision.
