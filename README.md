# Zealot Investing

This repo documents the research and engineering behind my systematic trading work. It shows how the work was done, not the tradeable edge itself. Strategy parameters, raw datasets and research notebooks stay private on purpose.

## Live futures basket (current, November 2026)

Three automated strategies on CME micro futures, running in NinjaTrader 8 and copied across five Lucid prop firm accounts. Every account runs the same three strategies under a shared C# risk guard.

| Strategy | Market | Session | Holdout $ per trade | Payout odds | Blow rate |
|---|---|---|---|---|---|
| MGC Asia | Micro Gold | Asia | $104 | 97% | 1.1% |
| MNQ Asia | Micro Nasdaq 100 | Asia | $61 | 98% | 1.7% |
| MGC NY | Micro Gold | New York | $29 | 85% | 5.9% |

Holdout figures come from a period that was never used to pick or tune the strategy. Payout odds and blow rate come from Monte Carlo simulation of each strategy against the prop firm's rules: the chance an account reaches a payout before it hits its loss limit.

Exits were chosen by testing, not by feel. On MGC Asia, a breakeven stop at +3R that locks in +0.5R beat every alternative tested, including earlier breakevens and 17 different time stops (see the [problem log](./docs/problem-log.md)).

**Risk rules built into the code**

- A daily loss cap on each account, enforced before any new entry

- Position size cut in half once an account falls a set distance below its equity peak

- A per account drawdown floor, so an account near its limit stops trading instead of blowing

## Research pipeline (v4, October 2026)

Built in Python on two years of Rithmic 1 minute data across six micro futures.

| Stage | Count |
|---|---|
| Signal combinations screened | 15,348 |
| Sent to full walk forward and holdout validation | 72 |
| Promoted after review | 9 |
| Running live | 3 |

Every candidate had to hold up across walk forward folds (6 months in sample, 3 months out of sample), a final holdout from February to October 2026 that was never touched during selection, and a delay test that shifts every entry one bar later to catch lookahead bias. 63 of the 72 candidates failed at least one of those checks.

**The audit that mattered most.** An October 2026 review found lookahead bias in an older strategy: 52% of its "validated" trades entered before its opening range had actually formed. With the bug its average was $69.58 per trade. Without it, $3.23. The strategy was rebuilt, and the delay test was added to the pipeline so every candidate now gets checked for data that would not have existed at the time of the trade.

## Writeups

**[Problem Log](./docs/problem-log.md)**
Fourteen real problems found across research, backtesting and live trading, each with how it showed up, the root cause, the fix and what changed. The best single page for seeing how I work.

**[Micro Futures Basket](./futures-basket/pipeline-writeup.md)**
The earlier v3 pipeline and how each stage works: gate tests, stop and target optimization, Monte Carlo simulation and live implementation.

**[XAUUSD Trend Breakout](./xauusd-trend-breakout/research-writeup.md)**
A daily breakout EA on gold, built in MT5. Segmenting 82 trades by regime, seasonality and direction found what was dragging performance. Trading only the stronger direction lifted the profit factor from 3.08 to 4.57 on the same data.

**[Debugging Journal](./docs/debugging-journal.md)**
A platform level bug quietly froze the data feeding four of six live strategies while they kept running like nothing was wrong. Five wrong theories before the actual cause, and why testing each one cheaply mattered more than guessing right the first time.

**[Engineering Notes](./docs/engineering-notes.md)**
A catalog of validation bugs found and fixed, with code examples.

## What's not here

Raw notebooks and workbooks, exact parameters, account numbers, broker credentials, and anything that would let someone rebuild the live edge.

## Stack

Python, pandas, Jupyter, NinjaScript (C#), NinjaTrader 8, MetaTrader 5, PostgreSQL, Rithmic data. Claude and Claude Code were used throughout as research and debugging partners.
