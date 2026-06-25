# ScreenTime — Jellyfin Plugin

[![Build](https://github.com/atcen/jellyfin-plugin-screentime/actions/workflows/build.yml/badge.svg)](https://github.com/atcen/jellyfin-plugin-screentime/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Jellyfin 10.11](https://img.shields.io/badge/Jellyfin-10.11%2B-00A4DC.svg)](https://jellyfin.org)

A server-side **screen-time / parental-control** plugin for [Jellyfin](https://jellyfin.org).
Set a **daily watch-time limit per user** — when a child runs out of time, the currently
playing episode is allowed to **finish**, but **no new playback can be started** until the
daily reset.

No external app, no polling, no extra reporting plugin required. ScreenTime listens directly
to Jellyfin's playback events.

---

## Why another one?

The popular [jelly-watch-wise](https://github.com/Joker-KP/jelly-watch-wise) is a great idea,
but it runs as a **separate app** that polls the API and needs the *Playback Reporting* plugin
as a data source. Its enforcement revokes a user's library access.

ScreenTime takes a different approach:

| | jelly-watch-wise | **ScreenTime** |
|---|---|---|
| Runs as | external Python app | **native Jellyfin plugin** |
| Data source | Playback Reporting plugin / Jellystat | **direct playback events** |
| Configuration | separate YAML / GUI | **Jellyfin dashboard** |
| Default enforcement | revoke library access | **fair soft block** (finish current, block next) |

## How it works

ScreenTime runs a lightweight background service inside Jellyfin:

1. **Tracking** — every minute, each user with an active (non-paused) playback gets one
   minute added to their daily total. Multiple parallel sessions of the same user count once
   (real screen time, not summed streams).
2. **Soft block (default)** — once the limit is reached, the **running item keeps playing to
   the end**. The moment a **new** playback starts — whether the user clicks something or
   *Auto-Play Next Episode* fires — it is stopped immediately with a friendly message.
   Nobody gets yanked out of the middle of an episode.
3. **Hard stop (optional)** — if you really need it, a toggle stops the running item as soon
   as the limit is hit.
4. **Daily reset** — counts reset at a configurable hour (midnight by default). The counting
   day is derived from the reset hour, so no scheduled task can get "stuck".

State is persisted to `watchtime.json` in the plugin's data folder, so a server restart
doesn't wipe the day's progress.

## Features

- ⏱️ Daily watch-time limit **per user** (0 = no limit)
- 🧒 **Fair soft block** — finish the current episode, block the next one
- 🔨 Optional **hard stop**
- 🕛 Configurable **daily reset hour**
- 💬 Customizable **on-screen message** when blocked
- 🧩 Configured entirely in the **Jellyfin dashboard** (own sidebar entry)
- 🪶 No external services, no extra reporting plugin

## Requirements

- Jellyfin **10.11** or newer
- (for building) **.NET 9 SDK**

## Installation

### Via plugin catalog (recommended)

1. In Jellyfin, go to **Dashboard → Plugins → Catalog**.
2. Click the gear icon next to **Repositories** and add a new one:
   - **Repository name:** `ScreenTime`
   - **Repository URL:**
     `https://raw.githubusercontent.com/atcen/jellyfin-plugin-screentime/refs/heads/main/manifest.json`
3. Click **Save**.
4. Back in the **Catalog**, find **ScreenTime** and click **Install**.
5. Restart Jellyfin.
6. Configure under **Dashboard → ScreenTime**.

### Manual

1. Download `Jellyfin.Plugin.ScreenTime.dll` and `meta.json` from the
   [latest release](https://github.com/atcen/jellyfin-plugin-screentime/releases).
2. Create a folder named `ScreenTime_1.0.0.0` inside your Jellyfin plugins directory:
   - Docker (linuxserver / official): `/config/data/plugins/`
   - Native Linux: `/var/lib/jellyfin/plugins/`
3. Copy both files into that folder.
4. Restart Jellyfin.
5. Open **Dashboard → ScreenTime** to configure.

### Build from source

```bash
git clone https://github.com/atcen/jellyfin-plugin-screentime.git
cd jellyfin-plugin-screentime
dotnet build -c Release
# DLL: bin/Release/net9.0/Jellyfin.Plugin.ScreenTime.dll
```

## Configuration

Open **Dashboard → ScreenTime** (sidebar) and set:

| Option | Description |
|---|---|
| **Enable tracking & limits** | Global on/off switch. |
| **Hard stop** | Off (recommended) = soft block. On = stop the running item too. |
| **Reset hour (0–23)** | When the daily count resets. `0` = midnight. |
| **Message – title / text** | What the user sees when blocked. |
| **Daily limits per user** | Minutes per day for each account. `0` = no limit. |

Set a limit (e.g. `60`) only on the children's accounts; leave admins/adults at `0`.

## How the limit behaves (example)

> Child has a 60-minute limit and is watching episode 5.
> - Minute 60 is reached mid-episode → **episode 5 keeps playing to the end**.
> - Episode 5 ends → Auto-Play tries episode 6 → **blocked** with the message.
> - Child manually opens a movie → **blocked** with the message.
> - Next day after the reset hour → counter is `0`, everything works again.

## Limitations

- A server plugin can **stop** a new playback but cannot prevent the click itself — the new
  item starts for ~1–2 seconds before it is stopped and the message appears.
- Time is counted in whole minutes (1-minute resolution).
- Library-lock enforcement (hiding libraries instead of stopping playback) is **not** included
  by design; the soft block already prevents new playback.

## License

[MIT](LICENSE)

## Acknowledgements

Inspired by [jelly-watch-wise](https://github.com/Joker-KP/jelly-watch-wise) and built on the
excellent [Jellyfin](https://jellyfin.org) plugin platform.
