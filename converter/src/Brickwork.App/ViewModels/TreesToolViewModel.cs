using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

/// <summary>Lists the tree trunk walls (see <see cref="TreeTrunks"/>), grouped by tree kind.</summary>
public partial class TreesToolViewModel : Tool
{
    private readonly EditorSession _session;
    private bool _syncingSelection;

    [ObservableProperty]
    private ObservableCollection<TreeKindViewModel> _kinds = [];

    [ObservableProperty]
    private object? _selectedTreeItem;

    public TreesToolViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                Rebuild();
            }

            if (args.PropertyName is nameof(EditorSession.ContentRevision) && !_session.IsGestureActive)
            {
                Sync();
            }

            if (args.PropertyName is nameof(EditorSession.FocusedWallEntityId) &&
                _session.FocusedWallEntityId is null &&
                SelectedTreeItem is TreeItemViewModel)
            {
                _syncingSelection = true;
                SelectedTreeItem = null;
                _syncingSelection = false;
            }
        };
        Rebuild();
    }

    public bool HasTrees => Kinds.Count > 0;

    public bool ShowEmptyMessage => !HasTrees;

    public string EmptyMessage => _session.Map is null
        ? "Open a Source Map to see trees"
        : "No trees found. Stamps whose names contain a tree word (tree, palm, oak, …) get a small X of walls at their trunk; the words can be changed under Settings → Import.";

    public string Summary
    {
        get
        {
            var trees = Kinds.SelectMany(kind => kind.Children).ToList();
            return $"{trees.Count(tree => tree.IsActive)} of {trees.Count} tree trunks exported as walls.";
        }
    }

    partial void OnSelectedTreeItemChanged(object? value)
    {
        if (_syncingSelection)
        {
            return;
        }

        if (value is TreeItemViewModel tree)
        {
            _session.SetSelection(tree.Walls.Select(wall => wall.EntityId), tree.Walls[0].EntityId);
        }
        else if (value is TreeKindViewModel kind)
        {
            _session.SetSelection(kind.Walls.Select(wall => wall.EntityId));
        }
    }

    internal void NotifySummaryChanged() => OnPropertyChanged(nameof(Summary));

    private void Rebuild()
    {
        _syncingSelection = true;
        SelectedTreeItem = null;
        _syncingSelection = false;

        var expanded = Kinds.Where(kind => kind.IsExpanded).Select(kind => kind.Name).ToHashSet();
        var kinds = new ObservableCollection<TreeKindViewModel>();
        if (_session.Map is { } map)
        {
            foreach (var kind in map.Walls
                         .Where(TreeTrunks.IsTrunk)
                         .GroupBy(wall => wall.DisplayName, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                var trees = kind
                    .GroupBy(wall => wall.TreeStampId)
                    .OrderBy(group => group.Key)
                    .Select(group => group.OrderByDescending(wall => wall.EntityId).ToList())
                    .ToList();
                kinds.Add(new TreeKindViewModel(this, _session, kind.Key, trees)
                {
                    IsExpanded = expanded.Contains(kind.Key),
                });
            }
        }

        Kinds = kinds;
        OnPropertyChanged(nameof(HasTrees));
        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>Refreshes values in place, or rebuilds when trunk walls were added or removed.</summary>
    private void Sync()
    {
        var map = _session.Map;
        var shown = Kinds.SelectMany(kind => kind.Walls).ToList();
        var current = map?.Walls.Where(TreeTrunks.IsTrunk).ToList() ?? [];
        if (shown.Count != current.Count || shown.Any(wall => !current.Contains(wall)))
        {
            Rebuild();
            return;
        }

        foreach (var kind in Kinds)
        {
            kind.RefreshFromModel();
        }

        OnPropertyChanged(nameof(Summary));
    }
}

public partial class TreeKindViewModel : ObservableObject
{
    private readonly TreesToolViewModel _tool;
    private readonly EditorSession _session;

    [ObservableProperty]
    private bool _isExpanded;

    public TreeKindViewModel(
        TreesToolViewModel tool,
        EditorSession session,
        string name,
        IReadOnlyList<IReadOnlyList<Wall>> trees)
    {
        _tool = tool;
        _session = session;
        Name = name;
        Children = new ObservableCollection<TreeItemViewModel>(
            trees.Select((walls, index) => new TreeItemViewModel(tool, session, walls, index + 1)));
        Walls = trees.SelectMany(walls => walls).ToList();
    }

    public string Name { get; }

    public IReadOnlyList<Wall> Walls { get; }

    public ObservableCollection<TreeItemViewModel> Children { get; }

    public string Header => $"{Name} ({Children.Count})";

    public bool? IsActive
    {
        get => Walls.All(IsWallOn) ? true : Walls.Any(IsWallOn) ? null : false;
        set
        {
            // A click on an indeterminate box reaches here as null; treat it as "enable all".
            var enabled = value ?? true;
            _session.Execute(
                enabled ? "Enable tree trunks" : "Disable tree trunks",
                () =>
                {
                    foreach (var wall in Walls)
                    {
                        WallLineEditing.SetWallEnabled(wall, enabled);
                    }
                });
            RefreshFromModel();
            _tool.NotifySummaryChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        foreach (var child in Children)
        {
            child.RefreshFromModel();
        }
    }

    internal static bool IsWallOn(Wall wall) => wall.IsActive && wall.LineType != WallLineType.Disabled;
}

public partial class TreeItemViewModel : ObservableObject
{
    private readonly TreesToolViewModel _tool;
    private readonly EditorSession _session;

    public TreeItemViewModel(TreesToolViewModel tool, EditorSession session, IReadOnlyList<Wall> walls, int number)
    {
        _tool = tool;
        _session = session;
        Walls = walls;
        Number = number;
    }

    /// <summary>The (usually two) walls of this tree's trunk X.</summary>
    public IReadOnlyList<Wall> Walls { get; }

    public int Number { get; }

    public string DisplayName => $"#{Number}";

    public string PositionText
    {
        get
        {
            var points = Walls.SelectMany(wall => wall.Points).ToList();
            return points.Count == 0
                ? string.Empty
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"({points.Average(point => point.X):0}, {points.Average(point => point.Y):0})");
        }
    }

    public bool IsActive
    {
        get => Walls.All(TreeKindViewModel.IsWallOn);
        set
        {
            if (IsActive == value)
            {
                return;
            }

            _session.Execute(
                value ? "Enable tree trunk" : "Disable tree trunk",
                () =>
                {
                    foreach (var wall in Walls)
                    {
                        WallLineEditing.SetWallEnabled(wall, value);
                    }
                });
            OnPropertyChanged();
            _tool.NotifySummaryChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(PositionText));
    }
}
