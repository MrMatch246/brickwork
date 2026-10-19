using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate;

/// <summary>Adds an X of two solid walls at the centre of every tree stamp (see <see cref="TreeTrunks"/>).</summary>
internal static class TreeImportDetection
{
    public static int AddTrunkWalls(MapDocument map, IEnumerable<InkStamp> stamps, InkarnateImportOptions options)
    {
        if (!options.DetectTreesFromStamps || options.TreeTrunkSize <= 0 || map.Grid.CellSize <= 0)
        {
            return 0;
        }

        var include = NameKeywords.Parse(options.TreeKeywords);
        var exclude = NameKeywords.Parse(options.TreeExcludeKeywords);
        var hiddenLayers = map.Layers
            .Where(layer => !layer.IsVisible)
            .Select(layer => layer.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var armLength = options.TreeTrunkSize * map.Grid.CellSize;
        var trees = stamps.Where(stamp => NameKeywords.MatchesEither(stamp.Name, stamp.DefaultName, include, exclude)).ToList();

        // One group per tree kind and layer, so a single checkbox switches e.g. all palm trunks.
        var kinds = trees
            .GroupBy(stamp => (Layer: stamp.LayerId ?? string.Empty, Kind: KindName(stamp)))
            .OrderBy(group => group.Key.Layer, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Kind, StringComparer.Ordinal)
            .ToList();
        var added = 0;

        for (var kindIndex = 0; kindIndex < kinds.Count; kindIndex++)
        {
            var kind = kinds[kindIndex];
            var group = new EntityGroup
            {
                GroupId = TreeTrunks.GroupId(kindIndex),
                Name = $"{kind.Key.Kind} trunks",
                LayerId = kind.First().LayerId,
            };

            foreach (var stamp in kind.OrderBy(stamp => stamp.EntityId))
            {
                var isActive = stamp.IsEntityVisible &&
                               (stamp.LayerId is null || !hiddenLayers.Contains(stamp.LayerId));
                var arms = TreeTrunks.BuildX(stamp.Position, armLength, stamp.Angle);
                for (var arm = 0; arm < arms.Count; arm++)
                {
                    var wall = new Wall
                    {
                        EntityId = TreeTrunks.WallId(stamp.EntityId, arm),
                        Name = kind.Key.Kind,
                        LayerId = stamp.LayerId,
                        GroupId = group.GroupId,
                        LineType = WallLineType.Solid,
                        WallEnabled = true,
                        IsActive = isActive,
                        IsEntityVisible = stamp.IsEntityVisible,
                        Scale = 1,
                        Points = [arms[arm].Start, arms[arm].End],
                    };
                    map.Walls.Add(wall);
                    group.MemberIds.Add(wall.EntityId);
                    added++;
                }
            }

            map.Groups.Add(group);
        }

        return added;
    }

    private static string KindName(InkStamp stamp) =>
        (string.IsNullOrWhiteSpace(stamp.Name) ? stamp.DefaultName : stamp.Name)?.Trim() ?? "Tree";
}
