<!--
Lean PR template — GitHub auto-loads this into every new PR.
The FULL checklist (security, testing, deployment, rollback) lives in
docs/process/PR_TEMPLATE.md — copy the relevant sections in for critical/security PRs.
-->

## Description
<!-- 2–3 sentences: what changed and why. -->

## Linked Task / Finding
Closes #___  ·  Audit finding: C-# / H-# (if any)

## Type
- [ ] Hotfix  - [ ] Feature  - [ ] Refactor  - [ ] Performance  - [ ] Docs  - [ ] Security

## Changes
-

## Checklist (Definition of Done — see docs/process/STANDARDS.md)
- [ ] Builds without warnings; tests pass (`dotnet test`)
- [ ] No secrets/tokens/credentials in the diff
- [ ] Comments in English, explain the "why"
- [ ] Follows naming conventions (STANDARDS.md)
- [ ] Tests added/updated in `tests/TallaEgg.AllServices.Tests` (the solution's only test project)
- [ ] Docs updated if behavior/API changed
- [ ] Peer reviewed (second reviewer for financial/security/critical changes)

<!-- Critical or security change? Also fill the Security + Deployment/Rollback sections
from docs/process/PR_TEMPLATE.md. -->
