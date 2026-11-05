// Two guards added after a live incident on the first day of a new basket.
//
// What happened: a strategy was attached to the wrong instrument's chart, noticed,
// then disabled and re-enabled while it held a position. Disabling a managed
// NinjaTrader strategy cancels its own stop and target, so the account was left
// holding a position with no stop at all. It was closed by hand for -$896.
// The stop it lost would have capped the loss at -$276.
//
// Fix 1: refuse to start on the wrong instrument at all.
// Fix 2: on every real-time bar, check the ACCOUNT (not the strategy's own view)
//        for a position on this instrument with no working stop, and alert loudly.

private const string RequiredSymbol = "MGC";

protected override void OnStateChange()
{
    // ...
    if (State == State.DataLoaded)
    {
        // Throwing here stops the strategy before it can place a single order.
        if (Instrument.MasterInstrument.Name != RequiredSymbol)
            throw new Exception(Name + ": must run on a " + RequiredSymbol + " chart. Attached: " + Instrument.FullName);
    }
}

private bool nakedAlerted;

private void CheckUnprotected()
{
    // Any open position on this instrument in the account...
    Position naked = null;
    foreach (Position p in Account.Positions)
        if (p.Instrument.FullName == Instrument.FullName && p.MarketPosition != MarketPosition.Flat)
            naked = p;

    // ...is fine as long as some working stop order exists for it.
    if (naked != null)
        foreach (Order o in Account.Orders)
            if (o.Instrument.FullName == Instrument.FullName
                && (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit)
                && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted || o.OrderState == OrderState.Submitted))
                naked = null;

    if (naked == null) { nakedAlerted = false; return; }
    if (nakedAlerted) return;                       // alert once per episode, not every bar
    nakedAlerted = true;

    string msg = "*** UNPROTECTED POSITION *** " + Account.Name + " " + naked.Instrument.FullName + " "
               + naked.MarketPosition + " " + naked.Quantity + " @ " + naked.AveragePrice
               + " has NO working stop -- place a stop or flatten NOW";
    Print(msg);
    Alert(Name + "_Unprotected", Priority.High, msg,
          NinjaTrader.Core.Globals.InstallDir + @"\sounds\Alert4.wav", 60, Brushes.Red, Brushes.White);
}

protected override void OnBarUpdate()
{
    if (State == State.Realtime) CheckUnprotected();
    // ... signal logic ...
}

// The checks found a second problem the same week: after a restart, the log showed
// "must run on a MNQ chart. Attached: MGC" several times in a row. That was Fix 1
// doing its job while the strategies were being re-attached, and it confirmed every
// copy ended up on the right chart before the session opened.
