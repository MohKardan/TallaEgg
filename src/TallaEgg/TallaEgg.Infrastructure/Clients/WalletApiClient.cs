using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Json;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.Core.Responses.Order;

namespace TallaEgg.Infrastructure.Clients;

/// <summary>
/// HTTP client for communicating with Wallet service
/// HTTP client for the Wallet service.
/// </summary>
public class WalletApiClient : IWalletApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WalletApiClient> _logger;
    private readonly Uri _walletApiUrl;

    /// <summary>
    /// Builds its own <see cref="HttpClient"/> for callers outside a DI container. The logger is
    /// optional only so existing call sites keep compiling; pass one. Without it the field falls
    /// back to <see cref="NullLogger{T}"/> rather than staying null, because every log call in
    /// this class is unconditional and a null field would turn a successful lock into a
    /// <see cref="NullReferenceException"/>.
    /// </summary>
    /// <param name="apiUrl">
    /// The value of <c>WalletApiUrl</c> from this host's own configuration section. Guarded, not
    /// defaulted: the bot and the simulator both reach this constructor through
    /// <c>TelegramBotOptions</c>, where an absent key arrives as null (issue #205).
    /// </param>
    public WalletApiClient(string? apiUrl, ILogger<WalletApiClient>? logger = null)
    {
        _logger = logger ?? NullLogger<WalletApiClient>.Instance;

        var handler = new HttpClientHandler();
#if DEBUG
        // DEV ONLY: accept self-signed certs for local inter-service calls.
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
        _httpClient = new HttpClient(handler);

        _walletApiUrl = ConfigurationGuard.RequireAbsoluteHttpUri(apiUrl, "WalletApiUrl");
        _httpClient.BaseAddress = _walletApiUrl;
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", APIKeyConstant.TallaEggApiKey);
    }
    public WalletApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<WalletApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Guarded rather than defaulted for the reason in ConfigurationGuard: the old fallback
        // was the address this client actually used in Orders.Api, so a missing key started the
        // service against a host nobody had configured (issue #205).
        _walletApiUrl = ConfigurationGuard.RequireUri(configuration, "WalletApiUrl");

        // Configure HttpClient base address
        _httpClient.BaseAddress = _walletApiUrl;
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", APIKeyConstant.TallaEggApiKey);
    }

    /// <summary>
    /// Lock balance for order placement
    /// Locks balance when placing an order.
    /// </summary>
    public async Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(
        Guid userId,
        string asset,
        decimal amount)
    {
        try
        {
            var request = new WalletRequest
            {
                UserId = userId,
                Asset = asset,
                Amount = amount
            };

            var json = JsonSerializer.Serialize(request, ApiJson.RequestOptions);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/wallet/lockBalance", stringContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to lock balance for user {UserId}, asset {Asset}, amount {Amount}. Status: {Status}, Response: {Response}",
                    userId, asset, amount, response.StatusCode, responseContent);

                // Try to extract error message from response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<WalletDTO>>(responseContent, ApiJson.ResponseOptions);
                    return (false, errorResponse?.Message ?? "خطا در قفل کردن موجودی", null);
                }
                catch
                {
                    return (false, "خطا در قفل کردن موجودی", null);
                }
            }

            var apiResponse = JsonSerializer.Deserialize<ApiResponse<WalletDTO>>(responseContent, ApiJson.ResponseOptions);

            if (apiResponse?.Success == true)
            {
                _logger.LogInformation("Successfully locked {Amount} {Asset} for user {UserId}",
                    amount, asset, userId);
                return (true, apiResponse.Message ?? "موجودی با موفقیت قفل شد", apiResponse.Data);
            }
            else
            {
                return (false, apiResponse?.Message ?? "خطا در قفل کردن موجودی", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking balance for user {UserId}, asset {Asset}, amount {Amount}",
                userId, asset, amount);
            return (false, "خطا در ارتباط با سرویس کیف پول", null);
        }
    }

    /// <summary>
    /// Unlock balance when order is cancelled
    /// Releases locked balance when an order is cancelled.
    /// </summary>
    public async Task<(bool Success, string Message)> UnlockBalanceAsync(
        Guid userId,
        string asset,
        decimal amount)
    {
        try
        {
            // Note: This endpoint might need to be implemented in Wallet service
            // Calls the wallet's unlock endpoint.
            var request = new WalletRequest
            {
                UserId = userId,
                Asset = asset,
                Amount = amount
            };

            var json = JsonSerializer.Serialize(request, ApiJson.RequestOptions);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Assuming there's an unlock endpoint - if not, we might need to implement it
            var response = await _httpClient.PostAsync("api/wallet/unlockBalance", stringContent);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully unlocked {Amount} {Asset} for user {UserId}",
                    amount, asset, userId);
                return (true, "موجودی با موفقیت آزاد شد");
            }
            else
            {
                _logger.LogWarning("Failed to unlock balance for user {UserId}, asset {Asset}, amount {Amount}. Status: {Status}",
                    userId, asset, amount, response.StatusCode);
                return (false, "خطا در آزاد کردن موجودی");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking balance for user {UserId}, asset {Asset}, amount {Amount}",
                userId, asset, amount);
            return (false, "خطا در ارتباط با سرویس کیف پول");
        }
    }

    /// <summary>
    /// Validate if user has sufficient balance for order
    /// Whether the user has enough balance for an order.
    /// TODO: this should take the quantity as an argument.
    /// valume = price * amount
    /// </summary>
    public async Task<(bool Success, string Message, bool HasSufficientBalance)> ValidateBalanceAsync(
        Guid userId,
        string asset,
        decimal valume)
    {
        try
        {

            var (balanceSuccess, balanceMessage, balance) = await GetBalanceAsync(userId, asset);

            if (balanceSuccess)
            {
                bool HasSufficientBalance = balance >= valume;
                return (true, "چک کردن موجودی", HasSufficientBalance);
            }
            else
            {
                return (false, "خطا در تجزیه پاسخ سرویس", false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating balance for user {UserId}, asset {Asset}, amount {Amount}",
                userId, asset, valume);
            return (false, "خطا در ارتباط با سرویس کیف پول", false);
        }
    }
    /// <summary>
    /// Checks a user's credit and balance before an order is placed.
    /// 
    /// </summary>
    /// <param name="userId">User id in our system.</param>
    /// <param name="symbol">
    /// The trading symbol, which names two assets.
    /// Trading Pair: Base Asset / Quote Asset
    /// </param>
    /// <param name="amount">
    /// The quantity the user intends to buy or sell.
    /// Quantity
    /// </param>
    /// <param name="price">
    /// The price, denominated in the quote currency.
    /// Quote Asset
    /// </param>
    /// <returns>
    /// Success is true when the check itself ran without error.
    /// HasSufficientCreditAndBalanceBase is true when the user has enough credit and balance in the
    /// base asset to place a sell order.
    /// HasSufficientCreditAndBalanceQuote is true when they have enough in the quote asset to place
    /// a buy order.
    /// </returns>
    public async Task<(
                        bool Success,
                        string Message,
                        bool HasSufficientCreditAndBalanceBase,
                        bool HasSufficientCreditAndBalanceQuote
        )> 
        ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price)
    {
        try
        {
            var baseAsset = symbol.Split('/')[0];
            var quoteAsset = symbol.Split('/')[1];

            // Read the user's various balances.
            var reads = new[]
            {
                await ReadBalanceAsync(userId, baseAsset),
                await ReadBalanceAsync(userId, "CREDIT_" + baseAsset),
                await ReadBalanceAsync(userId, quoteAsset),
                await ReadBalanceAsync(userId, "CREDIT_" + quoteAsset)
            };

            // A balance we could not read is not a balance of zero (issue #290). Every one of these
            // rows used to be written down as 0 when its read failed, and the method still answered
            // Success — so a wallet outage arrived at every caller as "this customer has nothing",
            // and the customer was told their funds were short and to go and top up.
            //
            // An absent wallet is different and stays zero: the customer really does hold none of
            // that asset. Only IRT, MAUA and CREDIT_MAUA exist at registration, so refusing to
            // decide whenever a row is missing would refuse every trade on every other symbol.
            // Array.Exists, not "did Find return something" — the default of a value tuple has
            // Outcome == Read only because Read happens to be the zero enum member, and reordering
            // the enum would silently invert the test.
            if (Array.Exists(reads, r => r.Outcome == BalanceRead.Unavailable))
            {
                var unavailable = Array.Find(reads, r => r.Outcome == BalanceRead.Unavailable);

                _logger?.LogWarning(
                    "Balance check for user {UserId} on {Symbol} could not read a balance, so it reports nothing rather than zero — {Message}",
                    userId, symbol, unavailable.Message);

                return (false, TallaEgg.Core.RefusalMessages.BalanceCheckFailed, false, false);
            }

            var spotBaseAssetBalance = reads[0].Balance;
            var creditBaseAssetBalance = reads[1].Balance;
            var spotQuoteAssetBalance = reads[2].Balance;
            var creditQuoteAssetBalance = reads[3].Balance;

            return (
                true,
                "اعتبار و موجودی کاربر بررسی شد",
                (spotBaseAssetBalance + creditBaseAssetBalance) + 
                (creditQuoteAssetBalance / price) >= amount,
                (spotQuoteAssetBalance + creditQuoteAssetBalance) +
                (creditBaseAssetBalance * price) >= amount * price
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error while validating a balance.");
            return (false, "خطا در ارتباط با سرویس کیف پول", false, false);
        }
    }

    public async Task<TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>> GetUserWalletsBalanceAsync(Guid userId)
    {
        try
        {

            var response = await _httpClient.GetAsync($"api/wallet/balances/{userId}");
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {

                var result = JsonSerializer.Deserialize<ApiResponse<IEnumerable<WalletDTO>>>(respText, ApiJson.ResponseOptions);

                // Deserialize returns null for a literal "null" body. The signature promises a
                // response, so a caller reading .Success on it would get a NullReferenceException
                // instead of a failure it can report.
                return result ?? ApiResponse<IEnumerable<WalletDTO>>.Fail("خطا در دریفات اطلاعات");
            }

            return TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>.Fail("خطا در دریفات اطلاعات");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching the wallet balances of user {UserId}.", userId);
            return TallaEgg.Core.DTOs.ApiResponse<IEnumerable<WalletDTO>>.Fail("خطا در ارتباط با سرور");

        }
    }

    /// <summary>
    /// What a single balance read produced. Three outcomes, not two, because "the customer holds
    /// none of this asset" and "we could not find out" are different facts with different
    /// consequences, and collapsing them is issue #290.
    /// </summary>
    private enum BalanceRead
    {
        /// <summary>The wallet answered with a balance.</summary>
        Read,

        /// <summary>
        /// The customer has never held this asset, so the balance is genuinely zero. Only IRT,
        /// MAUA and CREDIT_MAUA are seeded at registration; every other row appears when something
        /// first writes to it, and reading one before that answers 400.
        /// </summary>
        NoWallet,

        /// <summary>
        /// The read did not happen — unreachable, timed out, a server error, an unreadable body.
        /// Nothing is known about this balance, and nothing may be assumed about it.
        /// </summary>
        Unavailable
    }

    /// <summary>
    /// One asset's balance, with a failed read and an absent wallet both reported as
    /// <c>Success = false</c>.
    /// </summary>
    /// <remarks>
    /// Kept at this shape because it is on <see cref="IWalletApiClient"/>. Callers that need to
    /// tell "no wallet" from "could not read" must not use it —
    /// <see cref="ValidateCreditAndBalanceAsync"/> uses <see cref="ReadBalanceAsync"/> for exactly
    /// that reason (issue #290).
    /// </remarks>
    public async Task<(bool Success, string Message, decimal? balance)> GetBalanceAsync(Guid userId, string asset)
    {
        var (outcome, message, balance) = await ReadBalanceAsync(userId, asset);

        return outcome == BalanceRead.Read
            ? (true, message, balance)
            : (false, message, (decimal?)null);
    }

    private async Task<(BalanceRead Outcome, string Message, decimal Balance)> ReadBalanceAsync(Guid userId, string asset)
    {
        // Input validation
        if (userId == Guid.Empty)
        {
            return (BalanceRead.Unavailable, "شناسه کاربر نامعتبر است.", 0m);
        }

        if (string.IsNullOrWhiteSpace(asset))
        {
            return (BalanceRead.Unavailable, "نوع دارایی مشخص نشده است.", 0m);
        }

        HttpResponseMessage? response = null;
        string? responseContent = null;

        try
        {
            // Create cancellation token with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Make HTTP request with timeout
            response = await _httpClient.GetAsync($"api/wallet/balance/{userId}/{asset}", cts.Token);

            // Read response content
            responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Handle successful response
                try
                {
                    
                    var walletDto = JsonSerializer.Deserialize<ApiResponse<WalletDTO>>(responseContent, ApiJson.ResponseOptions);

                    // A body of "null", or one shaped like the envelope but carrying no wallet,
                    // parses without throwing. Reaching through it would raise a
                    // NullReferenceException, which this JsonException handler does not catch, so
                    // an unreadable payload would escape the method instead of being reported as
                    // one.
                    if (walletDto?.Data is null)
                    {
                        _logger.LogError(
                            "Balance response for user {UserId}, asset {Asset} parsed but carried no wallet.",
                            userId, asset);
                        return (BalanceRead.Unavailable, "خطا در پردازش اطلاعات دریافتی: پاسخ سرور قابل تفسیر نیست.", 0m);
                    }

                    return (BalanceRead.Read, "موجودی دریافت شد.", walletDto.Data.Balance);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Unreadable balance payload for user {UserId}, asset {Asset}.", userId, asset);
                    return (BalanceRead.Unavailable, $"خطا در پردازش اطلاعات دریافتی: پاسخ سرور قابل تفسیر نیست.", 0m);
                }
            }
            else
            {
                // Handle HTTP error status codes
                var errorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => "کیف پول مورد نظر یافت نشد.",
                    System.Net.HttpStatusCode.Unauthorized => "عدم دسترسی: احراز هویت نشده است.",
                    System.Net.HttpStatusCode.Forbidden => "عدم دسترسی: دسترسی به این عملیات مجاز نیست.",
                    System.Net.HttpStatusCode.BadRequest => "درخواست نامعتبر: پارامترهای ارسالی صحیح نیست.",
                    System.Net.HttpStatusCode.InternalServerError => "خطای داخلی سرور.",
                    System.Net.HttpStatusCode.ServiceUnavailable => "سرویس کیف پول در دسترس نیست.",
                    System.Net.HttpStatusCode.RequestTimeout => "زمان انتظار درخواست به پایان رسید.",
                    System.Net.HttpStatusCode.TooManyRequests => "تعداد درخواست‌های زیاد. لطفاً کمی صبر کنید.",
                    _ => $"خطا در دریافت موجودی: کد خطا {(int)response.StatusCode}"
                };

                // Try to extract detailed error message from response if available
                if (!string.IsNullOrWhiteSpace(responseContent))
                {
                    try
                    {

                        var errorResponse = JsonSerializer.Deserialize<ApiResponse<object>>(responseContent, ApiJson.ResponseOptions);

                        if (errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.Message))
                        {
                            errorMessage = errorResponse.Message;
                        }
                    }
                    catch
                    {
                        // If parsing fails, use the default error message
                    }
                }

                // 400, and only 400, is how this endpoint says the customer has never held the
                // asset: WalletService.GetBalanceAsync throws BusinessRuleException and the
                // endpoint turns that into BadRequest. Only IRT, MAUA and CREDIT_MAUA are seeded
                // and every other row appears when something first writes to it, so that is a real
                // zero and must stay one.
                //
                // 404 is deliberately NOT treated as a missing wallet, though it reads like one.
                // No endpoint in Wallet.Api returns it, so a 404 means the route is not being
                // served — a base address with a path prefix and no trailing slash, a proxy
                // answering mid-deploy, an older build. Calling that "the customer has nothing"
                // would answer Success with four zero balances and tell a funded customer their
                // funds are short: issue #290 surviving its own fix.
                var outcome = response.StatusCode == System.Net.HttpStatusCode.BadRequest
                    ? BalanceRead.NoWallet
                    : BalanceRead.Unavailable;

                return (outcome, errorMessage, 0m);
            }
        }
        catch (HttpRequestException httpEx)
        {
            // Network-related errors
            return (BalanceRead.Unavailable, $"خطا در ارتباط شبکه: {httpEx.Message}", 0m);
        }
        catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
        {
            // Request timeout
            return (BalanceRead.Unavailable, "زمان انتظار درخواست به پایان رسید. لطفاً مجدداً تلاش کنید.", 0m);
        }
        catch (TaskCanceledException)
        {
            // Request was cancelled
            return (BalanceRead.Unavailable, "درخواست لغو شد.", 0m);
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled
            return (BalanceRead.Unavailable, "عملیات لغو شد.", 0m);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Unreadable error payload for user {UserId}, asset {Asset}.", userId, asset);
            // JSON parsing errors
            return (BalanceRead.Unavailable, "خطا در پردازش اطلاعات دریافتی از سرور.", 0m);
        }
        catch (ArgumentException argEx)
        {
            // Invalid arguments
            return (BalanceRead.Unavailable, $"پارامتر نامعتبر: {argEx.Message}", 0m);
        }
        catch (InvalidOperationException invOpEx)
        {
            // Invalid operation state
            return (BalanceRead.Unavailable, $"عملیات غیرمجاز: {invOpEx.Message}", 0m);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error while fetching a balance.");
            // Catch-all for any other unexpected exceptions
            return (BalanceRead.Unavailable, "خطای غیرمنتظره در ارتباط با سرور", 0m);
        }
        finally
        {
            // Cleanup resources if needed
            response?.Dispose();
        }
    }

    /// <summary>
    /// Turns a non-success wallet response into a failure that still carries the reason.
    ///
    /// <para>
    /// The endpoints answer a rejected request with 400 and the <c>BusinessRuleException</c>
    /// message in the body — Persian, written for the person reading it, which is the whole
    /// contract of that exception type. Discarding it and substituting "خطا در بروزرسانی" told an
    /// admin who tried to deduct more credit than a customer has that something had gone wrong with
    /// the system, when what actually happened was the wallet correctly refusing them.
    /// </para>
    ///
    /// <para>
    /// The generic message stays as the fallback, for a body that is empty, not JSON, or carries no
    /// message — a 500 from an unhandled fault, say, whose detail is not for the customer.
    /// </para>
    /// </summary>
    private static ApiResponse<WalletBallanceDTO> FailureFrom(string responseBody)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<ApiResponse<WalletBallanceDTO>>(responseBody, ApiJson.ResponseOptions);

            if (!string.IsNullOrWhiteSpace(parsed?.Message))
                return ApiResponse<WalletBallanceDTO>.Fail(parsed!.Message);
        }
        catch (JsonException)
        {
            // Not JSON at all. Nothing to salvage; fall through to the generic message.
        }

        return ApiResponse<WalletBallanceDTO>.Fail("خطا در بروزرسانی");
    }

    public async Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletRequest request)
    {
        try
        {

            var json = JsonSerializer.Serialize(request, ApiJson.RequestOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"api/wallet/deposit", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {

                var result = JsonSerializer.Deserialize<ApiResponse<WalletBallanceDTO>>(respText, ApiJson.ResponseOptions);

                // Deserialize returns null for a literal "null" body; see the note in
                // GetUserWalletsBalanceAsync.
                return result ?? ApiResponse<WalletBallanceDTO>.Fail("خطا در بروزرسانی");
            }

            return FailureFrom(respText);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error depositing to the wallet of user {UserId}.", request.UserId);
            return TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Fail("خطا در ارتباط با سرور");

        }
    }

    public async Task<TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>> WithdrawalAsync(WalletRequest request)
    {
        try
        {

            var json = JsonSerializer.Serialize(request, ApiJson.RequestOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"api/wallet/withdrawal", content);
            var respText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {

                var result = JsonSerializer.Deserialize<ApiResponse<WalletBallanceDTO>>(respText, ApiJson.ResponseOptions);

                // Deserialize returns null for a literal "null" body; see the note in
                // GetUserWalletsBalanceAsync.
                return result ?? ApiResponse<WalletBallanceDTO>.Fail("خطا در بروزرسانی");
            }

            return FailureFrom(respText);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing from the wallet of user {UserId}.", request.UserId);
            return TallaEgg.Core.DTOs.ApiResponse<WalletBallanceDTO>.Fail("خطا در ارتباط با سرور");

        }
    }
    /// <summary>
    /// Once a trade has executed, its transaction must be recorded and the balances updated.
    ///
    /// The reason a settlement was refused must reach the caller verbatim. Every error used to be
    /// collapsed into a generic message, so the outbox processor never learned what actually went
    /// wrong and stored that useless string in LastError (issue #38).
    /// </summary>
    public async Task<(bool Success, string Message)> TradeTransactionAndBalanceChangeAsync(TradeDto trade)
    {
        try
        {
            var json = JsonSerializer.Serialize(trade, ApiJson.RequestOptions);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/wallet/changeBalance", stringContent);
            var body = await response.Content.ReadAsStringAsync();

            // The endpoint returns an ApiResponse on both success and failure, and the precise
            // settlement message is in its Message field.
            var parsed = TryParseMessage(body);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Trade {TradeId} settled by the wallet service. Symbol: {Symbol}, quantity: {Quantity}, quote: {QuoteQuantity}.",
                    trade.Id, trade.Symbol, trade.Quantity, trade.QuoteQuantity);

                return (true, parsed ?? "تسویهٔ معامله با موفقیت انجام شد.");
            }

            var reason = parsed ?? $"سرویس کیف پول کد {(int)response.StatusCode} برگرداند.";

            _logger.LogWarning(
                "Wallet service rejected settlement of trade {TradeId} with status {StatusCode}: {Reason}",
                trade.Id, (int)response.StatusCode, reason);

            return (false, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error settling trade {TradeId} against the wallet service.", trade.Id);
            return (false, "خطا در ارتباط با سرویس کیف پول");
        }
    }

    /// <summary>
    /// Extracts the message from an ApiResponse body. Returns null when the body is empty or cannot
    /// be parsed, so the caller can supply its own fallback — reading a message must never be what
    /// fails a settlement.
    /// </summary>
    private string? TryParseMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            var apiResponse = JsonSerializer.Deserialize<TallaEgg.Core.DTOs.ApiResponse<string>>(body, ApiJson.ResponseOptions);

            return string.IsNullOrWhiteSpace(apiResponse?.Message) ? null : apiResponse.Message;
        }
        catch (JsonException)
        {
            _logger.LogDebug("Wallet settlement response was not a parsable ApiResponse: {Body}", body);
            return null;
        }
    }

}
