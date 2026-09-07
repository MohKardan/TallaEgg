using TallaEgg.Core.Startup;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A <see cref="DatabaseReadiness"/> that is already open, for tests that construct a background
/// loop directly.
/// </summary>
/// <remarks>
/// Since issue #230 those loops wait on readiness before touching the database, because the
/// migration now runs alongside the host rather than ahead of it. These tests drive their target
/// method directly against an already-migrated in-memory database, so the honest stand-in is a
/// readiness that has already opened — a closed one would hang the loop, which is the point of it.
/// </remarks>
internal static class MigratedDatabase
{
    public static DatabaseReadiness Readiness()
    {
        var readiness = new DatabaseReadiness();
        readiness.MarkReady();
        return readiness;
    }
}
