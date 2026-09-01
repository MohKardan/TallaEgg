using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A host must be able to override a shared-file value with an environment variable or a
/// command-line switch (issue #159).
///
/// <para>
/// Configuration providers are last-wins. Every service registered
/// <c>config/appsettings.global.json</c> — and then the section flattened out of it — <i>after</i>
/// <c>WebApplication.CreateBuilder</c> had already registered the environment-variable and
/// command-line providers, so the file outranked both. The practical effect was that ports, URLs
/// and connection strings could only be changed by hand-editing the one file that holds live
/// credentials and is deliberately untracked (#33): running a second instance, or pointing
/// staging at another database, meant editing secrets on the server.
/// </para>
///
/// <para>
/// <b>Two kinds of test for one rule, deliberately.</b> The behavioural tests below pin what the
/// precedence chain does, but they build that chain themselves — on their own they would keep
/// passing after someone reordered a <c>Program.cs</c> back.
/// <see cref="EveryHost_RegistersTheEnvironment_AfterTheSharedFile"/> is what makes the reordering
/// fail, by reading the host files themselves. Neither half is worth much without the other.
/// </para>
///
/// <para>
/// <b>The two places provider order could not reach (#181).</b> An API host called
/// <c>UseUrls</c> with the file's value unconditionally, and <c>UseUrls</c> writes through
/// <c>UseSetting</c>, which bypasses the provider chain — so the file beat <c>ASPNETCORE_URLS</c>
/// and <c>--urls</c> however the providers were ordered, and a service came up somewhere other
/// than where the host had told it to, without saying so. The bot, meanwhile, had the environment
/// in the right place but not the command line, which <c>Host.CreateDefaultBuilder(args)</c>
/// registers among the defaults, before the shared file. Both are covered below, in the same two
/// halves.
/// </para>
/// </summary>
public class ConfigurationPrecedenceTests
{
    /// <summary>The section name is arbitrary here; only its shape has to match the real file.</summary>
    private const string ApplicationName = "Wallet.Api";

    /// <summary>
    /// The five API hosts, whose provider order this fix changed. Repo-relative, forward-slashed.
    /// </summary>
    public static TheoryData<string> ApiHosts() => new()
    {
        "src/Order/Orders.Api/Program.cs",
        "src/User/Users.Api/Program.cs",
        "src/Wallet/Wallet.Api/Program.cs",
        "src/TallaEgg/TallaEgg.Api/Program.cs",
        "src/Affiliate/Affiliate.Api/Program.cs",
    };

    /// <summary>
    /// The hosts whose configuration a command-line provider feeds: the five APIs, and the bot
    /// since #181.
    ///
    /// <para>
    /// The simulator is deliberately absent. It builds a bare <c>ConfigurationBuilder</c> and
    /// parses its own <c>--users</c>/<c>--trades</c> switches in <c>SimulationOptions.FromArgs</c>,
    /// which a command-line configuration provider has no part in.
    /// </para>
    /// </summary>
    public static TheoryData<string> CommandLineHosts()
    {
        var hosts = ApiHosts();
        hosts.Add("TelegramBot/TallaEgg.TelegramBot.Infrastructure/Program.cs");
        return hosts;
    }

    /// <summary>
    /// Every host that reads the shared file, including the bot and the simulator — which had the
    /// environment provider in the right place already, and are listed so they cannot drift out
    /// of it.
    /// </summary>
    public static TheoryData<string> AllHosts()
    {
        var hosts = ApiHosts();
        hosts.Add("TelegramBot/TallaEgg.TelegramBot.Infrastructure/Program.cs");
        hosts.Add("TelegramBot/TallaEgg.TelegramBot.Simulator/Program.cs");
        return hosts;
    }

    /// <summary>
    /// The acceptance criterion of #159: a connection string set in the environment reaches the
    /// service, and everything the host did not override still comes from the file.
    /// </summary>
    [Fact]
    public void EnvironmentVariable_OverridesTheSharedFile()
    {
        using var sharedFile = new SharedConfigFile(walletDb: "Server=from-the-file;Database=Wallet;");
        using var hostOverride = new EnvironmentVariable(
            "ConnectionStrings__WalletDb", "Server=from-the-host;Database=Wallet;");

        var configuration = BuildLikeAHost(sharedFile.Path);

        Assert.Equal("Server=from-the-host;Database=Wallet;", configuration.GetConnectionString("WalletDb"));

        // The half that is easy to break while fixing the other half: the file is still the
        // source of truth for every value the host said nothing about.
        Assert.Equal("http://localhost:5140", configuration["OrdersApiUrl"]);
    }

    /// <summary>
    /// The same for the command line, which <c>CreateBuilder(args)</c> also registered too early
    /// to be useful.
    /// </summary>
    [Fact]
    public void CommandLineArgument_OverridesTheSharedFile()
    {
        using var sharedFile = new SharedConfigFile(walletDb: "Server=from-the-file;Database=Wallet;");

        var configuration = BuildLikeAHost(
            sharedFile.Path,
            args: new[] { "--ConnectionStrings:WalletDb=Server=from-the-command-line;Database=Wallet;" });

        Assert.Equal("Server=from-the-command-line;Database=Wallet;", configuration.GetConnectionString("WalletDb"));
    }

    /// <summary>
    /// A port is the other value #159 named, and it is reached differently: the hosts read
    /// <c>Services:{ApplicationName}:Urls</c> straight off the section rather than through the
    /// flattened copy, so the override has to be written at the nested path.
    /// </summary>
    [Fact]
    public void EnvironmentVariable_OverridesTheConfiguredPort()
    {
        using var sharedFile = new SharedConfigFile(walletDb: "Server=from-the-file;Database=Wallet;");
        using var hostOverride = new EnvironmentVariable(
            $"Services__{ApplicationName}__Urls__0", "http://localhost:61000");

        var configuration = BuildLikeAHost(sharedFile.Path);
        var urls = configuration.GetSection($"Services:{ApplicationName}:Urls").Get<string[]>();

        Assert.Equal(new[] { "http://localhost:61000" }, urls);
    }

    /// <summary>
    /// The negative control. Registering the shared file last is what the services used to do,
    /// and it is what the tests above have to be ruling out — without this they could pass under
    /// either provider order for some reason unrelated to precedence.
    /// </summary>
    [Fact]
    public void SharedFileRegisteredLast_OutranksTheEnvironment()
    {
        using var sharedFile = new SharedConfigFile(walletDb: "Server=from-the-file;Database=Wallet;");
        using var hostOverride = new EnvironmentVariable(
            "ConnectionStrings__WalletDb", "Server=from-the-host;Database=Wallet;");

        var builder = new ConfigurationBuilder();
        builder.AddEnvironmentVariables();
        AddSharedFileAndItsFlattenedSection(builder, sharedFile.Path);

        Assert.Equal("Server=from-the-file;Database=Wallet;", builder.Build().GetConnectionString("WalletDb"));
    }

    /// <summary>
    /// The first half of #181. <c>ASPNETCORE_URLS</c> is the spelling a deployment script, a
    /// service definition or a container image reaches for, and it did nothing at all: the file's
    /// address was applied unconditionally through <c>UseUrls</c>, so the service started,
    /// reported healthy, and listened somewhere other than where it had been told.
    /// </summary>
    [Fact]
    public void AspNetCoreUrls_OutranksTheConfiguredUrls()
    {
        using var sharedFile = new SharedConfigFile(walletDb: "Server=from-the-file;Database=Wallet;");
        using var hostAddress = new EnvironmentVariable("ASPNETCORE_URLS", "http://localhost:61001");

        Assert.Equal("http://localhost:61001", ResolveListenAddressLikeAnApiHost(sharedFile));
    }

    /// <summary>
    /// The same through the switch. It reaches the host by a different provider than the
    /// environment does, and both land on <see cref="WebHostDefaults.ServerUrlsKey"/>, which is
    /// what the guard reads.
    /// </summary>
    [Fact]
    public void UrlsSwitch_OutranksTheConfiguredUrls()
    {
        using var sharedFile = new SharedConfigFile(walletDb: "Server=from-the-file;Database=Wallet;");
        using var noHostAddress = new EnvironmentVariable("ASPNETCORE_URLS", null);

        var listenAddress = ResolveListenAddressLikeAnApiHost(
            sharedFile, args: new[] { "--urls", "http://localhost:61002" });

        Assert.Equal("http://localhost:61002", listenAddress);
    }

    /// <summary>
    /// The half that is easy to break while fixing the other one, and the regression this change
    /// actually risks: with no address named by the host — every host in this system today — the
    /// file still decides where the service listens.
    /// </summary>
    [Fact]
    public void ConfiguredUrls_StillApply_WhenTheHostNamedNoAddress()
    {
        using var sharedFile = new SharedConfigFile(walletDb: "Server=from-the-file;Database=Wallet;");
        using var noHostAddress = new EnvironmentVariable("ASPNETCORE_URLS", null);

        Assert.Equal("http://localhost:60933", ResolveListenAddressLikeAnApiHost(sharedFile));
    }

    /// <summary>
    /// The negative control for the three above. Applying the file's address unconditionally, as
    /// the hosts did before #181, and the address the host named is gone — which is the whole bug,
    /// and what rules out the three passing for a reason unrelated to the guard.
    /// </summary>
    [Fact]
    public void UnconditionalUseUrls_OutranksTheAddressTheHostNamed()
    {
        using var sharedFile = new SharedConfigFile(walletDb: "Server=from-the-file;Database=Wallet;");
        using var hostAddress = new EnvironmentVariable("ASPNETCORE_URLS", "http://localhost:61003");

        var listenAddress = ResolveListenAddressLikeAnApiHost(sharedFile, guardTheAddressTheHostNamed: false);

        Assert.Equal("http://localhost:60933", listenAddress);
    }

    /// <summary>
    /// The regression guard: the environment provider has to be registered after the shared file
    /// <i>and</i> after the section flattened out of it, in the host files themselves. The
    /// flattening is the easy one to miss — it copies the file's values onto the root keys the
    /// services actually read, so a chain that put the environment between the two would still be
    /// overridden.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllHosts))]
    public void EveryHost_RegistersTheEnvironment_AfterTheSharedFile(string hostProgram)
    {
        var code = ReadHostProgram(hostProgram);

        AssertRegisteredLast(code, hostProgram, "AddEnvironmentVariables", "AddJsonFile");
        AssertRegisteredLast(code, hostProgram, "AddEnvironmentVariables", "AddInMemoryCollection");
    }

    /// <summary>
    /// The command line, for the five APIs and — since #181 — the bot.
    ///
    /// <para>
    /// The bot's <c>Host.CreateDefaultBuilder(args)</c> registers a command-line provider among
    /// the defaults, which run before <c>ConfigureAppConfiguration</c> adds the shared file, so
    /// the file outranked <c>--Key=value</c> there. #159 was about the environment, which the bot
    /// already had in the right place, so the gap outlived it. Nothing in the bot reads a setting
    /// from <c>args</c> today — the inconsistency is what is being fixed, before something does.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(CommandLineHosts))]
    public void EveryHostThatParsesTheCommandLine_RegistersIt_AfterTheSharedFile(string hostProgram)
    {
        var code = ReadHostProgram(hostProgram);

        AssertRegisteredLast(code, hostProgram, "AddCommandLine", "AddJsonFile");
        AssertRegisteredLast(code, hostProgram, "AddCommandLine", "AddInMemoryCollection");
    }

    /// <summary>
    /// The port override rests on a second ordering that no behavioural test can reach: the hosts
    /// capture <c>serviceSection</c> early but read <c>Urls</c> off it late, and a
    /// <see cref="IConfigurationSection"/> reads through to its root on every access, so the read
    /// picks up providers registered after the capture. Move the <c>UseUrls</c> block up beside
    /// the flattening it reads from and the port override dies silently — with every other test
    /// in this file still green.
    /// </summary>
    [Theory]
    [MemberData(nameof(ApiHosts))]
    public void EveryApiHost_ReadsItsUrls_AfterTheEnvironmentIsRegistered(string hostProgram)
    {
        var code = ReadHostProgram(hostProgram);

        AssertRegisteredLast(code, hostProgram, "GetSection(\"Urls\")", "AddEnvironmentVariables");
        AssertRegisteredLast(code, hostProgram, "UseUrls", "AddEnvironmentVariables");
    }

    /// <summary>
    /// The source half of the <c>UseUrls</c> fix (#181): the file's address may only be applied
    /// when the host named none. The behavioural tests above build that condition themselves, so
    /// on their own they would stay green after a host went back to calling <c>UseUrls</c>
    /// unconditionally — and because that call bypasses the provider chain, no other test in this
    /// file would notice either.
    /// </summary>
    [Theory]
    [MemberData(nameof(ApiHosts))]
    public void EveryApiHost_AppliesTheConfiguredUrls_OnlyWhenTheHostNamedNoAddress(string hostProgram)
    {
        var code = ReadHostProgram(hostProgram);

        var guard = code.LastIndexOf("WebHostDefaults.ServerUrlsKey", StringComparison.Ordinal);
        var useUrls = code.LastIndexOf("UseUrls", StringComparison.Ordinal);

        Assert.True(guard >= 0,
            $"{hostProgram} must read WebHostDefaults.ServerUrlsKey before applying the configured Urls, " +
            "or ASPNETCORE_URLS and --urls are silently ignored again (#181).");
        Assert.True(useUrls > guard,
            $"{hostProgram} reaches UseUrls before it checks WebHostDefaults.ServerUrlsKey. UseUrls writes " +
            "through UseSetting, which bypasses the configuration providers, so an unguarded call makes the " +
            "file's address beat the host's whatever the provider order is (#181).");
    }

    /// <summary>
    /// Boots configuration the way an API host does, and returns the address that host would
    /// listen on.
    ///
    /// <para>
    /// A real <see cref="WebApplicationBuilder"/> rather than a hand-built chain, because the
    /// question is entirely about providers <c>WebApplication.CreateBuilder</c> registers — the
    /// <c>ASPNETCORE_</c>-prefixed environment and the command line, both of which land on
    /// <see cref="WebHostDefaults.ServerUrlsKey"/> — and about <c>UseUrls</c> writing back to that
    /// same key through <c>UseSetting</c>. A model of that chain could model it wrong and still
    /// pass.
    /// </para>
    /// </summary>
    /// <param name="guardTheAddressTheHostNamed">
    /// <c>false</c> reproduces the unconditional call the hosts made before #181, for the negative
    /// control.
    /// </param>
    private static string? ResolveListenAddressLikeAnApiHost(
        SharedConfigFile sharedFile,
        string[]? args = null,
        bool guardTheAddressTheHostNamed = true)
    {
        args ??= Array.Empty<string>();

        // Content root is the throwaway file's own directory, so that no appsettings.json sitting
        // beside the test assembly can take part in the answer.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = Environments.Production,
            ContentRootPath = sharedFile.Directory,
        });

        AddSharedFileAndItsFlattenedSection(builder.Configuration, sharedFile.Path);
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddCommandLine(args);

        var serviceSection = builder.Configuration.GetSection($"Services:{ApplicationName}");
        var urls = serviceSection.GetSection("Urls").Get<string[]>();
        if ((!guardTheAddressTheHostNamed || string.IsNullOrWhiteSpace(builder.Configuration[WebHostDefaults.ServerUrlsKey]))
            && urls is { Length: > 0 })
        {
            builder.WebHost.UseUrls(urls);
        }

        return builder.Configuration[WebHostDefaults.ServerUrlsKey];
    }

    /// <summary>
    /// Builds configuration the way the five API hosts do, in their order: the shared file, then
    /// the service's own section flattened into the root, then the host's own overrides.
    /// </summary>
    private static IConfigurationRoot BuildLikeAHost(string sharedConfigPath, string[]? args = null)
    {
        var builder = new ConfigurationBuilder();
        AddSharedFileAndItsFlattenedSection(builder, sharedConfigPath);
        builder.AddEnvironmentVariables();
        builder.AddCommandLine(args ?? Array.Empty<string>());
        return builder.Build();
    }

    /// <summary>
    /// The part of every host's startup that has to be reproduced faithfully for the precedence
    /// question to mean anything — the flattening writes the file's values to root-level keys, so
    /// it, and not only the file, is what an override has to beat.
    /// </summary>
    private static void AddSharedFileAndItsFlattenedSection(IConfigurationBuilder builder, string sharedConfigPath)
    {
        builder.AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: false);

        var section = builder.Build().GetSection($"Services:{ApplicationName}");
        var prefix = $"Services:{ApplicationName}:";
        var flattened = section.AsEnumerable(true)
            .Where(pair => pair.Value is not null)
            .Select(pair => new KeyValuePair<string, string?>(
                pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? pair.Key[prefix.Length..] : pair.Key,
                pair.Value))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key));

        builder.AddInMemoryCollection(flattened);
    }

    /// <summary>
    /// Source of a host's <c>Program.cs</c> with comment lines removed, so that a comment naming
    /// a provider — this fix added several — cannot satisfy or defeat an ordering assertion.
    /// </summary>
    private static string ReadHostProgram(string repoRelativePath)
    {
        var path = Path.Combine(FindRepositoryRoot(), repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Host program not found: {repoRelativePath}");

        var code = File.ReadAllLines(path)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

        return string.Join(Environment.NewLine, code);
    }

    /// <summary>
    /// Asserts that the last occurrence of <paramref name="expectedLast"/> comes after the last
    /// occurrence of <paramref name="expectedFirst"/>.
    /// </summary>
    private static void AssertRegisteredLast(string code, string hostProgram, string expectedLast, string expectedFirst)
    {
        var last = code.LastIndexOf(expectedLast, StringComparison.Ordinal);
        var first = code.LastIndexOf(expectedFirst, StringComparison.Ordinal);

        Assert.True(first >= 0, $"{hostProgram} no longer contains {expectedFirst} — this test is out of date.");
        Assert.True(last > first,
            $"{hostProgram} must reach {expectedLast} after {expectedFirst}, or the shared config file " +
            "wins over the host again (#159): configuration providers are last-wins, and a section reads " +
            "through to whatever providers exist at the moment it is read.");
    }

    /// <summary>
    /// Walks up from the test assembly to the directory holding <c>TallaEgg.sln</c>. The same
    /// anchor <c>SolutionMembershipTests</c> uses, and duplicated rather than shared because
    /// extracting it would mean editing a test this change has no business touching.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TallaEgg.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return dir.FullName;
    }

    /// <summary>
    /// A throwaway file shaped like <c>config/appsettings.global.json</c>: a
    /// <c>Services:{ApplicationName}</c> section holding what a service reads. Real values are
    /// never involved — the actual file holds live credentials, and CI does not have one.
    /// </summary>
    private sealed class SharedConfigFile : IDisposable
    {
        public SharedConfigFile(string walletDb)
        {
            Directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tallaegg-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(Directory);
            Path = System.IO.Path.Combine(Directory, "appsettings.global.json");
            File.WriteAllText(Path, $$"""
                {
                  "Services": {
                    "{{ApplicationName}}": {
                      "Urls": [ "http://localhost:60933" ],
                      "OrdersApiUrl": "http://localhost:5140",
                      "ConnectionStrings": { "WalletDb": "{{walletDb}}" }
                    }
                  }
                }
                """);
        }

        public string Path { get; }

        /// <summary>
        /// The file's own directory, which the tests hand a host as its content root: a directory
        /// with nothing else in it cannot contribute an <c>appsettings.json</c> of its own.
        /// </summary>
        public string Directory { get; }

        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }

    /// <summary>
    /// Sets a process environment variable for the length of a test and puts back whatever was
    /// there. Nothing else in this suite reads the environment, but a test that leaks one fails
    /// somewhere else entirely, and those are expensive to find.
    ///
    /// <para>
    /// A null <c>value</c> removes the variable instead of setting it, which is how the tests that
    /// assert the file still supplies the listen address make sure the machine running them has
    /// not named one (#181).
    /// </para>
    /// </summary>
    private sealed class EnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _original;

        public EnvironmentVariable(string name, string? value)
        {
            _name = name;
            _original = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
    }
}
