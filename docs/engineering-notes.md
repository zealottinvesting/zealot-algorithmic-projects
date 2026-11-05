# Statistical Validation & Risk Engineering: A Bug Catalog

## Why this exists

The other writeups in this repo describe what got built. This one describes how it got tested, where it broke, and what caught each break. Some of that testing happened in direct collaboration with Claude, working through a real validation problem in real time rather than treating an AI assistant as an autocomplete for code. The second half of this file is a running catalog of the significant bugs found across this project, so the scope of what's been caught is visible in one place instead of scattered across separate writeups.

## Thin samples and a rolling window

The basket's out of sample validation samples were small, low double digits for several strategies. Small enough that a strategy's measured win rate could plausibly be noise rather than a real edge. That's not a fixable bug on its own, it's a limitation of a single fixed train/test split on a young dataset, and the honest response to it is to get more out of sample evidence, not to trust the number harder.

Getting there meant pulling roughly two years of minute level history per instrument. The platform's continuous contract symbol turned out to be unsupported on this data feed entirely, not a settings problem, a hard limitation. The workaround was pulling each individual back month contract separately and stitching them into one continuous, price adjusted series by hand, correcting for the price gap at every contract rollover so the series has no artificial jump at any seam.

With that longer history in place, the validation approach shifted from one fixed split to a rolling window: repeatedly training on one stretch of time and testing on the stretch immediately after it, sliding that pair forward across the full two years in six overlapping windows. Instead of one out of sample number per strategy, this produces six, one per window, which makes it possible to see whether a strategy's edge holds up consistently across different stretches of market history or whether it only worked in one particular period.

### The bug the rolling window surfaced

Early results from the rolling window run looked suspicious in a specific way: three of the six strategies, all built around one type of signal condition, were consistently losing, and the other three, built around a different type, were consistently winning, split too cleanly to be a real, independent finding. A clean split like that is usually a sign of something systematic rather than six unrelated edges happening to sort themselves by category.

The actual cause was in how bars got labeled during resampling to a higher timeframe. The resampling logic was labeling each aggregated bar by its opening timestamp instead of its closing timestamp, so a bar the code called "closed at time X" had, in reality, closed several minutes earlier than that. Once that mislabeled bar got forward filled onto the finer timeframe used for entries, the effect was that a signal became visible earlier than it could have actually been seen live, a look ahead leak. It affected every strategy's signal timing, not just the ones on one side of the split, which is exactly why fixing it moved every strategy's numbers, not only the ones that had looked artificially bad.

The scale of the correction varied a lot by strategy. One affected strategy's total backtested profitability dropped by roughly 64 percent once the leak was fixed, moving from what had looked like a strong result to a merely solid one. Another dropped by roughly 99 percent, from a result that looked strongly profitable to something close to breakeven, the clearest sign that its apparent edge had mostly been a timing artifact rather than a real one.

Confirming the fix meant more than rerunning the numbers and checking that they moved. It meant tracing a specific historical signal by hand against the corrected bar boundaries and confirming the timestamp lined up with when that condition would have actually been visible live, not just trusting that the new output looked more reasonable.

### A second, smaller finding from the same pass

While validating the corrected results against the actual deployed strategy files, several of the strategy configurations used for backtesting had quietly drifted from what was actually live. Two strategies in the list weren't in the live basket at all anymore, replaced earlier by different ones that the backtest config had never been updated to reflect. A few parameter values on strategies that were still live had also drifted from what the deployed code was actually running.

None of this was found by staring at the config and guessing something was wrong with it. It was found by deciding not to trust the config's numbers at all and checking each one directly against the live strategy files line by line, the same discipline as the frozen data series bug below: a value sitting there looking plausible is not the same as a value that's actually been checked.

That discipline caught something small but real even after the rewrite: one strategy's backtest config still carried an old target distance that no longer matched what the live deployed file was actually running, a leftover from an update earlier in the same pass that either hadn't fully landed or got overwritten by a later rewrite pulling from a different snapshot of the live file. It was only caught by diffing every field directly against the live code again, rather than assuming a recent rewrite meant everything in it was already current.

Separately, a few of the live strategies had a small, low stakes version of the same category of problem: the human readable log line printed when a trade fires didn't match the quantity the order was actually placed with. The order itself was correct, only the log text describing it afterward was stale. Harmless on its own, but a reminder that a log message is a claim about what happened, not a guarantee, and it's worth checking the actual order call rather than trusting the print statement next to it. This one is still open across most of the affected strategies, caught but not yet cleaned up.

### What came out of it

Two years of rolling window testing, once corrected, showed a clear split in the basket: some strategies held up consistently across every window tested, some had a real, measurable edge that was structurally too small to capture within the shape of the strategy as originally sized, and some had thin enough edge that it didn't survive honest testing at all. That last group made the case for reducing the live basket, a decision the walk-forward evidence supports even though it hasn't been carried out against the deployed strategies yet. That's a better outcome than continuing to run strategies on the strength of a small sample that happened to look good the first time it was measured.

For one of the strategies with a structural sizing problem rather than a missing edge, the fix came from directly measuring how far price actually tends to travel within the available trading window, rather than guessing at a smaller target. The adjusted version was then re-tested with the same rolling window process used everywhere else, not just checked against a single pass of the same data it was tuned on, since tuning a parameter and testing it against the same data it was tuned on doesn't prove anything on its own.

## Bug catalog

A short index of the significant bugs found across this project, what layer each one lived in, and where to read more.

| Bug | Layer | Status |
|---|---|---|
| Frozen secondary data series on higher timeframe signals | Live strategy platform layer | Full writeup: [debugging journal](./debugging-journal.md) |
| Signal formula drift between validated research and deployed strategy code | Live strategy code | Found and corrected during live deployment hardening |
| Gate window sizing mismatches against validated research values | Live strategy code | Found and corrected during live deployment hardening |
| Crossover style entry conditions missing valid signals already past threshold when a gate opened | Live strategy code | Corrected across all live strategies, switched to threshold based confirmation |
| Look ahead leak from bars labeled by open time instead of close time during resampling | Validation / backtesting engine | Found and corrected above |
| Backtest strategy configuration silently drifted from live deployed strategy code | Validation / backtesting engine | Found and corrected above |
| Trade log text describing an order not matching the order actually placed | Live strategy code | Found above, cosmetic, fix pending |
| Opening range and premarket levels used before they existed (52% of one strategy's trades) | Validation / backtesting engine | Fixed in v4; every stage now checked for future data |
| Research data stored in UTC but treated as Eastern time | Data layer | Fixed in v4; all prior two year research rerun |
| Scale-in adds credited against one contract of risk, inflating returns to a fake 25R per month | Exit research | Fixed: returns normalised by mean peak risk |
| Walk forward folds graded on a payout score that saturates at 100, so ties counted as losses | Exit research | Fixed: folds graded on R, payout checked separately |
| Folds with a negative in-sample base silently skipped (2 of 4 folds dropped) | Exit research | Fixed: anchored walk forward, every fold kept |
| Monthly jobs report dates assumed to be the first Friday (wrong about 10 times in two years) | News research | Fixed: official BLS and Fed calendars |
| Built-in Bollinger Bands use population std, research used sample std | C# port | Matched in code; replica parity check |
| Stray weekend bars in research data created a fake trading day and shifted pivots | Data layer | Found by the replica parity check; live code was correct |
| New account inherited another account's locked drawdown floor via a name prefix match | Live risk guard | Fixed: [code](../code-examples/guards/account-seed-prefix-bug.cs) |
| Strategy disabled mid-trade cancelled its own stop, leaving a naked position (-$896 vs -$276) | Live operations | Fixed: [code](../code-examples/guards/unprotected-position-check.cs) |
| Regime filter failed open on short history and traded unfiltered | Live strategy code | Fixed: fails closed |

## Edge or luck? (November 2026)

Every earlier fix in this file made the backtests more honest, but they all answered the question "is this backtest computed correctly?" The October basket was computed correctly and still lost money live. The question it hadn't answered was "would a search this big have found something this good on data with no edge in it?"

The test for that is to run the entire search again, end to end, on data where the signal timing has been shuffled, and compare the winners of the real search with the winners of the shuffled one. Comparing one candidate against random entries isn't enough, because the candidate was chosen as the best of thousands. It has to be compared with the best of thousands of nothing. A toy version is in [same-search-null-test.py](../code-examples/validation-bugs/same-search-null-test.py).

Two lessons came out of building it:
- **Drift fools long strategies.** Stock index and gold futures went up overnight for most of the data. A long rule that does nothing but buy in the evening "passes" a normal test. A drift-free version, where signals are computed on real prices but P&L is computed with each hour's average move removed, separated real long signals from passengers. Most longs turned out to be passengers.
- **The first version of the drift removal was wrong in the other direction.** A rolling 60 day estimate over-corrected, turning random gold longs from +9.8 to -9.1 ticks, which tilted the test against longs. Replacing it with a per-month, per-hour estimate fixed that. A test that is unfair against your hypothesis is as broken as one that is unfair for it.

## Exit research: exhaustive, and mostly rejected

The rule for exits and risk management was that nothing gets hand picked. Every layer (breakeven, partial exits, trailing stops, time stops, scale-ins, regime filters, circuit breakers, news skips) is swept over a full grid, selected only inside walk forward, smoothed so a single lucky grid cell can't win, and then put through its own luck test.

Almost everything failed. The in-sample choice was worse out of sample 70 to 94% of the time, depending on the layer. The per-fold detail explained why: in a bad in-sample stretch the optimizer reaches for extreme settings (very tight breakeven, very short time stops) that rank last out of sample. The strategies' edge also builds slowly, up to three hours after entry, so early exits chop the winners.

That was the useful result. It meant the live strategies run the simplest version that survived, and the few management rules that are live were each chosen against a written scoring rule, with near misses shown next to their cost in blown accounts so the decision to accept or reject them was an informed one.

## Stack note

This validation pass was worked through collaboratively with Claude Code, used as a research and debugging partner with direct access to the project's files rather than as a code generator working from a description. The process throughout stayed the same as everywhere else in this repo: don't trust a result until it's been checked against something independent, and treat a clean or convenient looking pattern as a reason to look closer, not a reason to stop looking.
