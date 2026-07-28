# AzDO PR Monitor

A terminal UI app that keeps an eye on your outstanding Azure DevOps pull requests, so you don't have to keep a browser tab open.

## What it does

- Scans every project in an Azure DevOps organization for **active pull requests you created**.
- Auto-refreshes every 5 minutes in the background; manual refresh is one keystroke away.
- Shows approval status per reviewer (approved / approved-with-suggestions / waiting / rejected) and a computed **"can this be completed?"** verdict, based on required-reviewer votes, draft state, and merge status.
- Highlights rows that are ready to complete (black-on-bright-green).
- Lets you view a PR's comment threads inline, without leaving the terminal.
- Opens a PR in your default browser with one keystroke.

It does **not** call Azure DevOps's branch-policy/build-validation API, so "can complete" is a heuristic (required reviewers + merge conflicts + draft state), not the full policy engine AzDO itself evaluates.

## Requirements

- .NET SDK (net10.0 or compatible)
- An Azure DevOps Personal Access Token (PAT) with **Code (Read)** scope

## Setup

```bash
export AZDO_ORG="your-org"      # defaults to "rr-wfm" if unset
export AZDO_PAT="your-pat-here"
dotnet run
```

Create a PAT at `https://dev.azure.com/{org}/_usersSettings/tokens`.

## Keyboard shortcuts

| Key | Action |
|---|---|
| `↑`/`↓` | Move selection |
| `Enter` or `Alt+C` | View comments for the selected PR |
| `Alt+O` | Open the selected PR in your default browser |
| `Alt+R` | Refresh now |
| `Alt+Q` or `Esc` | Quit |

> On macOS, `Alt`/`Option` shortcuts may need "Use Option as Meta key" enabled in your terminal's preferences (Terminal.app: Settings → Profiles → Keyboard; iTerm2: Preferences → Profiles → Keys → Left/Right Option key acts as Esc+). Otherwise Option+letter types an accented character instead of sending the shortcut.

## How it works

- **`Program.cs`** — reads `AZDO_ORG`/`AZDO_PAT`, resolves the authenticated user's identity via the AzDO `connectionData` API, and starts the TUI.
- **`src/AzureDevOpsClient.cs`** — thin REST client (project listing, per-project PR search, comment threads) authenticated with a PAT over Basic auth.
- **`src/PrDataService.cs`** — fans out across every project in the org (AzDO has no single org-wide PR search endpoint) and merges the results. Capped at 8 concurrent project queries.
- **`src/Formatting.cs`** — turns raw PR data into list rows, column headers, the details-pane text, and comment-thread text; also computes the "can complete" heuristic.
- **`src/PrMonitorApp.cs`** — the Terminal.Gui (v2) UI: the PR list, the details pane below it, keybindings, the 5-minute polling loop, and the comments dialog.
- **`src/Models.cs`** — JSON DTOs for the Azure DevOps REST responses.

Built with [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) v2.
