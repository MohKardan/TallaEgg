using Serilog;

namespace TallaEgg.Core;

/// <summary>
/// Makes a failure during host construction reach the log file rather than only stderr.
/// </summary>
public static class StartupLogging
{
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
    /// That operator could not read it. The services are installed with <c>sc.exe</c> and run
    /// under <c>UseWindowsService()</c> (issue #70), and a Windows service has no console: an
    /// exception escaping the top-level statements went to stderr and nowhere else — the same
    /// class of problem issue #202 fixed for the proxy decision. Serilog is configured before
    /// anything can throw, so a handler here puts the reason in the rolling file the operator
    /// is told to look at (issue #205).
    /// </para>
    ///
    /// <para>
    /// The log path itself is still relative to the working directory, which <c>sc.exe</c> does
    /// not set — tracked on issue #105.
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
