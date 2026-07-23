# XAUUSD Trend Breakout

## Overview

A daily timeframe trend and breakout EA on gold, built and backtested in MetaTrader 5. This writeup covers what happened after the strategy existed: finding what was actually dragging on performance, proposing fixes, and testing whether each fix held up rather than trusting a better headline number.

The original parameters came from manual iteration, not a systematic search. The real work here is the analysis layer built on top of the resulting trade data. Exact parameter values are left out.

## Data

Trade records were rebuilt by pairing MT5 Strategy Tester output with the EA's own execution journal, joining each entry's context, signal price, fill, stop, trend state, to its eventual exit and profit. That pairing is what makes the deeper analysis possible. Every trade carries the reason it was taken alongside how it turned out.

## Baseline

82 paired trades tested since 2021, a 46.3 percent win rate, a profit factor of 3.08, average profit per trade around $32.

| Metric | Value |
|---|---|
| Total paired trades | 82 |
| Win rate | 46.3% |
| Profit factor | 3.08 |
| Avg profit per trade | $32.15 |
| Max win | $840.15 |
| Max loss | -$69.22 |

## Supporting data

Weekday breakdown, all paired trades:

| Weekday | Trades | Win rate | Profit factor |
|---|---|---|---|
| Monday | 13 | 46.2% | 2.18 |
| Tuesday | 19 | 42.1% | 3.19 |
| Wednesday | 12 | 50.0% | 2.59 |
| Thursday | 21 | 47.6% | 4.54 |
| Friday | 17 | 47.1% | 2.33 |

Month breakdown, showing the seasonal cluster referenced below:

| Month | Trades | Win rate | Profit factor |
|---|---|---|---|
| Jan | 4 | 50.0% | 4.92 |
| Feb | 8 | 62.5% | 12.66 |
| Mar | 7 | 14.3% | 0.01 |
| Apr | 5 | 20.0% | 0.34 |
| May | 11 | 27.3% | 0.51 |
| Jun | 6 | 66.7% | 5.84 |
| Jul | 7 | 42.9% | 1.97 |
| Aug | 10 | 60.0% | 6.93 |
| Sep | 7 | 42.9% | 1.19 |
| Oct | 3 | 66.7% | 2.20 |
| Nov | 6 | 50.0% | 3.72 |
| Dec | 8 | 62.5% | 4.79 |

March through May sit well below the rest of the year, the cluster referenced in the seasonal finding below.

Trend regime split, referenced in the regime finding below:

| Regime | Trades | Win rate | Profit factor |
|---|---|---|---|
| Bullish (price above trend line) | 56 | 50.0% | 4.57 |
| Bearish (price below trend line) | 26 | 38.5% | 0.59 |

## What the investigation found

The dataset gets sliced along eight dimensions, weekday, month, direction, trend regime, signal state, stop distance, moving average spread, and distance from trend, then win rate and profit factor get checked inside each slice. That's how you find where an edge actually lives instead of trusting one aggregate number.

**Directional asymmetry.** One direction was clearly dragging the other down. Rather than cutting it blind, the losing side got reviewed on its own before deciding anything. The call was to disable it and treat the two directions as separate legs going forward. Restricting to the stronger direction alone took the profit factor from 3.08 to 4.57 on the same data.

**A seasonal drawdown cluster.** A specific three month window showed a concentrated run of losses. Rather than bolting on a calendar filter, the cluster got attributed to gold's broader macro regime during that stretch rather than something the calendar itself causes. Treating it as a filter risks fitting to one bad quarter and calling it a seasonal pattern.

**Regime dependency.** Splitting by trend regime, price above versus below a long moving average, showed almost all the strategy's edge sitting in the bullish side. The bearish side lost money outright. Still open. Either restrict entries to the favorable regime or build a separate model for the other one instead of forcing one rule set to cover both.

## How fixes get validated

One change at a time, never stacked. Re run on a different time window, not just the one the finding came from. Compare profit factor, average profit, and max drawdown together, not total profit alone, since one big win can flatter a weak change. Confirm the improvement holds across multiple years, not one lucky month. Write the decision down, including the ones considered and turned down.

## Open items

Directional asymmetry is resolved, split into two legs. The seasonal cluster was investigated and attributed to regime rather than acted on directly, worth watching. Bear regime underperformance is still open, next step is either tighter entry confirmation there or a separate model entirely.
