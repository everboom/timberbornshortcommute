# tools

Developer helper scripts (not shipped with the mod).

| file | purpose |
|---|---|
| `copy-player-log.ps1` | Copy Timberborn's `Player.log` / `Player-prev.log` from `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\` into the repo's gitignored `dump/` folder (fixed names), so the latest log is always at `dump/Player.log` for inspection. Run after a crash or unexpected in-game behaviour. |

Usage:

```pwsh
.\tools\copy-player-log.ps1            # -> dump\Player.log, dump\Player-prev.log
.\tools\copy-player-log.ps1 -DestDir C:\tmp\timberlogs
```
