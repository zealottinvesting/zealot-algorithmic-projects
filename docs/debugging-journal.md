# Debugging Journal: A Platform Level Data Integrity Bug

## Summary

A live strategy's secondary data series, the one used to detect a higher timeframe signal condition, quietly stopped receiving live updates while the strategy's own logs looked like everything was running fine. Four of six live strategies were affected. It didn't show up as bad trades, the affected condition just never fired, which made it invisible until it mattered. Finding it took a run through several wrong theories, and those wrong turns are the more useful part of this writeup.

## The symptom

A strategy's higher timeframe signal, computed on a series separate from its main chart timeframe, appeared frozen. The logs showed the same historical timestamp and price over and over, while the strategy's internal bar counter for that same series kept climbing normally, and the main timeframe kept showing correct, live, current data the whole time.

That combination, a counter that keeps moving next to data that never changes, turned out to be the actual clue. It just took a few dead ends to get there.

## What got ruled out, in order

**A corrupted compiled file.** First theory. Checked the file directly on disk. Clean, single copy, not the problem.

**A second duplicated file, same category.** Also checked directly and disproven.

**A dependency on having a chart open.** The theory was that a secondary series only streams live data reliably if a matching chart for that instrument and timeframe is also open somewhere. Opened the charts, checked again. No change.

**Stale state from staying open too long.** Theorized the running application was holding onto old subscription state across reloads. Tested with a full restart, confirmed through the process manager that it had actually closed, not just the window. No change.

**A stale local data cache.** Theorized the local historical cache was serving old data instead of pulling fresh history. Cleared it entirely, forced a full redownload. No change.

Four wrong guesses in a row, each one disproven with an actual test instead of just assumed away. That's the part worth keeping. Every one of those tests was cheap enough to run in minutes, so being wrong four times in a row still wasn't expensive.

## What it actually was

The real cause was how the trading platform handles two data series on the same instrument running under different trading hours settings. When they don't match, live updates only get processed inside the hours where both settings overlap. Anything outside that overlap gets queued instead of dropped, which explains the symptom exactly: the counter kept moving because events were genuinely firing, they were just resolving against a stale queued reference instead of anything fresh.

The affected strategies were requesting their secondary series without specifying that setting explicitly, which meant they fell back to a platform default that didn't match the main series. One missing setting, and it took five wrong theories to trace back to it.

## The fix

Two approaches, picked per strategy depending on what fit.

Where the timeframes divided evenly, the higher timeframe signal got rebuilt directly from the main series' own bars, since that series was already confirmed to be reliably live. That sidesteps the platform behavior entirely instead of depending on it.

Where a trading hours setting could be attached to the secondary series directly, that became the more direct fix, but only after confirming with a small throwaway check that the value it depended on actually resolved correctly at that point in the strategy's lifecycle, before committing to the full change.

Both fixes were confirmed the same way, checking that the previously frozen timestamps started moving with real, current data, not just trusting that a clean compile meant it worked.

## What this taught me

A counter that keeps climbing isn't proof the data behind it is real. That distinction was the actual key here, and it's easy to miss if you only check whether something is still running instead of whether it's still changing.

Testing a wrong idea cheaply beats holding onto an unfalsifiable one. Five wrong theories, each one killed off with a real test, got to the answer faster than staying attached to any single guess would have.

Sometimes the fix for a platform quirk you can't fully explain is to design around it. Rebuilding the timeframe synthetically didn't require understanding why the platform behaves that way, only knowing which source was actually trustworthy.

---

# Later incidents (November 2026)

The first week of the current basket produced three more problems. None of them showed up as an error message. Each one was found by checking what the system actually did against what it should have done.

## A new account that never traded

**Symptom.** A newly purchased evaluation account sat idle while the same three strategies traded on four other accounts. The strategies were enabled, connected and printing normally.

**Investigation.** The strategy logs only said entries were blocked, which pointed at the shared risk guard. Instead of reading the guard's code for a theory, I read its saved state: the per-account file where it persists the trailing drawdown floor. The new $50,000 account had a floor of $50,100, locked. That's the floor of an older account that had passed long ago.

**Cause.** When no saved file exists, the guard seeds the floor from a table of known accounts, and the table matched on the account type prefix. Both accounts shared it. The new account got the old account's floor, the guard calculated zero room, so the daily stop was $0 and every entry was refused.

**Fix.** Match on the full account id, and make the default branch the safe one: an unknown account starts as a fresh account. The bad floor was already on disk, so it was backed up and deleted, and because the guard caches floors in static memory the platform had to be fully restarted, not just recompiled. After the restart the file came back as a fresh $48,000 floor, and the next morning the account took the same Nasdaq trade as the others. ([code](../code-examples/guards/account-seed-prefix-bug.cs))

**Lesson.** A default that silently copies another account's state is worse than one that fails loudly. When the logs only describe a symptom, read the state the code is acting on.

## A position that lost its stop

**Symptom.** One account was left holding a gold position with no stop, closed by hand for -$896. The strategy's own stop would have capped it at -$276.

**Cause.** Two mistakes stacked. The strategies had been attached to the wrong instrument's charts on some accounts, and while fixing that, a strategy was disabled and re-enabled while it held a position. Disabling a managed strategy cancels its own stop and target orders. The position stayed open; the protection didn't.

**Fix.** Each strategy now throws on startup if it's attached to the wrong instrument, and checks the account (not its own internal view) on every bar for any open position on its instrument without a working stop, raising a sound alert if it finds one. Plus a standing rule: never disable or enable a strategy while it's in a trade. ([code](../code-examples/guards/unprotected-position-check.cs))

## The platform crashing on network drops

**Symptom.** Twice in one week the trading platform simply wasn't running. Once, a 4:00 PM flatten never happened. Another time the platform was down from 7:05 AM to 2:55 PM and nobody noticed, so a whole New York session was missed.

**Investigation.** The platform's own log stops mid-line when it crashes, so the evidence had to come from outside it. The Windows Application log had the crash reports, and one included a .NET stack trace: an `ObjectDisposedException` inside the platform's broker adapter, in its market data disconnect handler. The platform was crashing while handling a dropped connection, so the next question was why the connection kept dropping.

The Wi-Fi event log lined up with it: 110 reconnects in 14 days, two of them "disconnected by the driver". The platform lost its connection at 3:59 PM, crashed at 4:01, and Windows logged the driver disconnect at 4:02, so the network was failing right through that window. The adapter is a Realtek card model known for dropouts, running a two-year-old driver with its own internal power saving enabled. That setting is separate from the Windows power and battery settings that had already been turned off.

The second crash did not fit. It had a different exception code (a stack buffer overrun), no stack trace, and no network drop around it in any log. Its cause is still open, and it is written down as open rather than folded into the Wi-Fi story because the story was convenient.

The same pass ruled things out. Twelve unclean reboots in a week looked alarming, but the event data showed none were blue-screen crashes (bug check code 0). Five were the power button held down, and the battery report matched most of the rest to the laptop running flat. Those were known and already fixed, so they were set aside, not mixed into the network problem.

**Fix.** The card's internal power saving was turned down and Windows was stopped from powering the adapter off, scheduled for one minute after the last trading window closed so it couldn't drop a live connection. The permanent fix is hardware: replacing the card with an Intel model or running a cable to the router. The crash itself is in the vendor's code, so the defense there is a platform update plus a watchdog that alerts when the platform isn't running.

**Lesson.** When the application can't tell you why it died, the operating system usually can. And separate the problems before fixing any of them: the reboots, the Wi-Fi drops and the platform crash looked like one issue, and were three.
