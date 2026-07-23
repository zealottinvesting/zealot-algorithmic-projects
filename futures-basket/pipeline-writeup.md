# Micro Futures Basket

## Overview

Six strategies across four CME micro futures instruments (Micro Gold, Micro Nasdaq, Micro S&P, Micro Dow), deployed live through NinjaTrader 8 on two independently risk managed prop firm accounts. The basket came out of a four stage Python pipeline, then got implemented and hardened as live NinjaScript strategies.

1,560 candidate signals entered the pipeline; six reached live deployment.

Exact periods, thresholds, gate windows and stop/target distances are left out here on purpose. This is about the process, not the rules it produced.

## Pipeline

Four stages, each one narrowing the candidate pool and raising the bar for what survives to the next.

Gate test, then stop and target optimization, then Monte Carlo risk simulation, then live deployment.

### Gate test: the matrix sweep

This stage tests candidate signal combinations across every symbol at once, not one at a time. Each candidate pairs a higher timeframe gating condition with a lower timeframe entry trigger, evaluated inside a bounded window.

The sweep is genuinely combinatorial. A standalone script loads the full research log, 1,560 candidate rows, and systematically pairs gating conditions with entry triggers across all four instruments, scoring each pairing on win rate, instance count, statistical significance, and an edge score. Only pairings that clear a minimum sample size and significance threshold move forward, 300 of the 1,560. A second, three layer gate narrows that further, down to 40. The run checkpoints per symbol so it can go unattended and pick back up if interrupted.

This is the part that actually earns the word matrix. Instead of hand picking a few ideas to test, the process runs the full combinatorial space and lets the numbers decide what's worth carrying forward.

### Stop and target optimization

Of the 40 candidates that clear the gate, 30 reach this stage with enough data to derive a usable stop and target.

Every surviving candidate gets its own stop and target derived from the actual distribution of outcomes, a percentile based worst case for the stop, a median favorable move for the target, computed separately on in sample and out of sample data so the risk profile has to hold up on data it wasn't fit to.

### Monte Carlo simulation

Each stop/target optimized candidate runs through ten thousand plus simulated trade sequences at several risk levels, producing blow probability and payout probability curves. A signal only reaches live deployment if it clears a minimum payout probability and stays under a maximum blow probability at a risk level that actually makes sense for the account. This step exists specifically to catch signals that look fine on win rate alone but carry real tail risk.

18 of the 30 candidates clear that bar.

### Live implementation

Six of the 18 candidates that clear Monte Carlo simulation were selected for live deployment, becoming live NinjaScript strategies, sitting under a shared account level risk guard that enforces a daily trade cap per strategy, an account wide daily loss circuit breaker that doesn't care which strategy caused the loss, and a profit consistency guard that compares today's running profit against everything banked before it, armed only once there's enough banked profit for the comparison to mean anything.

## Data integrity

A meaningful chunk of the live deployment work went into finding and fixing a platform level bug where several strategies' higher timeframe signal logic was quietly reading frozen historical data instead of live ticks. The full writeup is in the [debugging journal](../docs/debugging-journal.md). The short version: the fix was to rebuild the affected timeframe from a series confirmed to be reliably live, rather than depend on the platform's own handling of it.

## Monitoring

Live fire frequency and win rate get checked against what the research predicted on an ongoing basis, so a strategy drifting away from its validated behavior gets caught early instead of showing up as a bad month. Trade level execution data lands in PostgreSQL through a process fully decoupled from live trading, the strategies log locally and a separate script handles the database write, so nothing about the database can ever slow down or block a live order.
