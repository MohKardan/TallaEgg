using System;
using System.Net;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace TallaEgg.TelegramBot
{
    public class ProxyBotClient
    {
        private static readonly Uri TelegramApiRoot = new("https://api.telegram.org");

        // getUpdates is a long poll, so the .NET default of 100 s is too tight. Every path below
        // reaches this through CreateHttpClient, the exception fallback included: that fallback
        // used to hand TelegramBotClient no HttpClient at all and silently take the 100 s
        // default, and TelegramBotHostedService derives its polling-recovery gap from
        // ITelegramBotClient.Timeout, so landing there moved the down-alert threshold too
        // (issue #199).
        internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Builds the bot's Telegram client and reports, through <paramref name="logger"/>, which
        /// of the four connection paths it took.
        /// </summary>
        /// <remarks>
        /// The report goes to the log rather than to <see cref="Console"/> because the bot is
        /// installed with <c>sc.exe create</c> (issue #70) and a native Windows service has no
        /// console: under the SCM every one of these lines was written to a handle nobody holds.
        /// They are the lines that say whether the bot is proxying, which is the one thing worth
        /// knowing when Telegram is unreachable — and issue #199 was a whole class of damage done
        /// by trusting one of them, so they need to survive to somewhere readable.
        /// </remarks>
        public static ITelegramBotClient CreateWithProxy(string token, ILogger logger) =>
            new TelegramBotClient(token, CreateHttpClient(ResolveHandler(logger)));

        /// <summary>
        /// Works out the handler and reports the choice. Separate from client construction so the
        /// catch below covers only what its message blames: <see cref="TelegramBotClient"/>'s
        /// constructor throws <see cref="ArgumentException"/> on a malformed token, and inside the
        /// try that surfaced as a proxy warning followed by the same exception thrown again from
        /// the fallback — a configuration typo reported as a network problem, twice.
        /// </summary>
        private static HttpClientHandler? ResolveHandler(ILogger logger)
        {
            // Local override: when the machine can reach Telegram directly (e.g. a TUN-mode VPN),
            // the system HTTP proxy can be unstable on long-poll (getUpdates) and drops the
            // connection ("response ended prematurely"). Set BOT_DIRECT_CONNECTION=1 to bypass the
            // proxy and connect directly. Unset (the default) keeps the original proxy behaviour,
            // which the server deployment relies on.
            //
            // Deliberately outside the try below: if resolving the system proxy threw, the catch
            // would fall back to the default handler, which proxies — the one thing this flag is
            // set to avoid.
            if (Environment.GetEnvironmentVariable("BOT_DIRECT_CONNECTION") == "1")
            {
                var (directHandler, directMessage) = ChooseConnection(bypassProxy: true, systemProxy: null);
                // One property rather than a template with holes: the sentence is the artefact
                // here, worded so an operator can act on it and quoted verbatim in
                // NetworkTroubleshooting.md, so the tests pin it verbatim too.
                logger.LogInformation("{TelegramConnection}", directMessage);
                return directHandler;
            }

            try
            {
                var (handler, message) = ChooseConnection(bypassProxy: false, WebRequest.GetSystemWebProxy());
                logger.LogInformation("{TelegramConnection}", message);
                return handler;
            }
            catch (Exception ex)
            {
                // Warning, not Information: this path silently changes how the bot reaches
                // Telegram. The exception is passed rather than just its Message so the file sink
                // keeps the stack trace — under the service there is no console to re-run it in.
                logger.LogWarning(ex,
                    "Could not resolve the system proxy; falling back to the default handler, which may still use the system proxy.");
                return null;
            }
        }

        /// <summary>
        /// Chooses the handler the bot's <see cref="HttpClient"/> is built on, and the one line
        /// that describes the choice. A <see langword="null"/> handler means the stock one is
        /// right and nothing needs configuring.
        /// </summary>
        /// <remarks>
        /// Split out of <see cref="CreateWithProxy"/> so the choice can be asserted: the
        /// <see cref="ITelegramBotClient"/> that method returns exposes neither its handler nor
        /// its proxy settings, which is why the bug in issue #199 — a bypass flag that did not
        /// bypass — survived behind a message claiming otherwise.
        /// </remarks>
        internal static (HttpClientHandler? Handler, string Message) ChooseConnection(
            bool bypassProxy,
            IWebProxy? systemProxy)
        {
            if (bypassProxy)
            {
                // UseProxy = false is what makes this a bypass. The stock handler leaves it true
                // with a null Proxy, which on Windows resolves through HttpClient.DefaultProxy —
                // the same WinInet settings GetSystemWebProxy reads — so it proxies just like the
                // branch below (issue #199).
                return (new HttpClientHandler { UseProxy = false },
                    "🔗 Direct connection (proxy bypassed via BOT_DIRECT_CONNECTION=1)");
            }

            // GetProxy answers with the destination itself when no proxy applies to it, and can
            // answer null. Neither means "send this through a proxy", and reporting either as one
            // is how "🔧 Using proxy: https://api.telegram.org/" — which means the opposite — used
            // to get printed.
            var proxyUri = systemProxy?.GetProxy(TelegramApiRoot);
            if (proxyUri is null || proxyUri == TelegramApiRoot)
            {
                return (null, $"🔧 No system proxy applies to {TelegramApiRoot.Host}");
            }

            return (new HttpClientHandler { Proxy = systemProxy, UseProxy = true },
                $"🔧 Using proxy: {proxyUri}");
        }

        /// <summary>
        /// Builds the bot's <see cref="HttpClient"/> on <paramref name="handler"/>, or on the
        /// stock handler when it is <see langword="null"/>. The single place the long-poll
        /// timeout is applied, so no path can quietly do without it.
        /// </summary>
        internal static HttpClient CreateHttpClient(HttpClientHandler? handler) =>
            new(handler ?? new HttpClientHandler()) { Timeout = RequestTimeout };
    }
}
