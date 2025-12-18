using System.Collections.ObjectModel;

namespace GitSync;

public abstract class MapperBase
{
    readonly Dictionary<Parts, ICollection<Parts>> toBeAddedOrUpdatedEntries = new(
        new PartsComparer()
    );
    readonly List<Parts> toBeRemovedEntries = [];

    protected void AddOrRemoveInternal(IParts from, Parts to)
    {
        switch (from)
        {
            case Parts toAddOrUpdate:
                if (toAddOrUpdate.Type != to.Type)
                {
                    throw new ArgumentException(
                        $"Cannot map [{toAddOrUpdate.Type}: {toAddOrUpdate.Url}] to [{to.Type}: {to.Url}]. "
                    );
                }

                if (this.toBeRemovedEntries.Contains(to))
                {
                    throw new InvalidOperationException(
                        $"Cannot add this as the target path '{to.Path}' in branch'{to.Branch}' of '{to.Owner}/{to.Repository}' as it's already scheduled for removal."
                    );
                }

                if (!this.toBeAddedOrUpdatedEntries.TryGetValue(toAddOrUpdate, out var parts))
                {
                    parts = [];
                    this.toBeAddedOrUpdatedEntries.Add(toAddOrUpdate, parts);
                }

                parts.Add(to);

                break;

            case Parts.NullParts:
                if (to.Type == TreeEntryTargetType.Tree)
                {
                    throw new NotSupportedException(
                        $"Removing a '{nameof(TreeEntryTargetType.Tree)}' isn't supported."
                    );
                }

                if (this.toBeAddedOrUpdatedEntries.Values.SelectMany(_ => _).Contains(to))
                {
                    throw new InvalidOperationException(
                        $"Cannot remove this as the target path '{to.Path}' in branch '{to.Branch}' of '{to.Owner}/{to.Repository}' as it's already scheduled for addition."
                    );
                }

                if (this.toBeRemovedEntries.Contains(to))
                {
                    return;
                }

                this.toBeRemovedEntries.Add(to);

                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported 'from' type ({from.GetType().FullName})."
                );
        }
    }

    public IEnumerable<KeyValuePair<Parts, IEnumerable<Parts>>> ToBeAddedOrUpdatedEntries =>
        this.toBeAddedOrUpdatedEntries.Select(e => new KeyValuePair<Parts, IEnumerable<Parts>>(
            e.Key,
            e.Value
        ));

    public IEnumerable<Parts> ToBeRemovedEntries =>
        new ReadOnlyCollection<Parts>(this.toBeRemovedEntries);

    public IDictionary<string, IList<Tuple<Parts, IParts>>> Transpose()
    {
        var parts = new Dictionary<string, IList<Tuple<Parts, IParts>>>();

        foreach (var kvp in this.toBeAddedOrUpdatedEntries)
        {
            var source = kvp.Key;

            foreach (var destination in kvp.Value)
            {
                var orb = $"{destination.Owner}/{destination.Repository}/{destination.Branch}";

                if (!parts.TryGetValue(orb, out var items))
                {
                    items = [];
                    parts.Add(orb, items);
                }

                items.Add(new(destination, source));
            }
        }

        foreach (var destination in this.toBeRemovedEntries)
        {
            var orb = $"{destination.Owner}/{destination.Repository}/{destination.Branch}";

            if (!parts.TryGetValue(orb, out var items))
            {
                items = [];
                parts.Add(orb, items);
            }

            items.Add(new(destination, Parts.Empty));
        }

        return parts;
    }
}
