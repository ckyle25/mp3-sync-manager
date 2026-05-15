# MP3 Sync Manager

We are building a Windows-first desktop application for managing music on a child's MP3 player.

Primary behavior:
- The app is configured once with a source music folder.
- The source music folder is treated as read-only.
- The app automatically detects the MP3 player when plugged in.
- The MP3 player is plugged in via USB.
- The app allows copying music from the source folder to the MP3 player.
- The app allows removing music from the MP3 player.
- The app must never modify or delete files in the source folder.

Platform goals:
- Primary target: Windows
- Secondary target: macOS if practical
- Preferred stack: C#/.NET with Avalonia

Engineering rules:
- Keep the architecture simple.
- Favor small, incremental changes.
- All delete operations must be restricted to the detected MP3 device root.
- Do not introduce cloud services or a backend.
- Do not store secrets in the repo.
- Ask the architect agent before changing core device-detection or safety rules.
- Ask the QA agent before declaring a feature complete.

UI quality standards:
- The UI must feel like a real desktop app, not a developer prototype.
- Favor clean spacing, visual hierarchy, and clear empty states.
- Avoid cramped layouts and overly dense screens.
- Primary actions should be obvious.
- Dangerous actions must be visually distinct and require confirmation.
- The app should be usable by a child.
- The app should look acceptable on first launch without additional styling passes.
- Before declaring a UI slice complete, review layout, spacing, labels, and empty states.

Initial feature priorities:
1. First-run setup for source folder
2. Device auto-detection
3. Browse source music
4. Browse device music
5. Copy selected music to device
6. Remove selected music from device

## Claude coordination rules

Claude is the primary implementing agent for this repository.

When Codex creates or refines GitHub issues for Claude:
- keep scope narrow and feature-slice based
- instruct Claude to use explicit Claude subagents where relevant
- prefer one Claude subagent at a time unless the work is clearly separable

Expected Claude subagent usage:
- product: issue wording, acceptance criteria, child-friendly wording, UI clarity
- architect: design decisions, service boundaries, safety constraints
- frontend-engineer: Avalonia UI and interaction work
- backend-engineer: models, services, filesystem behavior, tests tied to implementation
- qa: blockers, regressions, risk review, go/no-go recommendations

Issue-writing rules for Codex:
- explicitly state which Claude subagent(s) should be used
- include in-scope and out-of-scope sections
- include acceptance criteria
- include tests to add or update
- include a stop condition such as "do not add additional features"