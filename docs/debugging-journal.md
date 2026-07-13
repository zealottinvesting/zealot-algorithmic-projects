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
