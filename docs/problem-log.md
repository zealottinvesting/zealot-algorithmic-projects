# Problem Log

Real problems found while building, testing and running the Zealot futures basket, from the first backtests in spring 2026 to the live basket in November 2026. Each one lists how it showed up, what was actually wrong, how it was fixed and what changed because of it.

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

## 10. Not enough history loaded for the strategy to trade

**How it showed up:** After the new basket was set up, the review found every copy was set to load only 5 days of history.

**Root cause:** MGC Asia's gap filter needs 15 completed trading days before it will trade, and it is built to fail closed rather than trade on incomplete data. With 5 days loaded it would have taken no trades for about three weeks. MNQ Asia would still trade, but its trailing stop needs 14 days of daily ranges and would have stayed off.

**Fix:** Set Days to load to 30 on MGC Asia and 20 on MNQ Asia across all copies, verified against NinjaTrader's strategy database.

**Result:** Both strategies trade with every filter active from the first session.

## 11. A new account blocked from every trade

**How it showed up:** One new prop account didn't take a single trade while the same strategies traded on the other four.

**Root cause:** The risk guard had no saved drawdown floor for the new account, so it fell back to the old account's numbers. Against a fresh account balance that left zero room, so the guard set the daily loss limit to $0 and skipped every entry. Separately, one strategy was still pointed at the old account's name.

**Fix:** Gave the new account its own peak and floor and synced the strategy settings to the new account name. After a restart the guard reseeded it as a fresh account with its own floor, unlocked, and all three strategies were enabled on it.

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

---

*Exact parameters, signal definitions and account numbers are intentionally left out.*
