using System.Diagnostics;

namespace GitSync;

sealed class TargetTree(Parts root)
{
    public readonly Dictionary<string, TargetTree> SubTreesToUpdate = [];
    public readonly Dictionary<string, Tuple<Parts, Parts>> LeavesToCreate = [];
    public readonly Dictionary<string, Parts> LeavesToDrop = [];
    public readonly Parts Current = root;
    public static string EmptyTreeSha = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    public void Add(Parts destination, Parts source) =>
        this.AddOrRemove(destination, source, 0);

    public void Remove(Parts destination) =>
        this.AddOrRemove(destination, Parts.Empty, 0);

    void AddOrRemove(Parts destination, IParts source, int level)
    {
        var toBeAdded = source is Parts;

        Debug.Assert(
            source is Parts.NullParts || toBeAdded,
            $"Unsupported 'from' type ({source.GetType().FullName}).");

        var segmentedParts = destination.SegmentPartsByNestingLevel(level);

        var segmentedPartsName = segmentedParts.Name!;
        if (destination.NumberOfPathSegments == level + 1)
        {
            if (toBeAdded)
            {
                var leaf = new Tuple<Parts, Parts>(destination, (Parts)source);
                this.LeavesToCreate.Add(segmentedPartsName, leaf);
            }
            else
            {
                this.LeavesToDrop.Add(segmentedPartsName, destination);
            }

            return;
        }

        if (!this.SubTreesToUpdate.TryGetValue(segmentedPartsName, out var targetTree))
        {
            targetTree = new(segmentedParts);
            this.SubTreesToUpdate.Add(segmentedPartsName, targetTree);
        }

        targetTree.AddOrRemove(destination, source, ++level);
    }
}
