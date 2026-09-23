using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A balance the wallet could not tell us is not a balance of zero (issue #290).
///
/// <para>
/// <c>ValidateCreditAndBalanceAsync</c> read four balances and wrote
/// <c>x.Success ? x.balance : 0</c> for each, then returned <c>Success = true</c> regardless. And
/// <c>GetBalanceAsync</c> catches every network error, timeout and HTTP status and returns
/// <c>(false, message, null)</c> rather than throwing — so its enclosing try/catch almost never
/// fired. A wallet outage therefore arrived at every caller as "this customer has nothing", and the
/// customer was told their funds were short and to visit the gold shop, for a failure that was ours.
/// </para>
///
/// <para>
/// These tests drive the <b>real</b> <see cref="WalletApiClient"/> through a stubbed transport,
/// which is the point. The first attempt at #290 tested a hand-written stub returning
/// <c>Success = false</c> for an outage — a tuple the real client never produces — so three tests
/// passed against behaviour that did not exist and would have stayed green for the life of the bug.
/// Put the test where the defect can live.
/// </para>
///
/// <para>
/// The distinction that makes this safe: a wallet the customer has never held answers <b>400</b>,
/// because only IRT, MAUA and CREDIT_MAUA are seeded and every other asset's row appears when
/// something first writes to it. That is a real zero and must stay one. Treating every failed read
/// as "could not check" would refuse every trade on every other symbol.
/// </para>
/// </summary>
public class WalletUnreachableIsNotZeroBalanceTests
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;   // MAUA/IRT
    private static readonly Guid Customer = Guid.NewGuid();

    /// <summary>Realistic figures: gold near 18,468,073 per gram, so 2.5 g is about 46m toman.</summary>
    private const decimal PricePerGram = 18_468_073m;
    private const decimal Quantity = 2.5m;

    /// <summary>Answers per asset, so one wallet can be missing while another reads normally.</summary>
    private sealed class PerAssetHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _answers = new(StringComparer.OrdinalIgnoreCase);

        public PerAssetHandler Balance(string asset, decimal balance)
        {
            _answers[asset] = () => Json(HttpStatusCode.OK,
                $"{{\"success\":true,\"message\":\"\",\"data\":{{\"asset\":\"{asset}\",\"balance\":{balance},\"lockedBalance\":0,\"updatedAt\":\"2026-09-23T00:00:00Z\"}}}}");
            return this;
        }

        /// <summary>What the endpoint answers for an asset the customer has never held.</summary>
        public PerAssetHandler NoWallet(string asset)
        {
            _answers[asset] = () => Json(HttpStatusCode.BadRequest,
                "{\"success\":false,\"message\":\"کیف پول پیدا نشد\",\"data\":null}");
            return this;
        }

        public PerAssetHandler Unreachable(string asset)
        {
            _answers[asset] = () => throw new HttpRequestException("connection refused");
            return this;
        }

        /// <summary>A 404, which for this API means the route is not there at all.</summary>
        public PerAssetHandler RouteMissing(string asset)
        {
            _answers[asset] = () => Json(HttpStatusCode.NotFound, "");
            return this;
        }

        public PerAssetHandler ServiceUnavailable(string asset)
        {
            _answers[asset] = () => Json(HttpStatusCode.ServiceUnavailable,
                "{\"success\":false,\"message\":\"سرویس در دسترس نیست\",\"data\":null}");
            return this;
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // api/wallet/balance/{userId}/{asset}
            var asset = request.RequestUri!.Segments[^1].TrimEnd('/');

            if (!_answers.TryGetValue(asset, out var answer))
                throw new InvalidOperationException($"the test did not say what {asset} answers");

            return Task.FromResult(answer());
        }
    }

    private static WalletApiClient ClientOver(PerAssetHandler handler) =>
        new(new HttpClient(handler),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["WalletApiUrl"] = "http://wallet.test/api" })
                .Build(),
            NullLogger<WalletApiClient>.Instance);

    /// <summary>Every wallet readable, and comfortably funded on both sides.</summary>
    private static PerAssetHandler WellFunded() => new PerAssetHandler()
        .Balance("MAUA", 100m)
        .Balance("CREDIT_MAUA", 0m)
        .Balance("IRT", 500_000_000m)
        .Balance("CREDIT_IRT", 0m);

    // ── what must not change ────────────────────────────────────────────────────

    [Fact]
    public async Task WhenEveryBalanceReads_TheCheckRunsAndPasses()
    {
        var client = ClientOver(WellFunded());

        var result = await client.ValidateCreditAndBalanceAsync(Customer, Symbol, Quantity, PricePerGram);

        Assert.True(result.Success);
        Assert.True(result.HasSufficientCreditAndBalanceBase);
        Assert.True(result.HasSufficientCreditAndBalanceQuote);
    }

    [Fact]
    public async Task WhenEveryBalanceReadsAndTheFundsAreShort_TheCheckRunsAndRefuses()
    {
        var client = ClientOver(new PerAssetHandler()
            .Balance("MAUA", 0m)
            .Balance("CREDIT_MAUA", 0m)
            .Balance("IRT", 1_000m)
            .Balance("CREDIT_IRT", 0m));

        var result = await client.ValidateCreditAndBalanceAsync(Customer, Symbol, Quantity, PricePerGram);

        Assert.True(result.Success);
        Assert.False(result.HasSufficientCreditAndBalanceQuote);
    }

    /// <summary>
    /// The one that makes the fix safe. Only IRT, MAUA and CREDIT_MAUA are seeded, so a customer
    /// legitimately has no <c>CREDIT_IRT</c> row and the endpoint answers 400. That is a real zero.
    /// Treating it as "could not check" would refuse every trade on every unseeded asset.
    /// </summary>
    [Fact]
    public async Task AWalletTheCustomerHasNeverHeldCountsAsZero_NotAsAFailedCheck()
    {
        var client = ClientOver(new PerAssetHandler()
            .Balance("MAUA", 100m)
            .Balance("CREDIT_MAUA", 0m)
            .Balance("IRT", 500_000_000m)
            .NoWallet("CREDIT_IRT"));

        var result = await client.ValidateCreditAndBalanceAsync(Customer, Symbol, Quantity, PricePerGram);

        Assert.True(result.Success);
        Assert.True(result.HasSufficientCreditAndBalanceQuote);
    }

    // ── what the bug is ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenTheWalletCannotBeReached_TheCheckReportsThatItDidNotRun()
    {
        var client = ClientOver(new PerAssetHandler()
            .Balance("MAUA", 100m)
            .Balance("CREDIT_MAUA", 0m)
            .Unreachable("IRT")
            .Balance("CREDIT_IRT", 0m));

        var result = await client.ValidateCreditAndBalanceAsync(Customer, Symbol, Quantity, PricePerGram);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task WhenTheWalletAnswers503_TheCheckReportsThatItDidNotRun()
    {
        var client = ClientOver(new PerAssetHandler()
            .Balance("MAUA", 100m)
            .Balance("CREDIT_MAUA", 0m)
            .ServiceUnavailable("IRT")
            .Balance("CREDIT_IRT", 0m));

        var result = await client.ValidateCreditAndBalanceAsync(Customer, Symbol, Quantity, PricePerGram);

        Assert.False(result.Success);
    }

    /// <summary>
    /// And it must not answer "you have nothing" on the way out. A caller that reads the two
    /// sufficiency flags without checking <c>Success</c> would otherwise refuse a funded customer.
    /// </summary>
    [Fact]
    public async Task WhenTheWalletCannotBeReached_ItDoesNotAlsoClaimTheFundsAreShort()
    {
        var client = ClientOver(new PerAssetHandler()
            .Unreachable("MAUA")
            .Unreachable("CREDIT_MAUA")
            .Unreachable("IRT")
            .Unreachable("CREDIT_IRT"));

        var result = await client.ValidateCreditAndBalanceAsync(Customer, Symbol, Quantity, PricePerGram);

        Assert.False(result.Success);
        Assert.NotEqual("اعتبار و موجودی کاربر بررسی شد", result.Message);

        // The flags too, not only Success — the name of this test claims both, and a caller that
        // reads them without checking Success is exactly the mistake worth guarding against.
        Assert.False(result.HasSufficientCreditAndBalanceBase);
        Assert.False(result.HasSufficientCreditAndBalanceQuote);
    }

    /// <summary>
    /// 404 reads like "no wallet" and is not.
    ///
    /// <para>
    /// No endpoint in Wallet.Api answers 404 — a missing wallet is 400 — so a 404 means the route
    /// is not being served: a base address with a path prefix and no trailing slash, a proxy
    /// answering mid-deploy, an older build. Classifying it as a missing wallet would answer
    /// Success with four zero balances and tell a funded customer their funds are short, which is
    /// this very issue surviving its own fix. It was classified that way in the first draft.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A404MeansTheRouteIsMissing_NotThatTheCustomerHasNoWallet()
    {
        var client = ClientOver(new PerAssetHandler()
            .Balance("MAUA", 100m)
            .Balance("CREDIT_MAUA", 0m)
            .RouteMissing("IRT")
            .Balance("CREDIT_IRT", 0m));

        var result = await client.ValidateCreditAndBalanceAsync(Customer, Symbol, Quantity, PricePerGram);

        Assert.False(result.Success);
    }

    /// <summary>
    /// An outage on the credit ledger is just as blinding as one on the balance: credit backs a
    /// position in either currency (docs/decisions/004-credit-is-cross-asset.md), so an unreadable
    /// credit row can turn an affordable order into a refused one.
    /// </summary>
    [Fact]
    public async Task AnUnreachableCreditLedgerAlsoStopsTheCheck()
    {
        var client = ClientOver(new PerAssetHandler()
            .Balance("MAUA", 100m)
            .Unreachable("CREDIT_MAUA")
            .Balance("IRT", 500_000_000m)
            .Balance("CREDIT_IRT", 0m));

        var result = await client.ValidateCreditAndBalanceAsync(Customer, Symbol, Quantity, PricePerGram);

        Assert.False(result.Success);
    }
}
