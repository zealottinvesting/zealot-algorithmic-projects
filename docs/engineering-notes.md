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

## Stack note

This validation pass was worked through collaboratively with Claude Code, used as a research and debugging partner with direct access to the project's files rather than as a code generator working from a description. The process throughout stayed the same as everywhere else in this repo: don't trust a result until it's been checked against something independent, and treat a clean or convenient looking pattern as a reason to look closer, not a reason to stop looking.
