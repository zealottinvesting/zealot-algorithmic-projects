// Synthetic higher-timeframe bar aggregation
//
// Problem this solves: a NinjaTrader secondary series added via
// AddDataSeries() can silently stop receiving live bars under certain
// trading-hours configurations, while everything else in the strategy
// looks normal — see docs/debugging-journal.md for the full incident
// writeup. Rather than depend on the platform to deliver a reliable
// secondary series, this pattern rebuilds the higher timeframe directly
// from the primary series' own bars, which is already confirmed to be
// live. Where the timeframes divide evenly (e.g. a 5-minute primary into
// 30-minute bars, a 6:1 ratio), this is a safe substitute for
// AddDataSeries() on that timeframe.
//
// Below: builds synthetic OHLC bars from primary-series bars, then
// computes a rolling ATR comparison (current period's average true range
// vs. the prior period's) on the synthetic series. AtrPeriod and AtrMult
// are the strategy's own tunable inputs — define them however fits your
// strategy (NinjaScriptProperty, plain field, etc.); no specific values
// are prescribed here.

private double   _synOpen        = 0;
private double   _synHigh        = double.MinValue;
private double   _synLow         = double.MaxValue;
private double   _synClose       = 0;
private DateTime _synBarStart    = DateTime.MinValue;
private bool     _synBarValid    = false;
private Series<double> _synAtrSeries;   // stores each completed synthetic bar's true range
private double   _synPrevClose   = 0;
private bool     _havePrevClose  = false;

// Call once from State.DataLoaded:
//   _synAtrSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);

// Synthetic 30m bar builder — replaces an unreliable secondary series.
// Primary bars aggregate cleanly into 30m (6:1 for a 5m primary), aligned
// to wall-clock half-hour boundaries.
{
    DateTime barTime = Time[0];
    DateTime windowStart = new DateTime(barTime.Year, barTime.Month, barTime.Day,
        barTime.Hour, barTime.Minute < 30 ? 0 : 30, 0);

    if (windowStart != _synBarStart)
    {
        // Previous synthetic bar (if any) just completed — finalize it.
        if (_synBarValid)
        {
            double trueRange = _synHigh - _synLow;
            if (_havePrevClose)
            {
                trueRange = Math.Max(trueRange, Math.Abs(_synHigh - _synPrevClose));
                trueRange = Math.Max(trueRange, Math.Abs(_synLow - _synPrevClose));
            }
            _synAtrSeries[0] = trueRange;   // store this bar's true range

            Print("[SyntheticBar] BAR_CLOSED"
                + " windowStart=" + _synBarStart
                + " O=" + _synOpen.ToString("F2")
                + " H=" + _synHigh.ToString("F2")
                + " L=" + _synLow.ToString("F2")
                + " C=" + _synClose.ToString("F2")
                + " trueRange=" + trueRange.ToString("F4"));

            _synPrevClose  = _synClose;
            _havePrevClose = true;
        }

        // Start new synthetic bar
        _synBarStart = windowStart;
        _synOpen     = Open[0];
        _synHigh     = High[0];
        _synLow      = Low[0];
        _synClose    = Close[0];
        _synBarValid = true;
    }
    else
    {
        // Still inside the current window — extend it.
        _synHigh  = Math.Max(_synHigh, High[0]);
        _synLow   = Math.Min(_synLow, Low[0]);
        _synClose = Close[0];
    }
}

// Rolling ATR comparison computed from the synthetic true-range series.
// Only re-evaluate once a synthetic bar has just closed (avoid
// recomputing on every primary-series tick inside the same window).
if (_synAtrSeries.Count >= AtrPeriod + 1)
{
    double atrVal = 0;
    for (int i = 0; i < AtrPeriod; i++)
        atrVal += _synAtrSeries[i];
    atrVal /= AtrPeriod;

    double atrPrev = 0;
    for (int i = 1; i <= AtrPeriod; i++)
        atrPrev += _synAtrSeries[i];
    atrPrev /= AtrPeriod;

    bool atrExpanding = (atrVal > AtrMult * atrPrev);

    Print("[SyntheticBar] ATR_DEBUG"
        + " atrVal=" + atrVal.ToString("F5")
        + " atrPrev=" + atrPrev.ToString("F5")
        + " atrMult=" + AtrMult.ToString("F2")
        + " threshold=" + (AtrMult * atrPrev).ToString("F5")
        + " EXPANDING=" + atrExpanding.ToString().ToUpper());
}
