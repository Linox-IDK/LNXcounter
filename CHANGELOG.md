# Changelog

Every update of LNXcounter, newest first. Download any version from the [Releases](../../releases) page.

## v1.20.0
**Records, goals and a chest map**
- Chests smashed by your car (or an explosion) are now counted like opened ones: the replay names who destroyed them.
- Treasure gizmos are counted from the moment they are given to you, even if you never equip them (the name shows when you do).
- **Chest map:** every match in *Recent matches* has a small **eye**. Click it to see the **map** with every chest you opened (or broke) in that match, drawn with its own icon (normal, rare, buried, relic, sprite). Hover one to see what it was and when. A chest is drawn where it is in the game; when the replay does not store its place, it is drawn where you stood when you opened it.
- **Records:** a gold **BEST MATCH** line above the matches list, with your **best day** and your **streak** (days in a row with at least one chest opened).
- **Goal (optional):** *Settings > Goal* lets you set a target **per day** or **per week**. A progress bar shows above your matches and says when you reached it. Off by default.
- **Treasure Hour count:** under the main counter, in small gold: how many of your chests were opened **during a Treasure Hour**.
- **Tab slide:** moving between *Overview*, *Drops* and *Settings* now slides and fades the pages instead of switching instantly.
- Matches counted by an earlier version get their map positions added once, while their replay is still on your PC. Their counts do not change.

## v1.10.0
**Treasure Hunters rewards read from your replays**
- When a chest gives a reward, the game puts a **golden chest** in the match. LNXcounter now reads it: a reward **you** got is **ticked as found** on the Drops page by itself (Pirate Shop Access, and the weekly outfit, pickaxe or back bling, vehicle decal, jam track and loading screen of weeks 1 and 2; weeks 3-5 are added when their ids are known).
- A match with a treasure gets a **★** in *Recent matches*. Hover it to see what was found and when. Rewards found by **other players near you** are listed there too (☆).
- **Gizmos** you equip in a match are listed the same way (a gizmo you never equip is not written in the replay).
- Matches counted by an earlier version are read again once, while their replay is still on your PC.
- Only what happens near you is recorded by the game.
- The idea was inspired by LED-N's Fortnite-ChestCounter (credited in the README); the code is LNXcounter's own.

**Also in this version**
- **Treasure Hour odds:** while a Treasure Hour is on, the drop rates on the Drops page are multiplied by the boost (outfit, Pirate Shop Access, vehicle), shown in **lucky green** with a *TREASURE HOUR* badge. The normal rate stays in the details popup.
- **Export to CSV or PDF:** *Settings > Export* saves your chests per **day**, **week** (Monday to Sunday) or **month**. Pick **CSV** (Excel / Google Sheets, with chests by type) or **PDF** (a black and gold report with totals, a bar per period and the treasures you found).
- **Treasure Hour alert:** when a Treasure Hour starts, a pop-up slides in at the **top right** of the screen with a short chime, then slides out by itself (click it to close). Can be switched off in *Settings*, with a *Test* button.
- **Warnings before the hour:** a silent pop-up **30 minutes** before and one with the chime **10 minutes** before. Separate switch in *Settings*.
- **Backup and restore:** *Settings > Backup and restore* saves all your stats (matches, chests added by hand, ticked drops) to one file and loads it back, for example on a new PC. Restoring keeps the old stats as `state.json.bak` and restarts the app.
- **Updates:** the app checks GitHub for a newer version when it starts and every 6 hours (can be switched off). When there is one, a **red dot** appears on the *SETTINGS* tab and the *Updates* card says *"New update available vX.Y.Z"* with an **Update now** button that downloads it and restarts. *Check now* checks by hand.
- **Your language:** the app follows the language of Windows: **French, Spanish and German** for now, English for anything else. Dates and times use your Windows format too.
- **Your Fortnite name** is shown next to the app name. Replays only contain Epic account ids, so it is read from Fortnite's own log on your PC; nothing is sent anywhere.
- **Already-read replays are never read again** (only *Rescan all* does that), so old matches do not get the treasure markers retroactively.
- **Cleaner look:** every outline is now gold and every separator black, and the hover tooltips (for example on the treasure star) have the same dark and gold style.
- A small optional **Support on Ko-fi** button at the bottom of *Settings*.
- **`LNXcounter.exe -up`** in a terminal opened in the exe folder updates the app from the command line.
- **Start with Windows:** a checkbox in *Settings* (also in the tray icon and overlay right-click menus) opens the app quietly in the tray at login.

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
