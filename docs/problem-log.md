# Problem Log

Nineteen real problems found while building, testing and running the Zealot futures basket, from the first backtests in spring 2026 to the live basket in November 2026. Each one lists how it showed up, what was actually wrong, how it was fixed and what changed because of it.

The pattern across all of them: a number looked off, I refused to trust it, and I traced it back to a cause I could prove before changing anything.

---

## 1. Backtest timestamps were five hours off

**How it showed up:** NinjaTrader found 9 trades in five months on a strategy the Python backtest said should have taken 51 to 126.

**Root cause:** NinjaTrader exports bar timestamps in UTC, and the backtest engine was reading them as New York time. Every session level (London low, premarket low) was being built 4 to 5 hours off, so the backtest was trading setups that never existed.

**Fix:** Rebuilt the data pipeline to convert every timestamp from UTC to Eastern time, then stitched individual monthly contracts into clean files for six markets.

**Result:** Spot checks against NinjaTrader showed a 0.000 difference in session levels. Every strategy was retested on the corrected data before anything went live.

## 2. An entry rule that could almost never fire

**How it showed up:** The strategy fired far less often than the research predicted.

**Root cause:** The code required both session levels to be touched on the exact same one minute bar. The research meant both levels touched at any point during the session.

**Fix:** Replaced the same bar check with a flag for each level that stays true once that level is touched during the session.

**Result:** The live entry now matches the rule the research actually tested.

## 3. Four bugs in the backtest engine itself

**How it showed up:** Backtest results didn't match what NinjaTrader produced on the same strategies.

**Root cause:** An audit found four separate issues:

- Higher timeframe EMAs used the bar that was still forming instead of the last closed bar

- When a stop and target were both hit on the same bar, the engine filled the target first

- The session build window had a midnight reset bug

- The armed window was off by one bar

**Fix:** Higher timeframe values now use only closed bars, a stop fills before a target on the same bar (matching NinjaTrader), the session reset was corrected and window counting became inclusive.

**Result:** Backtests got less flattering and closer to live behavior. Strategies were rerun and reranked on the fixed engine.

## 4. A crash that silently cut the search short

**How it showed up:** The signal search finished for crude oil but never produced results for silver, gold or the index futures.

**Root cause:** A variable in the auto stack code was only created inside an if statement. The first time that condition was false, Python raised an unbound variable error and everything after crude oil never ran.

**Fix:** Initialized the variable before the branch and reran the full search on every market.

**Result:** Five markets that had never been tested were added to the research.

## 5. The live strategy didn't match the research

**How it showed up:** A live gold strategy's stop and target looked nothing like the validated values when checked against the research workbook.

**Root cause:** The deployed NinjaScript file still had parameters from an earlier version. The research had validated targets several times larger and a third filter the live code didn't have.

**Fix:** Updated the live code to the validated parameters and started checking every deployed file line by line against the research before going live.

**Result:** This became a standing rule: config drift between research and live code gets checked on every deployment. See [engineering notes](./engineering-notes.md).

## 6. Wrong tick values in the backtester

**How it showed up:** Stop and target prices came out wrong for some markets.

**Root cause:** Price per tick wasn't set correctly for each contract, so stop and target distances were converted to dollars with the wrong values.

**Fix:** Built a single tick size table per symbol (for example gold 0.10, silver 0.005, crude 0.01) that every calculation reads from.

**Result:** Dollar figures match the exchange contract specs.

## 7. Lookahead bias in an opening range strategy

**How it showed up:** One older strategy averaged $69.58 per trade, which was too good compared to everything else.

**Root cause:** 52% of its "validated" trades entered before the opening range had actually finished forming. The backtest was using information that didn't exist yet at the time of the trade.

**Fix:** Rebuilt the strategy so entries can only happen after the range closes, and added a delay test to the pipeline that shifts every entry one bar later. A real edge survives that; a lookahead bug falls apart.

**Result:** The real average was $3.23 per trade. The delay test has since rejected 8 more candidates.

## 8. Duplicate trades inflating expectancy

**How it showed up:** Published strategy stats looked better than live results justified.

**Root cause:** The trade book contained duplicate trades, so winning trades were counted more than once.

**Fix:** Deduplicated the trade records and recalculated every published statistic.

**Result:** Strategy stats were corrected downward before being published again.

## 9. Live strategies running on frozen data

**How it showed up:** Four of six live strategies stopped behaving like their backtests while still appearing to run normally.

**Root cause:** A platform level bug froze the higher timeframe data series those strategies depended on.

**Fix and result:** Full writeup in the [debugging journal](./debugging-journal.md), including five wrong theories ruled out before finding the cause.

## 10. A false alarm from reading the wrong setting

**How it showed up:** A pre-launch review read the strategies' saved settings and found "Days to load: 5" on every copy. MGC Asia's gap filter needs 15 completed trading days and is built to fail closed, so with 5 days it would not have traded for about three weeks.

**Root cause:** The review was wrong, not the setup. NinjaTrader's saved settings contain a default "Days to load" value that isn't the one in use. The history actually loaded comes from the start and end dates stored next to it: 120 days for both gold strategies and 30 for MNQ Asia, exactly what had been set in the platform.

**Fix:** Nothing in the setup needed changing. The checking procedure did: settings are now read from the fields NinjaTrader actually uses and confirmed against the platform's own screen before anyone is told to change anything.

**Result:** A reminder that an audit can be wrong too. A finding has to be checked against the system's real behavior before it turns into a change, especially on live accounts.

## 11. A new account blocked from every trade

**How it showed up:** One new prop account didn't take a single trade while the same strategies traded on the other four.

**Root cause:** When the risk guard has no saved drawdown floor for an account, it seeds one from a table of known accounts. That table matched on the account type prefix, which the old and new accounts shared, so the new $50,000 account inherited the old account's locked $50,100 floor. Against the new balance that left zero room, so the guard set the daily loss limit to $0 and skipped every entry. The bad floor had also already been saved to disk.

**Fix:** The table now matches the full account ID, and any account it doesn't know starts as a fresh account. The saved file was backed up and deleted, and the platform fully restarted, because the guard caches floors in memory. It reseeded as a fresh $48,000 floor, unlocked, and the next morning the account took the same Nasdaq trade as the other four. ([code](../code-examples/guards/account-seed-prefix-bug.cs))

**Result:** The guard did what it was built to do, fail safe. The lesson: guard state has to be keyed to the full account ID and set fresh on every account change.

## 12. Should a losing trade change the rules?

**How it showed up:** An MGC Asia trade was up about $96, then reversed into a full stop loss. The obvious reaction was to move the breakeven stop much earlier.

**Investigation:** Tested breakeven triggers across the strategy's 137 historical trades, measured on the holdout period:

| Rule | $ per trade | Payout odds | Blow rate | Trades that win |
|---|---|---|---|---|
| Breakeven at +0.5R | $35 | 46% | 5.1% | 29% |
| Breakeven at +1R | $53 | 68% | 14% | 40% |
| Breakeven at +2R | $102 | 96% | 2.6% | 50% |
| **Breakeven at +3R, lock +0.5R (live)** | **$104** | **97%** | **1.1%** | **55%** |
| No breakeven | $90 | 91% | 5.3% | 53% |

**Result:** The live rule was kept. This strategy's winners usually go about $80 in favor, pull back near entry and then run for hours, so an early breakeven would have saved that one trade but knocked out most of the winners. Changing rules after one losing trade is how a working strategy gets broken.

## 13. Would a time stop help?

**How it showed up:** A follow up question after problem 12: what if trades were closed after a set amount of time?

**Investigation:** Tested 17 time stop lengths from 15 minutes to 12 hours on MGC Asia, all with the live breakeven rule.

**Result:** Every length scored lower than no time stop. The median trade is held about 11.5 hours and the profit builds into the London session and the morning, so short time stops turn winners into scratches. No change made.

## 14. Platform setup and data integrity checks before going live

**How it showed up:** A pre launch review of the NinjaTrader setup found all instruments stacked in one chart window instead of separate charts, and one market's chart showing a flat line with a broken price scale.

**Fix:** Separated the charts, confirmed data on every instrument, and checked every strategy copy for the right account, history setting and timeframe before enabling it.

**Result:** Deployments now follow a written setup checklist.

## 15. A position left without a stop

**How it showed up:** On the new basket's first live day, one account held a gold position with no stop. It was closed by hand for -$896. The strategy's stop would have capped the loss at -$276.

**Root cause:** Two mistakes stacked. Some strategies had been attached to the wrong instrument's chart, and while fixing that, a strategy was disabled and re-enabled mid-trade. Disabling a managed NinjaTrader strategy cancels its own stop and target, and the position stays open.

**Fix:** Every strategy now refuses to start on the wrong instrument, and on every bar it checks the account itself for any open position on its instrument with no working stop, sounding an alert if it finds one. A standing rule was added too: never disable or enable a strategy while it's in a trade. ([code](../code-examples/guards/unprotected-position-check.cs))

**Result:** The startup check fired several times during a later restart while strategies were being re-attached, and confirmed every copy ended up on the right chart before the session opened.

## 16. The platform crashing when the network dropped

**How it showed up:** Twice in one week the platform simply wasn't running. One crash skipped a 4:00 PM flatten. Another left it down from 7:05 AM to 2:55 PM, and a whole New York session was missed before anyone noticed.

**Root cause:** The platform's own log stops mid-line when it crashes, so the evidence came from Windows. The first crash had a .NET stack trace inside the platform's broker adapter, in its market data disconnect handler. The Wi-Fi log showed 110 reconnects in 14 days on a Realtek card known for dropouts, with its own internal power saving still on. The second crash had a different error code and no network drop near it, so its cause is still open.

**Fix:** Turned down the card's power saving and stopped Windows from powering it off, timed for after the last trading window closed. The permanent fix is hardware: an Intel card or a wired connection. A platform update and a watchdog that alerts when the platform isn't running cover the crash itself. Full investigation in the [debugging journal](./debugging-journal.md).

**Result:** Three problems that looked like one were separated: unclean reboots (power button and flat battery, none were system crashes), Wi-Fi drops, and a vendor bug triggered by those drops.

## 17. Most winning long strategies were luck

**How it showed up:** The overnight session search found strong-looking long strategies on every market.

**Root cause:** A search across thousands of rules always finds some winners, and stock index and gold futures drifted up overnight for most of the data, so a rule that just buys in the evening looks like an edge.

**Fix:** The whole search was rerun on shuffled data where no rule can have an edge, and again with each hour's average drift removed from P&L. The real search only counts if its survivors clearly beat the shuffled search's survivors. ([code](../code-examples/validation-bugs/same-search-null-test.py))

**Result:** On Nasdaq Asia longs, the shuffled search produced 22 survivors against 6 for the real one, so the group was rejected and only one long that also beat the drift-free test went live. Gold Asia shorts held up: 30 real survivors against 8 on drift-free shuffled data. The first drift removal was itself biased against longs (it turned random gold longs from +9.8 to -9.1 ticks) and was replaced before any conclusion was drawn.

## 18. Risk management layers that only worked in hindsight

**How it showed up:** Breakeven stops, partial exits, trailing stops, time stops and scale-ins all looked like improvements on the full history.

**Root cause:** Choosing a setting on the same data that grades it. Each layer was swept over a full grid, chosen inside walk forward, and judged on data it hadn't seen.

**Result:** The in-sample choice was worse out of sample 70 to 94% of the time depending on the layer. In a bad stretch the optimizer reached for extreme settings that ranked last out of sample, and these strategies' edge builds over hours, so early exits cut winners. Circuit breakers (1,311 rules) and regime filters (45 market features) found nothing either. The few management rules that are live were each chosen against a written scoring rule, with near misses shown next to their cost in blown accounts.

## 19. The C# port disagreeing with the research

**How it showed up:** Before going live, each strategy was rewritten in Python exactly as the C# works and run on the research data. Every entry had to match.

**Root cause:** Several quiet differences: NinjaTrader's built-in Bollinger Bands use population standard deviation where the research used sample; its pivot indicator builds levels from the session template instead of regular-hours bars; stop sizes in research weren't whole ticks.

**Fix:** The strategies build their own higher timeframes and indicators from 1 minute bars to match the research, and stops were rounded to whole ticks. ([code](../code-examples/validation-bugs/replica-parity-check.py))

**Result:** 335 of 335, 446 of 446 and 137 of 137 entries matched. The last strategy also had 4 extra entries, all on Sundays. They traced to stray weekend bars in the research data that created a fake trading day, so the live code was right and the research data was wrong.

---

*Exact parameters, signal definitions and account numbers are intentionally left out.*
