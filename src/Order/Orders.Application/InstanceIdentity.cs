namespace Orders.Application;

/// <summary>
/// Names this running copy of the service, so a claim written to the database says who wrote it.
///
/// Both coordination mechanisms added for issue #160 need an owner: the per-row lease on the
/// outbox, and the leader lease the background loops hold. Sharing one identity means a stuck
/// claim points at a process an operator can actually go and look at.
///
/// The random suffix matters. Machine name and process id alone can repeat — Windows reuses
/// process ids — and a restarted instance inheriting the identity of the one that crashed would
/// consider itself the owner of claims it knows nothing about. A fresh suffix per start makes
/// those claims belong to nobody, so they expire and are picked up normally.
/// </summary>
public sealed class InstanceIdentity
{
    public InstanceIdentity()
        : this($"{Environment.MachineName}#{Environment.ProcessId}#{Guid.NewGuid().ToString("N")[..8]}")
    {
    }

    /// <summary>Explicit identity, so a test can act as two separate instances against one database.</summary>
    public InstanceIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("An instance identity cannot be empty.", nameof(value));

        // Truncated rather than rejected: the column is length-limited, and a machine name long
        // enough to overflow it is not a reason to refuse to start.
        Value = value.Length > MaxLength ? value[..MaxLength] : value;
    }

    /// <summary>Matches the length of the owner columns that store this value.</summary>
    public const int MaxLength = 100;

    public string Value { get; }

    public override string ToString() => Value;
}
