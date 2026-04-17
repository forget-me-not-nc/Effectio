# Contributing

Effectio is a small, single-maintainer library. PRs and issues are
welcome.

## Build and test

```
dotnet restore
dotnet build -c Release
dotnet test
```

All 78 tests should pass on a clean checkout. New features need new
tests in `Effectio.Tests/`, mirroring the source folder layout.

To run the Unity sample, open `samples/UnityDemo/` in Unity Hub
(2022.3 LTS recommended). The library is referenced by local `file:`
path so library edits show up in Unity after one recompile.

## Filing an issue

- **Bug** - include the Effectio version, target framework, and a
  minimal repro snippet (a failing test against `Effectio.Tests/` is
  ideal).
- **Feature** - check `docs/ROADMAP.md` first; it is the canonical
  list of planned work. If your idea fits an existing milestone,
  reference its task number in the issue.

## Branches

| Branch | Purpose |
|---|---|
| `main` | Last released library code plus docs, samples, and build infrastructure. Tagged `vX.Y.0` whenever a milestone ships. |
| `release/1.0.x` | Maintenance line for the v1.0 series. Only receives `fix/*` PRs. |
| `release/<next>.0` | Integration branch for the next milestone (currently `release/1.1.0`). All `feat/*` PRs target this branch. |
| `feat/<short-name>` | One per roadmap task, branched off the active integration branch. |
| `fix/<short-name>` | One per bug, branched off `release/1.0.x` or `main` (for docs-only fixes). |

## Pull requests

- One PR per task or fix.
- Reference the relevant `docs/ROADMAP.md` task or issue number in the
  PR description.
- Commit messages: imperative mood, lowercase subject, optional
  body wrapped at ~72 cols. Example:

  ```
  feat(reactions): support stack-aware require/consume rules

  Adds RequireStacks(key, min) and ConsumesStacks(key, count) to
  ReactionBuilder, plus integration tests covering ...
  ```
- `feat/*` and `fix/*` PRs are squash-merged into their target branch.
- `release/*` -> `main` PRs use a merge commit so the integration
  history is preserved.
