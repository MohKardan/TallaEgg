namespace TallaEgg.Core.Enums.Wallet
{
    /// <summary>
    /// The wallet types the system recognises.
    /// </summary>
    public enum WalletType
    {
        /// <summary>
        /// Spot wallet, for immediate trading. Its balance can be withdrawn straight away.
        /// </summary>
        Spot = 1,

        /// <summary>
        /// Credit wallet: credit extended by the exchange. Usable for trading but not withdrawable,
        /// and repaid through trades or a cash deposit.
        /// </summary>
        Credit = 2,

        /// <summary>
        /// Margin wallet, for leveraged trading against collateral and credit.
        /// </summary>
        Margin = 3,

        /// <summary>
        /// Futures wallet, for dated contracts.
        /// </summary>
        Futures = 4,

        /// <summary>
        /// Savings wallet: balance locked at a fixed return.
        /// </summary>
        Savings = 5,

        /// <summary>
        /// Staking wallet for proof-of-stake networks: balance locked to earn network rewards.
        /// </summary>
        Staking = 6,

        /// <summary>
        /// P2P wallet for direct user-to-user trading, acting as temporary escrow.
        /// </summary>
        P2P = 7,

        /// <summary>
        /// DeFi wallet, connecting to external decentralised protocols.
        /// </summary>
        DeFi = 8,

        /// <summary>
        /// Blocked wallet, for balances under review: restricted or temporarily disabled.
        /// </summary>
        Locked = 9,

        /// <summary>
        /// Rewards wallet, holding balance earned from loyalty and referral programmes.
        /// </summary>
        Reward = 10,

        /// <summary>
        /// Guarantee wallet, holding balance locked as security for trades.
        /// </summary>
        Collateral = 11
    }
}