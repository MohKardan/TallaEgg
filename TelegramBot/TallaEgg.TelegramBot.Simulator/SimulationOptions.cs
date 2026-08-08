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
    /// Telegram ids for simulated users start here. High enough that it can never collide
    /// with a real Telegram user id, and low enough to leave room below it for admin/owner
    /// test ids if ever needed.
    /// </summary>
    public const long TelegramIdBase = 900_000_000;

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
