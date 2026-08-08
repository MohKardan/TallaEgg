namespace TallaEgg.TelegramBot.Simulator;

/// <summary>
/// Run-size knobs, all overridable from the command line (e.g. <c>--users 20 --seed 7</c>) so a
/// smaller run can be used while iterating on the simulator itself.
/// </summary>
public sealed class SimulationOptions
{
    public int UserCount { get; init; } = 100;
    public int QuoteCount { get; init; } = 120;
    public int TradeCount { get; init; } = 1000;
    public int Seed { get; init; } = 42;

    /// <summary>
    /// Telegram ids for simulated users start here and count down (more negative per user).
    ///
    /// A first version used a large positive base (900,000,000) on the theory that a real
    /// Telegram user id would never reach it — wrong: modern Telegram user ids run well past
    /// that (a real dev account in this database is 6,389,449,308), and a run's reset step
    /// deleted that account along with its wallets and trade history before this was caught.
    ///
    /// Negative is the one range genuinely guaranteed empty: Telegram user ids are always
    /// positive (negative ids belong to group chats, which don't apply here), and the seeded
    /// bootstrap admin is TelegramId 0. No magic threshold to get wrong, and no future growth
    /// in real Telegram ids can ever reach it.
    /// </summary>
    public const long TelegramIdBase = -1_000_000_000;

    public static SimulationOptions FromArgs(string[] args)
    {
        var users = 100;
        var quotes = 120;
        var trades = 1000;
        var seed = 42;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--users": users = int.Parse(args[++i]); break;
                case "--quotes": quotes = int.Parse(args[++i]); break;
                case "--trades": trades = int.Parse(args[++i]); break;
                case "--seed": seed = int.Parse(args[++i]); break;
            }
        }

        return new SimulationOptions { UserCount = users, QuoteCount = quotes, TradeCount = trades, Seed = seed };
    }
}
