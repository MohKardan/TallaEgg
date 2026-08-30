# Audit Archive

Every risk audit this project has run, one file per run, kept forever.

**These files are dated measurements, not status trackers.** An archived audit is never
edited to mark work done — not the findings, not the score, not a banner. Remediation
status lives in GitHub issues and pull requests:

```
gh issue list --label audit-finding
```

The one exception is a factual correction to what the audit *observed*, made inline and
labelled as a correction so the original claim stays visible. `AUDIT_2026-08.md` has one,
about a build-warning count.

## How the next audit is run

Follow [`METHODOLOGY_v8.md`](METHODOLOGY_v8.md). It is the current methodology; hand it to
the auditor rather than pasting an older prompt. It detects re-audit mode from this file, so
keep the table below current. [`METHODOLOGY_v7.md`](METHODOLOGY_v7.md) is retired and kept only
because the 2026-08-29 audit was run under it.

A second audit in a month that already has one gets a letter suffix (`AUDIT_2026-08b.md`)
rather than replacing the file that is there. Nothing in this directory is ever overwritten.

An audit should be run by someone — or something — that did not write the code. The
agent that made a change is not independent evidence about it.

## Trend

| Date | Overall | Production readiness | Methodology | Model | Files |
|---|---|---|---|---|---|
| 2026-07-08 | 4.6 / 10 | 30% | [v1](METHODOLOGY_v1.md) | Claude Opus 4.8 | [`AUDIT_2026-07.md`](AUDIT_2026-07.md) (summary), [`AUDIT_2026-07.html`](AUDIT_2026-07.html) (full, Persian) |
| 2026-08-26 | 6.6 / 10 | ~55% | none — ad hoc | Claude Opus 5 | [`AUDIT_2026-08.md`](AUDIT_2026-08.md) |
| 2026-08-29 | 7.8 / 10 | ~65% | v7.0 | GLM (Z.ai) / Cline | [`AUDIT_2026-08b.md`](AUDIT_2026-08b.md) |

**Read this column-by-column, not row-by-row.** The three runs used different methods and
different models — the product owner supplied the model attribution for the first two rows in
August 2026, and the July prompt itself in `METHODOLOGY_v1.md`, so the column is now known
for every run, and the method is readable for two of the three. A score that rises between
runs may mean the code improved, or that a different reader weighted the same code
differently. The comparison is only sound where the methodology and model columns match —
which, so far, they never do. v7 requires both to be recorded, so the 2026-08-29 run is the
first whose successor can be genuinely comparable to it.

**The 2026-08-26 run had no methodology.** It was commissioned by two sentences inside a
message about something else — audit the code, every service except Affiliate — and
delivered twelve minutes later. It was also run by the same session that had been editing
this repository that day, so it was not independent of the code it measured in even the
weakest sense. Both facts belong next to its 6.6, and neither is visible in the audit
itself.

One independence fact, checked 2026-08-30 and worth weighting the rows by: all 31 commits
merged between the 2026-08-26 audit and the 2026-08-29 one (`git log --since=2026-08-26
--no-merges`) were authored under the product owner's account carrying `Co-authored-by`
trailers from Claude models — 30 by **Claude Opus 5**, the same model credited with the
2026-08-26 audit, and 1 (#115, `83cc77b`) by **Claude Sonnet 5** — while GLM, the 2026-08-29
run's model, appears nowhere in the repository's authorship and wrote none of the code under
review. The 2026-08-29 run is therefore the first in this table performed by a model that did
not write the code it audited, which is what the independence line at the top of this file
asks for.

Scope has also varied: the August pass excluded the **Affiliate** service at the product
owner's request, and v7 keeps that exclusion.

## Why the methodology keeps changing

Each version answers failures found in the run before it, so the version column above is also
a record of what this project learned about auditing itself:

| Version | Answers |
|---|---|
| v1 | Nothing yet — the first pass. Fifty-odd review dimensions weighted equally, a score for each, and no rule about evidence, reproduction, scope, or what counts as a finding |
| v7 | Findings inferred from the shape of the code without checking product intent; an audit that only read and never ran anything; a directory the method named but that did not exist |
| v8 | Claims written during synthesis that no session had checked; a prior-audit status row carried forward for a problem already fixed; a verified sample stated as a verified universe; command output lost by the terminal and read as a negative result |

A rising score with a rising methodology version is not evidence of a rising codebase. The two
have to be read together, which is what the caveat above is for.

## Known reliability of what is archived here

`AUDIT_2026-08.md` closes with a section assessing its own errors: two of its six new
findings were wrong, both because they were inferred from the shape of the code rather
than verified against product intent, and both were caught by the product owner rather
than by the audit. That section is why v7 makes an audit self-assessment mandatory, and
why it requires Critical and High findings to be reproduced rather than only read.

Treat findings in the archive that were traced through an actual execution path as
substantially more reliable than those reasoned from structure.

## History of this directory

These files lived in `docs/operations/` and `docs/` until they were collected here so
that mode detection, the trend table, and a stable naming scheme could work. Names
changed at the same time; the content did not.

| Was | Is now |
|---|---|
| `docs/operations/AUDIT_FINDINGS.md` | `docs/audit/AUDIT_2026-07.md` |
| `docs/CODE_AUDIT_REPORT.html` | `docs/audit/AUDIT_2026-07.html` |
| `docs/operations/RE_AUDIT_2026-08.md` | `docs/audit/AUDIT_2026-08.md` |

Two cross-links *inside* `AUDIT_2026-08.md` were repointed at the same time, so that its
references to the July audit keep resolving. Nothing else in either archived file was
touched.

The HTML file is the long-form Persian report of the July audit; the Markdown file of
the same date is its English summary. Both describe one audit. v7 produces Markdown
only.
