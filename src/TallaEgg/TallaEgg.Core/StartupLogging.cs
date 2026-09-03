using Serilog;

namespace TallaEgg.Core;

/// <summary>
/// Makes a failure during host construction reach the log file rather than only stderr.
/// </summary>
public static class StartupLogging
{
    /// <summary>
    /// Builds an absolute path to <paramref name="fileName"/> in a <c>logs</c> directory beside
    /// the running binary, for a Serilog file sink.
    /// </summary>
    /// <remarks>
    /// Serilog resolves a relative sink path against the process working directory. The four
    /// deployed hosts are installed with <c>sc.exe create</c> (issue #70), which has no option to
    /// set one, so the SCM hands every service <c>C:\Windows\System32</c> — and that is exactly
    /// where all four logs were found on the first real deployment (issue #211).
    ///
    /// <para>
    /// <c>UseWindowsService()</c> does not help here: it points <c>ContentRootPath</c> at
    /// <see cref="AppContext.BaseDirectory"/> but leaves <c>Environment.CurrentDirectory</c>
    /// alone. The sink is also configured before the host exists, so there is no
    /// <c>IHostEnvironment</c> to read yet. <see cref="AppContext.BaseDirectory"/> is the one
    /// anchor available at that point that does not depend on how the process was launched.
    /// </para>
    /// </remarks>
    public static string LogFilePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "logs", fileName);

    /// <summary>
    /// Reports any exception that terminates the process through the configured Serilog sinks.
    /// </summary>
    /// <remarks>
    /// Every service throws before it can serve a request when configuration is missing — that
    /// is the rule <c>AGENT.md</c> states and what <see cref="ConfigurationGuard"/> enforces.
    /// The message names the key and the file to edit, so it is written to be acted on by an
    /// operator with no debugger attached.
    ///
    /// <para>
    /// That operator could not read it. The four deployed hosts — Wallet, Users, Orders and the
    /// bot — are installed with <c>sc.exe</c> and run under <c>UseWindowsService()</c> (issue
    /// #70), and a Windows service has no console: an exception escaping the top-level
    /// statements went to stderr and nowhere else — the same class of problem issue #202 fixed
    /// for the proxy decision. Serilog is configured before anything can throw, so a handler
    /// here puts the reason in the rolling file the operator is told to look at (issue #205).
    ///
    /// <para>
    /// It reports through Serilog's static <see cref="Log"/>, which is what a guard throwing
    /// before the host is built can reach. <c>UseSerilog()</c> only redirects the host's
    /// <c>ILogger&lt;T&gt;</c>, and at that point there is no host.
    /// </para>
    /// </para>
    ///
    /// <para>
    /// The file it reaches is now the one the operator is told to look at: <see cref="LogFilePath"/>
    /// anchors the sink beside the binary, so it no longer follows the working directory the SCM
    /// picks (issue #211).
    /// </para>
    /// </remarks>
    public static void ReportUnhandledExceptionsToLog()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(
                args.ExceptionObject as Exception,
                "The service is terminating on an unhandled exception.");

            // The process is going down either way; flush before it does.
            Log.CloseAndFlush();
        };
    }
}
