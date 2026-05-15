# AGENTS.md

This file contains repository-specific instructions for Codex and other coding agents.

## Project

- Name: MP3 Sync Manager
- Primary target: Windows
- Secondary target: macOS if practical
- Stack: C# / .NET / Avalonia
- App type: desktop application

## Product purpose

This app lets a child manage music on an MP3 player that appears as a removable drive on a computer.

The app must:
- allow a parent to configure a source music folder once
- treat the source music folder as read-only
- auto-detect the MP3 player/device when it is connected
- let the user browse music in the source library
- let the user copy music from the source library to the device
- let the user remove music from the device
- never modify or delete files in the source folder

## File/folder model

The source library is organized as:

- Artist/Album/Song

The MP3 player/device should mirror the same nested folder structure:

- Artist/Album/Song

Source and device listing behavior should stay consistent with that model.

## Safety rules

These rules are mandatory:

- The source music folder is always read-only.
- Copy operations may only read from the configured source root.
- Delete operations may only affect files under the detected device root.
- Dangerous actions must require confirmation.
- User-facing errors must use plain-language messages.
- Do not surface raw exception text or raw file-system paths in child-facing UI.
- Never introduce behavior that could silently modify, rename, or delete source files.

## UX rules

- The app should feel simple, readable, and desktop-friendly.
- The UI should be understandable for a child.
- Avoid developer-looking or prototype-looking layouts.
- Use strong empty states and clear labels.
- Dangerous actions should be visually distinct.
- Progress and status should be visible and easy to understand.

## Agent roles

Codex acts as planner/reviewer/workflow manager for this repository.

Claude is the primary implementing agent for this repository.

Codex should:
- define small, isolated feature slices
- write or refine GitHub issues
- review PRs and completed work
- identify blockers, regressions, and missing tests
- recommend the next smallest useful task
- keep scope controlled

Codex should not:
- create large multi-feature tasks when a smaller slice is possible
- bypass safety or UI quality rules
- assume code is correct just because it builds
- advance to the next feature before the current slice is reviewed

## Claude coordination rules

When Codex creates or refines GitHub issues for Claude:
- keep scope narrow and feature-slice based
- explicitly state which Claude subagent(s) should be used
- prefer one Claude subagent at a time unless the work is clearly separable

Expected Claude subagent usage:
- product: issue wording, acceptance criteria, usability, child-friendly wording
- architect: design decisions, service boundaries, safety constraints
- frontend-engineer: Avalonia UI, layout, interaction work, polish
- backend-engineer: models, services, filesystem behavior, copy/delete logic, tests tied to implementation
- qa: blockers, regressions, test gaps, go/no-go recommendations

Issue-writing rules for Codex:
- clearly state Goal
- clearly state In scope
- clearly state Out of scope
- include Acceptance criteria
- include Tests to add or update
- include Risks / notes
- include a stop condition such as "do not add additional features"

## Workflow rules

Use GitHub issues and pull requests as the handoff layer.

Preferred workflow:
1. Codex defines or refines the issue.
2. Claude implements the issue.
3. Claude updates tests as needed.
4. Codex reviews the result.
5. If needed, Codex creates a follow-up issue or requests revisions.
6. Only then should the next slice begin.

## Definition of done

A slice is not done until:

- build passes cleanly
- relevant automated tests are added or updated
- tests pass
- QA review is complete
- visible UI changes have been reviewed for usability and wording
- no blocking issues remain
- user-facing messages are plain-language
- source-folder safety rules are preserved

## Current product expectations

Important constraints to preserve:

- source folder is configured once, but can be reconfigured from the app
- source library scanning is recursive
- device listing is recursive and mirrors the nested folder structure
- copy progress should be visible
- copy/delete actions should refresh device state appropriately
- selection state must update correctly from real UI interaction
- the UI must not get stuck in disabled or in-progress states after failures

## Review priorities

When reviewing work, prioritize:

1. safety and data integrity
2. correctness of copy/delete behavior
3. test coverage for real failure modes
4. state management and UI enable/disable behavior
5. wording clarity and child-friendliness
6. polish of visible UI

## Build and test

From the repo root, common commands are:

```bat
dotnet restore
dotnet build .\src\Mp3SyncManager.sln
dotnet test .\src\Mp3SyncManager.Tests\