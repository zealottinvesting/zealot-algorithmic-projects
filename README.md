# Zealot Investing

This repo documents the research and engineering behind my systematic trading work. It shows how the work was done, not the tradeable edge itself. Strategy parameters, raw datasets and research notebooks stay private on purpose.

## Live futures basket (current, November 2026)

Three automated strategies on CME micro futures, running in NinjaTrader 8 on five Lucid prop firm accounts (one funded, four in evaluation). Every account runs the same three strategies under a shared C# risk guard.

| Strategy | Market | Session |
|---|---|---|
| Asia session long | Micro E-mini Nasdaq 100 | Enters 6:00 PM to 3:00 AM ET |
| New York session short | Micro Gold | Enters 8:00 AM to 4:00 PM ET |
| Asia session short | Micro Gold | Enters 6:00 PM to 3:00 AM ET |

**Risk rules built into the code**
- Position size scales with each account's distance from its trailing drawdown floor, so size shrinks automatically as an account gets closer to failing.
- The daily stop is the smaller of $1,150 or 1.5x that remaining room, and it counts open risk, not just closed trades.
- One trade per strategy per CME trading day. Each strategy refuses to start on the wrong instrument and raises an alarm if the account ever holds a position with no working stop.

**First live week (Nov 1 to 4, 2026):** 5 trades, 3 wins and 2 losses, about +$437 per account before commissions. That is far too small a sample to judge anything, and the plan is fixed in advance: no changes until 40 live trades, then compare live against research trade by trade.

**What it replaced.** The October basket (four strategies including crude oil and S&P) lost money live and was retired. Instead of tuning it, the whole research pipeline was rebuilt with a stricter question: could this result have come from luck?

## Research pipeline (v4.1, October to November 2026)

Built in Python on two years of 1 minute data across four micro futures (gold, Nasdaq, S&P, Dow), mined separately for the New York, London and Asia sessions.

1. **Raw edge.** About 100 signal families, including ICT concepts and a library of indicators ported line for line from NinjaTrader source, combined into 6,813 candidate signals per symbol. Candidates must pass walk forward (anchored folds) and a final holdout that was never touched during selection.
2. **Luck test.** The entire search is rerun on shuffled data where no rule can have an edge. If the shuffled search produces as many survivors as the real one, the real survivors are treated as luck. A second version removes each hour's average drift from P&L, so a long that only works because the market went up overnight does not count. ([example](./code-examples/validation-bugs/same-search-null-test.py))
3. **Exits.** A full grid of stop and target levels based on how far trades actually move for and against the entry (MFE/MAE), scored by the probability of reaching a prop firm payout minus the probability of blowing the account.
4. **Risk management layers.** Breakeven stops, partial exits, trailing stops, time stops and scale-ins, each swept over a full grid, selected inside walk forward, smoothed so a single lucky cell can't win, and kept only if it improves out of sample in at least 3 of 4 folds and in the holdout.
5. **Regime filters, circuit breakers and news.** 45 causal market features, 1,311 loss streak and drawdown breaker rules, and news skips built on the official CPI, jobs report and FOMC calendars, each with its own luck test.
6. **Port to C# and prove it.** Each strategy is reimplemented in Python exactly as the C# code works and must reproduce every research entry: 335 of 335, 446 of 446, and 137 of 137 entries matched. ([example](./code-examples/validation-bugs/replica-parity-check.py))

**What the pipeline said no to.** This is the part I'm proudest of, because every "no" was money not lost.
- Most long strategies were luck. On Nasdaq Asia, the shuffled search produced 22 survivors against 6 for the real one. Gold Asia shorts were the opposite: 30 real survivors against 8 on drift-free shuffled data.
- Risk layers almost never helped. Across 48 strategy and layer starting points, the setting picked in sample was worse out of sample 70 to 94% of the time. Breakers and regime filters: nothing passed.
- One finding went against intuition: FOMC days were the *best* days for the gold New York short, so a "skip the news" rule would have cut profit.
- I overrode the pipeline once, on purpose: a gap filter on the gold Asia short with a luck p value of 0.06, just above the 0.05 cutoff. It's documented as an override, not passed off as a pass.

**The earlier audit.** An October 2026 review of the previous pipeline found lookahead bias: 52% of one strategy's "validated" trades entered before its opening range had actually formed. With the bug it averaged $69.58 per trade; without it, $3.23. Every stage now checks for data that would not have existed at the time of the trade.

## Live operations

Running five accounts unattended surfaces problems no backtest shows. Recent ones, each found from logs and data rather than guesses:
- **A new account never traded.** The guard seeded its drawdown floor by account name prefix, so the new account inherited an old account's locked floor and got $0 of risk room. ([code](./code-examples/guards/account-seed-prefix-bug.cs))
- **A position lost its stop.** Disabling a strategy mid-trade cancels its own stop. Now every strategy checks the account for unprotected positions on every bar. ([code](./code-examples/guards/unprotected-position-check.cs))
- **The platform crashed on network drops.** Windows event logs showed the trading platform crashing inside its broker adapter when the market data connection dropped. The drops traced to the laptop's Wi-Fi driver (110 reconnects in 14 days). The full write-up is in the [debugging journal](./docs/debugging-journal.md).

## Other projects

**[Micro Futures Basket (v3)](./futures-basket/pipeline-writeup.md)**
The earlier pipeline and the six strategy basket it produced, stage by stage.

**[XAUUSD Trend Breakout](./xauusd-trend-breakout/research-writeup.md)**
A daily breakout EA on gold, built in MT5. The value here is the analysis layer built afterward: segmenting 82 trades by regime, seasonality and direction to find what was dragging performance. Trading only the stronger direction lifted the profit factor from 3.08 to 4.57 on the same data, and the writeup explains why a seasonal filter was deliberately not added.

**[Debugging Journal](./docs/debugging-journal.md)**
Real incidents, including a platform bug that quietly froze the data feeding four of six live strategies. It walks through five wrong theories before the actual cause, and why testing each one cheaply mattered more than guessing right the first time.

**[Engineering Notes](./docs/engineering-notes.md)**
A catalog of validation bugs found and fixed, with code examples.

## What's not here

Raw notebooks and workbooks, exact parameters and signal definitions, account numbers, broker credentials, and anything that would let someone rebuild the live edge.

## Stack

Python (pandas, NumPy, Numba), Backtrader, PostgreSQL, NinjaScript (C#), MetaTrader 5. Claude was used throughout as a research and debugging partner.

*Results shown are from historical testing and a short live period. They do not guarantee future performance. Not investment advice.*
