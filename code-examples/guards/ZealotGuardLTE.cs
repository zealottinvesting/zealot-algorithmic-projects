using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
    public static class ZealotGuardLTE
    {
        private const int    MaxTradesPerStrategy = 3;
        private const double MaxDailyLoss         = 900.0;

        private static Dictionary<string, int>    _tradeCounts  = new Dictionary<string, int>();
        private static Dictionary<string, string> _symbolOwner  = new Dictionary<string, string>();
        private static DateTime _snapshotDate  = DateTime.MinValue;
        private static double   _dailyStartPnl = 0;
        private static bool     _lossLocked    = false;
        private static Account  _account       = null;

        // Call once from State.DataLoaded in any Acc1 strategy — idempotent,
        // first strategy to call it wins, safe to call from all three.
        public static void SetAccount(Account acct)
        {
            if (_account == null) _account = acct;
        }

        // Call from each strategy's existing "new day" check, passing Time[0].
        // Guarded against double-reset when multiple strategy instances hit
        // their own session-start block on the same day.
        public static void ResetDay(DateTime tradeDate)
        {
            if (_snapshotDate.Date == tradeDate.Date) return;
            _snapshotDate = tradeDate.Date;
            _tradeCounts.Clear();
            _lossLocked = false;
            if (_account != null)
                _dailyStartPnl = _account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
            NinjaTrader.Code.Output.Process(
                "[ZealotGuardLTE] DAY RESET — counts cleared, loss lock cleared, startPnl=" + _dailyStartPnl.ToString("F2"),
                PrintTo.OutputTab1);
        }

        public static bool CanTrade(string strategyName)
        {
            if (_lossLocked) return false;
            int count = _tradeCounts.ContainsKey(strategyName) ? _tradeCounts[strategyName] : 0;
            return count < MaxTradesPerStrategy;
        }

        public static void RegisterTrade(string strategyName)
        {
            if (!_tradeCounts.ContainsKey(strategyName)) _tradeCounts[strategyName] = 0;
            _tradeCounts[strategyName]++;
            NinjaTrader.Code.Output.Process(
                "[ZealotGuardLTE] TRADE REGISTERED strategy=" + strategyName
                + " count=" + _tradeCounts[strategyName] + "/" + MaxTradesPerStrategy,
                PrintTo.OutputTab1);
        }

        // Call every bar from each strategy's BIP==0 block. No auto-close —
        // this only blocks NEW entries; anything already open runs to its own
        // stop/target untouched. Trips once and stays tripped for the rest of
        // the day (cleared again by the next ResetDay call).
        public static void CheckLossLock()
        {
            if (_account == null || _lossLocked) return;
            double currentPnl = _account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
            double todayPnl   = currentPnl - _dailyStartPnl;
            if (todayPnl <= -MaxDailyLoss)
            {
                _lossLocked = true;
                NinjaTrader.Code.Output.Process(
                    "[ZealotGuardLTE] *** LOSS LOCK TRIGGERED *** todayPnl=" + todayPnl.ToString("F2")
                    + " threshold=-" + MaxDailyLoss,
                    PrintTo.OutputTab1);
            }
        }

        // ── Per-symbol cross-strategy position guard ────────────────────────
        // Prevents two strategies on the SAME account from both holding a
        // position in the SAME underlying symbol at once. Call RegisterEntry/
        // RegisterExit from each strategy's OnPositionUpdate override (fires
        // reliably on every position-state transition regardless of whether
        // the position closed via stop, target, EOD flatten, or consistency
        // lock) — not from the entry/exit order calls themselves.

        // Call from OnPositionUpdate when this strategy's position goes
        // Flat -> non-Flat. Records this strategy as the symbol's current
        // owner (root/underlying symbol, e.g. "MGC" — not the full contract
        // name, so different expiries of the same underlying still collide).
        public static void RegisterEntry(string strategyName, string symbol)
        {
            _symbolOwner[symbol] = strategyName;
            NinjaTrader.Code.Output.Process(
                "[ZealotGuardLTE] SYMBOL LOCKED symbol=" + symbol + " owner=" + strategyName,
                PrintTo.OutputTab1);
        }

        // Call from OnPositionUpdate when this strategy's position goes
        // non-Flat -> Flat. Only clears the lock if this strategy is still
        // the recorded owner (defensive against any out-of-order callback).
        public static void RegisterExit(string strategyName, string symbol)
        {
            if (_symbolOwner.ContainsKey(symbol) && _symbolOwner[symbol] == strategyName)
            {
                _symbolOwner.Remove(symbol);
                NinjaTrader.Code.Output.Process(
                    "[ZealotGuardLTE] SYMBOL FREED symbol=" + symbol + " owner=" + strategyName,
                    PrintTo.OutputTab1);
            }
        }

        // Call before any new entry, alongside CanTrade(). True unless a
        // DIFFERENT strategy on this account currently holds an open
        // position in this symbol. A strategy that's blocked here should
        // simply not fire this bar and re-check next bar — it must NOT be
        // treated as "used up" for the day (no _tradedToday-style latch),
        // so it re-enters the instant the account goes flat on that symbol.
        public static bool CanTradeSymbol(string strategyName, string symbol)
        {
            return !_symbolOwner.ContainsKey(symbol) || _symbolOwner[symbol] == strategyName;
        }
    }
}