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

Follow [`METHODOLOGY_v7.md`](METHODOLOGY_v7.md). It is the current methodology; hand it
to the auditor rather than pasting an older prompt. It detects re-audit mode from this
file, so keep the table below current.

A second audit in a month that already has one gets a letter suffix (`AUDIT_2026-08b.md`)
rather than replacing the file that is there. Nothing in this directory is ever overwritten.

An audit should be run by someone — or something — that did not write the code. The
agent that made a change is not independent evidence about it.

## Trend

| Date | Overall | Production readiness | Methodology | Model | Files |
|---|---|---|---|---|---|
| 2026-07-08 | 4.6 / 10 | 30% | unversioned | not recorded | [`AUDIT_2026-07.md`](AUDIT_2026-07.md) (summary), [`AUDIT_2026-07.html`](AUDIT_2026-07.html) (full, Persian) |
| 2026-08-26 | 6.6 / 10 | ~55% | unversioned | not recorded | [`AUDIT_2026-08.md`](AUDIT_2026-08.md) |

**Read this column-by-column, not row-by-row.** The two runs used different methods and
different models, and neither recorded which model. A score that rises between runs may
mean the code improved, or that a different reader weighted the same code differently.
The comparison is only sound where the methodology and model columns match — which, so
far, they never do. v7 requires both to be recorded, so the next run is the first one
that will be genuinely comparable to its successor.

Scope has also varied: the August pass excluded the **Affiliate** service at the product
owner's request, and v7 keeps that exclusion.

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
