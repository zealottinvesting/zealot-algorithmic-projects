# Code Examples

Short excerpts of real fixes from the live trading system and the research pipeline. Each file starts with the problem it solves and how the problem was found, then shows the code before and after. Parameters and signal definitions are left out or replaced with illustrative values.

## Validation and research (Python, runnable)

These run on their own with Python 3.10+ and the packages in [requirements.txt](./requirements.txt). Each prints its result.

| File | What it shows | Output |
|---|---|---|
| [lookahead-bar-labeling.py](./validation-bugs/lookahead-bar-labeling.py) | Resampling that labels bars by open time leaks future prices into a backtest | At 10:01 the buggy version already shows the 10:04 close |
| [same-search-null-test.py](./validation-bugs/same-search-null-test.py) | Whether the winners of a large rule search beat the winners of the same search on data with no edge | Survivor counts for the real and 20 shuffled searches, and a p value (about 15 seconds) |
| [replica-parity-check.py](./validation-bugs/replica-parity-check.py) | Checking that the deployed strategy takes exactly the research trades | Matched, missing and extra entries, with the weekday pattern that exposed bad research data |
| [config-drift.py](./validation-bugs/config-drift.py) | A backtest config silently drifting from the live strategy file | The fields that no longer match |

```bash
pip install -r code-examples/requirements.txt
python code-examples/validation-bugs/lookahead-bar-labeling.py
```

## Live strategy code (NinjaScript C#, excerpts)

These are excerpts from NinjaTrader 8 strategies and only compile inside a NinjaTrader strategy class.

| File | What it shows |
|---|---|
| [guards/account-seed-prefix-bug.cs](./guards/account-seed-prefix-bug.cs) | A new account inheriting another account's locked drawdown floor through a name prefix match |
| [guards/unprotected-position-check.cs](./guards/unprotected-position-check.cs) | Refusing to start on the wrong instrument, and alerting on any position without a working stop |
| [guards/ZealotGuardLFE.cs](./guards/ZealotGuardLFE.cs), [guards/ZealotGuardLTE.cs](./guards/ZealotGuardLTE.cs) | Shared per-account risk guards: daily loss lock, consistency lock, per-symbol position lock across strategies |
| [patterns/synthetic-bar-aggregation.cs](./patterns/synthetic-bar-aggregation.cs) | Building a higher timeframe from the main series instead of a second data series that can silently freeze |
| [validation-bugs/stale-log-text.cs](./validation-bugs/stale-log-text.cs) | A log line describing a different order size than the one actually placed |

The full investigations behind these fixes are in the [problem log](../docs/problem-log.md) and the [debugging journal](../docs/debugging-journal.md).
