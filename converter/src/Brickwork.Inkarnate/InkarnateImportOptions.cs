using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.Inkarnate;

public sealed class InkarnateImportOptions
{
    public double WallSimplificationTolerance { get; set; } =
        WallSimplificationSettings.DefaultToleranceSceneUnits;

    public bool DetectRegionsFromColorPaths { get; set; } = true;

    public bool DetectRegionsFromNamedContainers { get; set; } = true;

    public bool DetectRegionsFromPathNames { get; set; } = true;

    /// <summary>Create lights from stamps whose names match <see cref="LightKeywords"/>.</summary>
    public bool DetectLightsFromStamps { get; set; } = true;

    public string LightKeywords { get; set; } = Core.Models.LightKeywords.DefaultInclude;

    public string LightExcludeKeywords { get; set; } = Core.Models.LightKeywords.DefaultExclude;

    /// <summary>Pick a flicker/pulse animation for detected lights from their names.</summary>
    public bool DetectLightAnimations { get; set; } = true;

    /// <summary>Bright radius in grid squares for detected lights.</summary>
    public double LightBrightRadius { get; set; } = LightSource.DefaultBrightRadius;

    /// <summary>Dim radius in grid squares for detected lights.</summary>
    public double LightDimRadius { get; set; } = LightSource.DefaultDimRadius;

    /// <summary>Add an X of two walls at the trunk of every stamp whose name matches the tree words.</summary>
    public bool DetectTreesFromStamps { get; set; } = true;

    public string TreeKeywords { get; set; } = TreeTrunks.DefaultInclude;

    public string TreeExcludeKeywords { get; set; } = TreeTrunks.DefaultExclude;

    /// <summary>Length of each trunk wall, in grid squares.</summary>
    public double TreeTrunkSize { get; set; } = TreeTrunks.DefaultSize;

    /// <summary>
    /// Scale each trunk with its stamp, relative to the median scale of that tree kind: a typical
    /// tree gets <see cref="TreeTrunkSize"/>, one stamped twice as large gets twice the trunk.
    /// </summary>
    public bool ScaleTreeTrunksWithStamp { get; set; } = true;

    /// <summary>Per-version scale and offset from the tree-trunk library, keyed by <see cref="TreeTrunks.VersionKey(int?, string?)"/>.</summary>
    public IReadOnlyDictionary<string, TreeTrunkAdjustment> TreeAdjustments { get; set; } =
        new Dictionary<string, TreeTrunkAdjustment>();

    public bool AnyRegionDetectionEnabled =>
        DetectRegionsFromColorPaths ||
        DetectRegionsFromNamedContainers ||
        DetectRegionsFromPathNames;
}
