# LNXcounter

**A Fortnite chest counter for Windows that works completely offline.**
It reads the replay files your own game already saves and counts **only the chests you open or break yourself**, match after match. It also tracks the **Treasure Hunters** mini-season drops, shows every **Treasure Hour** in Paris time, and has a small always-on-top overlay.

No login, no account, no data sent anywhere. Everything happens on your PC.

![Overview](screenshots/overview.png)

---

## Features

### 📦 Chest counter
- Total chests **opened** and **broken**, number of matches, and the average per match
- Breakdown by chest type (normal, rare, buried, relic, sprite…)
- List of your recent matches with the number of chests opened in each
- A new match is picked up automatically **about 8–20 seconds after it ends**, no need to restart or click anything
- Your progress is saved on your PC between launches

### ⏰ Treasure Hours (all times in Paris time)
- The next Treasure Hour with a **live countdown** (days : hours : minutes : seconds)
- The full list of past and upcoming windows with their boosts (Gold Outfit, Pirate Shop Access, Vehicle & Decal)
- Switches to **LIVE NOW** while a window is running, and past windows are dimmed
- Every time shown in the app (Treasure Hours, overlay, tray menu, match times) is converted to **Paris time**, whatever your PC's time zone is

### 🏴‍☠️ Drops: Treasure Hunters mini-season
Weeks 1 to 5, each with its own character, rewards and colors.

![Drops page](screenshots/drops.png)

- Every reward of the week as a card (outfit, pickaxe or back bling, vehicle decal, jam track, loading screen and the five gizmos), with the rarity colors
- Tick **"Mark as found"** on what you got. The **Pirate Shop Token** is the same every week, so ticking it once counts for all weeks
- The **week ends and the next one starts automatically**: a finished week turns dark and black-and-white, and the page jumps to the new week by itself
- A live **"ends in" countdown** for the current week

#### Drop odds and "could I already have it?"
Each reward has its official drop rate, and the app turns your own chest count into a chance of having already found it.

![Drop odds](screenshots/drop-odds.png)

Click any reward to see its details. For every item the app **calculates your possible odds of already having it**, based on how many chests you have opened in total:

![Pirate Shop Token](screenshots/token.png)

#### Loading screen preview
Click a loading screen reward and press **▶ PREVIEW** to see it full screen in 16:9, with the animated "connecting" dots and random Fortnite tips, just like in the game.

![Loading screen](screenshots/loading-screen.png)

#### Jam track preview 🎵
Click a jam track reward to **listen to its song** right inside the app, with play / pause, a progress bar you can drag, and a **volume slider** (your volume is remembered). There is also an **"Open on YouTube"** button if you want to open the song in your browser.

![Jam track](screenshots/jam-track.png)

### 🪟 Overlay and tray
- A small **overlay** you can keep on top of other windows: your total, this session's count, the last match and the next Treasure Hour
- Move it to any corner of the screen, or collapse it to just the total
- Closing the window **keeps the app running in the system tray**. Left-click the tray icon to reopen it, right-click for the menu
- Optional **start with Windows**

---

## Download and install

1. Go to the [**Releases**](../../releases) page and download **`LNXcounter.exe`**.
2. Run it. It is a single file and there is nothing to install.

> **Windows SmartScreen warning:** the app is not code-signed, so Windows may show *"Windows protected your PC"*. Click **More info → Run anyway**.

## Requirements

### Supported Windows versions

| System | Supported? |
| --- | --- |
| **Windows 11** (x64) | ✅ Yes. This is what the app is developed and tested on |
| **Windows 10** (x64), version 1607 or newer (22H2 recommended) | ✅ Yes, this is the oldest version the .NET 10 runtime it is built on supports |
| Windows 11 on ARM | ⚠️ Should work through Windows' built-in x64 emulation, but it is untested |
| Windows 7, 8, 8.1 and 32-bit Windows | ❌ No |
| macOS, Linux | ❌ No (it is a Windows-only WPF app, and Fortnite replays are read from the Windows install) |

- **Nothing to install:** the exe contains the .NET runtime, so you do **not** need to install .NET.
- **Fortnite replays on:** the game saves them by default in `%LOCALAPPDATA%\FortniteGame\Saved\Demos`. If yours are somewhere else, click **Folder…** in the app to choose the folder.
- **Jam track preview only:** the Microsoft Edge **WebView2 runtime**. It comes with Windows 11 and with most up-to-date Windows 10 installs. If it is missing, the app tells you and you can [download it from Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/). Everything else works without it.
- **Disk:** the exe is about 170 MB (it is not compressed, which is what keeps its RAM use low, see below).

## Light on your PC

LNXcounter is meant to sit in the tray while you play, so it is built to stay out of the way:

- **Starts in Windows' Efficiency mode:** the app marks itself as a background process every time it starts. It runs on the power-efficient cores at lower priority and never competes with Fortnite for the CPU. On Windows 11 you can see the Efficiency mode leaf next to it in Task Manager; on Windows 10 it applies too, just without the leaf icon.
- **Small RAM footprint:** about **15–45 MB** of working memory while it sits in the tray and about **40–70 MB** with the window open, instead of the ~290 MB it used before (measured in Task Manager's working set on Windows 11). It regularly gives unused memory back to Windows, especially when the window is hidden or after it has read a match.
- **The song preview cleans up after itself:** the jam track player runs in a stripped-down browser (no GPU process, a single renderer), and it is shut down completely about 20 seconds after the song stops, so it uses no memory when you are not listening. If you only pause, it is kept for 3 minutes and then released too.

## How it works

LNXcounter reads the `.replay` files that Fortnite saves after each match and looks at which chests *you* searched. Nothing is injected into the game and nothing is read from the game's memory, so it can't affect your account.

Because it works from replay files, a match is counted once the game has finished writing it. If a replay can't be read (for example after a game update changes the format), the status bar tells you.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build -c Release
```

To produce the single-file exe:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -o dist
```

Compression is turned off on purpose: a compressed single-file exe makes .NET unpack all its libraries into private memory, which costs about 70 MB more RAM.

Built with WPF on .NET, [FortniteReplayReader](https://github.com/Shiqan/FortniteReplayDecompressor) for reading replays and Microsoft WebView2 for the song preview.

## Credits and disclaimer

- Treasure Hours, drop rates and the weekly rewards come from [fortnite.bot/treasure](https://fortnite.bot/treasure) and from Epic Games' announcements.
- Jam track previews play the official YouTube videos through YouTube's own embedded player. Nothing is downloaded or copied.
- **LNXcounter is a fan-made tool. It is not affiliated with, endorsed by or sponsored by Epic Games.** Fortnite and all related names, images and logos are trademarks of Epic Games, Inc.
