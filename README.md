# Zealot Investing

This repo documents the research and engineering behind my systematic trading work. It shows how the work was done, not the tradeable edge itself. Strategy parameters, raw datasets and research notebooks stay private on purpose.

**Start here:** the [Problem Log](./docs/problem-log.md) lists 19 real problems, each with how it showed up, the root cause, the fix and the result. It is the best single page for seeing how I work.

## Live futures basket (current, November 2026)

Three automated strategies on CME micro futures, running in NinjaTrader 8 and copied across five Lucid prop firm accounts (one funded, four in evaluation). Every account runs the same three strategies under a shared C# risk guard.

| Strategy | Market | Session | Holdout $ per trade | Payout odds | Blow rate |
|---|---|---|---|---|---|
| MGC Asia | Micro Gold | Asia | $104 | 97% | 1.1% |
| MNQ Asia | Micro Nasdaq 100 | Asia | $61 | 98% | 1.7% |
| MGC NY | Micro Gold | New York | $29 | 85% | 5.9% |

Holdout figures come from a period that was never used to pick or tune the strategy. Payout odds and blow rate come from Monte Carlo simulation of each strategy against the prop firm's rules: the chance an account reaches a payout before it hits its loss limit.

Exits were chosen by testing, not by feel. On MGC Asia, a breakeven stop at +3R that locks in +0.5R beat every alternative tested, including earlier breakevens and 17 different time stops (problems 12 and 13 in the [problem log](./docs/problem-log.md)).

**Risk rules built into the code**
- Position size scales with each account's distance from its trailing drawdown floor, so an account near its limit trades smaller instead of blowing.
- The daily stop is the smaller of $1,150 or 1.5 times that remaining room, and it counts open risk, not just closed trades.
- One trade per strategy per CME trading day.
- Each strategy refuses to start on the wrong instrument and sounds an alert if the account ever holds a position with no working stop.

**First live week (Nov 1 to 4, 2026):** 5 trades, 3 wins and 2 losses, about +$437 per account before commissions. Five trades say nothing about the edge. The plan was fixed before going live: no changes until 40 trades, then compare live against research trade by trade.

## Research pipeline (v4, October to November 2026)

Built in Python on two years of Rithmic 1 minute data.

| Stage | Count |
|---|---|
| Signal combinations screened in the main v4 run (six micro futures, New York session) | 15,348 |
| Candidate signals per symbol in the follow-up library and overnight session mines (four micro futures, three sessions) | 6,813 |
| Survivors sent to full walk forward and holdout validation (main run) | 72 |
| Promoted after review (main run) | 9 |
| Running live (all three from the follow-up mines) | 3 |

The configuration counts are reported on purpose. The more rules a search tries, the more of its winners are luck, so a result means little without knowing how big the search behind it was.

Every candidate had to pass these checks:
- **Walk forward:** the strategy must hold up on each out-of-sample fold, not just on the full history.
- **Holdout:** a final period never touched during selection.
- **Delay test:** shifts every entry one bar later to catch lookahead bias.
- **Luck test:** reruns the whole search on shuffled data where no rule can have an edge. The real search only counts if its survivors clearly beat the shuffled search's survivors. This rejected most long strategies, which were riding the market's overnight drift (problem 17).
- **C# port check:** each live strategy is rewritten in Python exactly as the C# works and must reproduce every research entry. 335 of 335, 446 of 446 and 137 of 137 entries matched (problem 19).

Exits and risk management were tested the same way: every layer swept over a full grid and chosen inside walk forward. Almost all of them failed out of sample (problem 18), so the live strategies run the simplest version that survived.

**The audit that mattered most.** An October 2026 review found lookahead bias in an older strategy: 52% of its "validated" trades entered before its opening range had actually formed. With the bug its average was $69.58 per trade. Without it, $3.23. The strategy was rebuilt, and the delay test was added so every candidate now gets checked for data that would not have existed at the time of the trade.

## Writeups

**[Problem Log](./docs/problem-log.md)**
Nineteen problems across research, backtesting and live trading, including one where my own audit was wrong (problem 10).

**[Debugging Journal](./docs/debugging-journal.md)**
Two long-form investigations: a platform bug that quietly froze the data feeding four of six live strategies (five wrong theories before the real cause), and a platform crash traced through Windows event logs to a Wi-Fi driver.

**[Engineering Notes](./docs/engineering-notes.md)**
A catalog of validation bugs found and fixed, plus how the luck test and the exit research work.

**[Micro Futures Basket (v3)](./futures-basket/pipeline-writeup.md)**
The earlier pipeline and how each stage works: gate tests, stop and target optimization, Monte Carlo simulation and live implementation.

**[XAUUSD Trend Breakout](./xauusd-trend-breakout/research-writeup.md)**
A daily breakout EA on gold, built in MT5. Segmenting 82 trades by regime, seasonality and direction found what was dragging performance. Trading only the stronger direction lifted the profit factor from 3.08 to 4.57 on the same data.

## Code examples

Short, self-contained excerpts of real fixes, each with the problem it solves at the top. See the [code examples index](./code-examples/README.md) for what each file shows and how to run the Python ones.

## What's not here

Raw notebooks and workbooks, exact parameters and signal definitions, account numbers, broker credentials, and anything that would let someone rebuild the live edge.

## Stack

Python (pandas, NumPy, Numba), Jupyter, NinjaScript (C#), NinjaTrader 8, MetaTrader 5, PostgreSQL, Rithmic data. Claude and Claude Code were used throughout as research and debugging partners.

*Results shown are from historical testing and a short live period. They do not guarantee future performance. Not investment advice.*
