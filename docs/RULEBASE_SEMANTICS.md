# Rulebase semantics — PDAG disambiguation

Derived from liblognorm's C source at commit `13e89c3f` (2026-08-12), read while
it was on disk during the corpus extraction. This records the rules that decide
**which parser wins when more than one could match at the same position** — the
part of behaviour that is implicit in the C implementation and is otherwise
recoverable only by reading it again.

Sources: `src/pdag.c`, `src/pdag.h`.

## The short version

At any PDAG node, candidate parsers are tried **in ascending priority order**,
and the **first one that matches wins**. There is no longest-match rule, no
scoring, and no ambiguity report. Reordering the rules in a rulebase file does
not change the outcome; changing a priority does.

## How a priority is computed

Every parser has a built-in priority, and the rulebase author may override it.
The two are packed into one integer (`pdag.c`, node construction):

```c
node->prio = ((assignedPrio << 8) & 0xffffff00) | (parserPrio & 0xff);
```

- The **user-assigned** priority occupies the upper 24 bits.
- The **parser built-in** priority occupies the low 8 bits.
- `DFLT_USR_PARSER_PRIO` is `30000` when the author specifies nothing.

The consequence is stated in the source's own guideline comment: the built-in
priority *only* breaks ties. Any explicit `"priority"` in a field definition
dominates every built-in value, because it is shifted eight bits left. An author
who sets a priority on one field and not another has not nudged the ordering —
they have overridden it completely for that field.

## Built-in priorities

`0` is highest, `255` lowest. From `parser_lookup_table` in `pdag.c`:

| Priority | Parsers |
|---:|---|
| 4 | `literal`, `repeat`, `whitespace`, `ipv4`, `ipv6`, `cisco-interface-spec`, `json`, `cee-syslog`, `cef`, `v2-iptables`, `checkpoint-lea` |
| 8 | `date-rfc3164`, `date-rfc5424`, `date-iso`, `time-24hr`, `time-12hr`, `name-value-list` |
| 16 | `number`, `float`, `hexnumber`, `kernel-timestamp`, `duration`, `mac48` |
| 32 | `word`, `alpha`, `string-to`, `char-to`, `char-sep`, `string` |
| 64 | `op-quoted-string`, `quoted-string` |
| 255 | `rest` |

`rest` sits alone at 255 by design — the comment reserves that value for "things
that *really* should only be run as last resort". It is the sink that matches
anything, so anything sharing its priority would be unreachable behind it.

Note the shape of the table: highly structured formats that either match or do
not (`ipv4`, `json`, `cef`) sit at 4, and the greedy generic matchers
(`word`, `char-to`, `string`) sit at 32 or below. The ordering is *specificity
descending*, which is what makes first-match-wins produce sensible results
without a longest-match rule.

## Sorting and the walk

Parsers at a node are sorted once, at optimisation time, by raw priority:

```c
static int qsort_parserCmp(const void *v1, const void *v2)
{
    return p1->prio - p2->prio;
}
```

`qsort` is **not stable**, so two parsers with an identical packed priority have
an unspecified relative order. That matters more than it looks: two fields of the
same type at the same position with no explicit priority tie exactly, and which
one is attempted first is then a property of the C library's sort implementation
rather than of the rulebase. A port that iterates in declaration order is not
wrong, but it is not guaranteed to agree either — this is the one place where
matching upstream exactly is not well-defined.

The walk itself is a depth-first traversal with backtracking: a parser that
matches advances into its child node, and if the remainder of that path fails,
the walk returns and tries the next parser at the original node.

## Literal path compaction

`optLitPathCompact` merges consecutive literal nodes into one at optimisation
time, so a shared prefix is evaluated once rather than per rule. It refuses to
compact a literal that:

- is not `PRS_LITERAL`, or
- has a name (the literal is being parsed out into a field), or
- is a terminal node, or
- has `refcnt != 1` (it is shared by another path), or
- has other than exactly one child parser, or
- whose child is not itself an unnamed literal with `refcnt == 1`.

The `refcnt == 1` conditions are what make this safe: a node reachable from more
than one rule cannot be merged into any single one of them. A port that compacts
without checking the reference count will silently merge paths that belong to
different rules.

## What this means for DeltaZulu.Parse

1. **First match wins, in priority order.** Any implementation that collects all
   matches and then chooses is not equivalent, however reasonable the choice.
2. **Author-assigned priority dominates.** It is not a hint.
3. **Ties are unspecified upstream.** Where two parsers tie, upstream parity is
   not a well-defined target, and a deliberate, documented order here is better
   than trying to reproduce `qsort`.
4. **Compaction requires the reference-count guard**, or rules bleed into each
   other in a way that produces plausible wrong output rather than an error.

## Corpus coverage of these paths

From `tests/fixtures/liblognorm-parity/manifest.json` (384 cases). Motifs are
counted across **both** rulebase syntaxes — the JSON form `"type":"x"` and the
`%name:type%` shorthand — because the corpus uses both heavily:

- **3 cases** use auxiliary rulebases, all via an `include=` directive.
- **0 cases** reference a custom `@type` defined in another file. The cross-file
  custom-type resolution path is **not covered by the upstream corpus at all**,
  so parity passing says nothing about it.
- **6 cases** set an explicit `"priority"`, e.g.
  `%{"name":"field", "type":"mac48", "priority":100}%`. Thin coverage of the one
  mechanism that overrides all built-in ordering.
- **`v2-iptables`: 15 cases. `cisco-interface-spec`: 5 cases.** Both verified by
  direct grep as well as by the manifest.

That last line matters for any plan to drop those two motifs: doing so forfeits
20 cases of upstream parity coverage, which is a decision to take deliberately
rather than a rounding error. An earlier count here read only the JSON syntax and
reported `cisco-interface-spec` as 0 and `v2-iptables` as 1, which would have made
the removal look free.

The cross-file `@type` gap and the thin priority coverage are the places where
upstream parity can be green while behaviour differs.
