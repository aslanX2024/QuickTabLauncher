# Contributing

Thanks for considering a contribution to QuickTabLauncher.

## Development Setup

1. Clone the repository.
2. Build from PowerShell:

```powershell
.\build.ps1
```

For a self-contained portable build:

```powershell
.\build.ps1 -SelfContained
```

## Guidelines

- Keep changes focused and small.
- Do not commit personal shortcuts, local notes, generated binaries, or machine-specific config.
- Use sample paths in docs and examples.
- Prefer clear behavior over large abstractions.

## Issues

When opening an issue, include:

- What you expected to happen
- What actually happened
- Windows version
- QuickTabLauncher version or commit
- Relevant `apps.json` or shortcut details, with private paths removed

## Pull Requests

Before opening a pull request:

- Build successfully.
- Update README or ROADMAP when behavior changes.
- Keep screenshots free of private paths, customer data, and personal links.
