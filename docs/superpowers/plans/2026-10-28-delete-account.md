# Delete Account Implementation Plan

> Execute inline using superpowers:executing-plans and test-driven-development. The user approved the preview and explicitly requested immediate implementation; no additional approval gate.

**Goal:** Delete the signed-in account and all device/cloud progress using the approved confirmation UI.
**Architecture:** A checkpointed deletion flow coordinates caller-only Cloud Save deletion, Firebase identity deletion, Unity identity deletion and local cleanup. A Resources dialog handles confirmation, busy, retry, success and startup recovery. Extend the existing Settings builder in place.
**Tech Stack:** Unity 6 uGUI/TMP, Firebase Auth 13.17.1, Unity Authentication/Cloud Code, .NET NUnit.
**Spec:** ../specs/2026-10-28-delete-account-design.md

## Global constraints
- Preserve existing uncommitted work; no commits, dependencies, environment changes or production deployment.
- Work in the current Unity project because its connected Editor owns the scene; save a scene copy first.
- No real user accounts or progress are deleted during verification.

## Review focus
- Cancellation/identity mismatch before deletion leaves all progress untouched.
- Network failure at each step preserves a checkpoint and cannot report success.
- Restart after Firebase deletion retries without signing up a replacement Firebase identity.
- Pending shop replies and fish OnDestroy must not recreate progress.
- English/Vietnamese and small screens retain readable, nonoverlapping actions.

## Task 1: Checkpointed deletion and caller-scoped cloud endpoint
- [ ] Add NUnit flow tests for ordering, cancellation, every failure/retry boundary and cleanup ordering; observe failure before implementation.
- [ ] Add `AccountDeletionFlow.cs`, linked into CloudCode tests, and `DeletePlayerData` using the caller's player ID; reject missing player IDs and ignore only not-found responses.
- [ ] Implement Firebase reauthentication/identity matching and Apple revocation, persist checkpoints, drain/block shop requests, clear device prefs after successful cloud/identity deletion.
- [ ] Verify .NET tests and Unity compilation; add focused local-cleanup/late-save checks.

## Task 2: Approved Settings UI and recovery
- [ ] Add `DeleteAccountDialog.cs` and its generated Resources prefab; wire cancel, progress, retry and success.
- [ ] Extend `MainAquariumScreenBuilder`/`MainAquariumScreen` with the signed-in-only row and grouped layout sizing. Recover pending deletions from StartGame before allowing play.
- [ ] Add English strings; apply the targeted builder to the open scene with a backup.
- [ ] Verify localization, persistent button actions, signed-in/guest visibility, no real deletions, and English/Vietnamese layout at phone sizes.

## Task 3: Review and delivery
- [ ] Run focused tests, inspect changes and request an independent review of the deletion path.
- [ ] Document Cloud Code deployment and physical-device verification requirements.

## Ledger
Planning: Firebase and Unity Authentication are separate identities; deleting Unity auth does not delete Cloud Save (official Unity docs). The current scene is MainAquarium, not playing, no unsaved scene edits. Preserve existing edits to ShopModule and its project file. CLI connectivity works outside the filesystem sandbox; CLI update check failed due unavailable DNS.
