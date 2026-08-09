namespace SeeSight.SharedKernel.Persistence;

/// <summary>
/// Marks an entity with a soft-delete marker — the owning service's
/// <c>DbContext</c> applies a <c>HasQueryFilter</c> excluding
/// <see cref="DeletedAt"/>-set rows for every entity implementing this.
/// </summary>
public interface ISoftDelete
{
    DateTimeOffset? DeletedAt { get; }
}
