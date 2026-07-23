# Zealot Investing

This repo documents the research and engineering behind two systematic trading projects. It shows how the work was done, not the tradeable edge itself. Strategy parameters, raw datasets, and research notebooks stay private on purpose.

## Projects

**[XAUUSD Trend Breakout](./xauusd-trend-breakout/research-writeup.md)**
A daily breakout EA on gold, built in MT5. The parameters came from manual iteration, but the value here is the analysis layer built afterward: segmenting trades by regime, seasonality, and direction to find what was actually dragging performance. Removing one underperforming trade direction lifted the profit factor from 3.08 to 4.57.

**[Micro Futures Basket](./futures-basket/pipeline-writeup.md)**
Six strategies across four CME micro futures instruments, deployed live through NinjaTrader 8 on two prop accounts. Built on a four-stage Python pipeline that runs a genuine multi symbol, multi signal search: 1,560 candidate signals swept, narrowed to 300 that clear an initial significance bar, 40 through a three layer gate, 30 into stop/target optimization, 18 through Monte Carlo simulation, and six selected for live deployment.

**[Debugging Journal](./docs/debugging-journal.md)**
A real incident writeup. A platform level bug quietly froze the data feeding four of six live strategies, and the strategies kept running like nothing was wrong. This walks through five wrong theories before finding the actual cause, and why testing each one cheaply mattered more than guessing right the first time.

## What's not here

Raw notebooks and workbooks, exact parameters, account numbers, broker credentials, anything that would let someone rebuild the live edge.

## Stack

Python, MetaTrader 5, NinjaScript, PostgreSQL, Backtrader. Claude was used throughout as a research and debugging partner.
