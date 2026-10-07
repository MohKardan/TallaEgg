# TallaEgg — Claude Code

Claude Code loads this file automatically. Everything that applies to an agent working in this
repository is in [`AGENTS.md`](AGENTS.md), which every other tool reads too, and which is imported here
in full:

@AGENTS.md

## Only for Claude Code

- **Do not add rules to this file.** A rule here is invisible to Gemini, Cline, Codex and every other
  agent that works in this repository. Put it in `AGENTS.md`. This file used to hold the repository's
  hard rules, and that is part of why another tool pushed straight to `main` on 2026-10-07.
- **Your auto-memory is private to this machine and to Claude.** When something you learn would help
  the next agent, propose it in the shared places listed under "Shared memory" in `AGENTS.md`, and keep
  in auto-memory only what is about this machine or this account.
- The skills in `.claude/skills/` (`work-an-issue`, `run-tallaegg`) are invoked with the Skill tool.
