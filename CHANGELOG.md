# Changelog

Every update of LNXcounter, newest first. Download any version from the [Releases](../../releases) page.

## v1.9.0
**Add chests that the replays can't see**
- New **SETTINGS** tab.
- **Add chests to your count:** type a number and press *Add* (or *Remove* to take some back). Use it for chests opened on another device, or before you installed the app. They are counted as **normal chests**.
- **Set your exact total:** type the total you know and the app adds whatever its replays did not count to reach it.
- Chests added by hand also count for the drop odds ("chance you would have found it by now"), the overlay and the tray tooltip. The match average is not affected.
- The big number on the Overview shows a note such as *"counted from your replays + 250 added by hand"*.
- They are kept when you press *Rescan all*, and cleared by *Reset*.

## v1.8.0
**Much lighter on your PC**
- Starts in Windows **Efficiency mode** every time (lower priority, power-efficient cores, never competes with Fortnite).
- RAM use down from about 290 MB to roughly **20 MB in the tray** and 40-70 MB with the window open. Unused memory is given back to Windows at startup, when the window is hidden or shown, after a match is read, and every 30 seconds while hidden.
- The jam track player runs in a stripped-down browser and is shut down completely about 20 seconds after the song stops (3 minutes after a pause).
- The exe is no longer compressed: about 70 MB less memory, but the file is about 170 MB.
- Fixed a garbled character in the tray tooltip.
- README: supported Windows versions (Windows 10 64-bit 1607+ and Windows 11).

## v1.7.0
**Treasure Hours frame**
- One frame on the Overview with the yellow token tile (the token floats up and down), a **NEXT TREASURE HOUR** badge, a live countdown and the date.
- Below it the **full list** of Treasure Hours with their boosts (Gold Outfit, Pirate Shop Access, Vehicle & Decal) and a status: next, upcoming or ended. A window shows **LIVE NOW** while it is running.
- Hours taken from fortnite.bot/treasure.
- **Every time in the app is now Paris time** (Treasure Hours, overlay, tray menu, match times), whatever the PC's time zone is.

## v1.6.0
- **Jam track preview:** the jam track rewards play their song inside the app (official YouTube video through YouTube's embedded player), with play/pause, a draggable progress bar and a **volume slider** that is remembered. An *Open on YouTube* button is there too.
- **Faster replay detection:** the app now checks every 2 seconds and counts a match about 8-20 seconds after it ends. A replay that grows after being read is read again, and failed reads are retried.
- **Automatic week change:** when week N+1 starts, week N instantly turns dark and black-and-white and the page moves to the new week by itself.

## v1.5.1
- New hero icons for weeks 3, 4 and 5, and Week 4's text is blue.
- The loading screen preview is now 16:9.
- Ended weeks and weeks that have not started yet look the same: dark, black-and-white hero, grey title.

## v1.5.0
- Smooth **flowing gradient** animation on the reward frames and popup.
- Weeks 1-5 are clickable and each shows its own rewards, like on fortnite.bot.
- **Click a reward** to open a details popup (type, tier, drop rate, your chance of already having it).
- **Loading screen preview:** see the loading screen full screen with random Fortnite tips and the animated "connecting" dots.

## v1.4.0
- Rewards you marked as found get a black-and-gold gradient case.
- Rewards of an ended week are shadowed, and its week icon turns black-and-white.
- The **Pirate Shop Token** is the same every week: ticking it once counts for all weeks.
- Icons and week names updated from fortnite.bot/treasure.
- The yellow drop-odds marker pulses, and the Epic Games drop-rate notice appears at the top of the odds list.

## v1.3.0
- Reward frames redesigned like the website, each with its **drop chance in %**.
- The token has its yellow/gold frame and shows **CONSUMABLE / PIRATE SHOP TOKEN**.
- Real images for the week 1 and week 2 rewards and week icons.
- **Hover** a reward: the image grows a little and a shaded text with its info appears.

## v1.2.0
- Chest-type icons: buried, rare, special (sprite and relic chests) and a "+" for other chests.
- **Drops page** with week 1-5 tabs.
- Reward tiles with the rates as "1 in N" and the percentage.
- The Pirate Shop Token floats inside its frame.

## v1.0 - v1.1 (first versions)
- Reads your Fortnite replay files **offline** (nothing is read from the game's memory) and counts only the chests **you** open or break, match by match.
- Gold and black theme, with a main window (totals, recent matches, chest types) and a small **overlay**.
- Save system: your progress is kept between launches.
- **Treasure Hours** (the next window only).
- Thin gold scrollbar, custom chest icon.
- Closes to the **system tray**: left-click reopens it, right-click opens the menu (also on the overlay).
