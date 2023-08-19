using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.RichText;
using Content.Client.UserInterface.ControlExtensions;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Controls.FancyTree;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.ContentPack;

namespace Content.Client.Guidebook.Controls;

[GenerateTypedNameReferences]
public sealed partial class GuidebookWindow : FancyWindow, ILinkClickHandler
{
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly DocumentParsingManager _parsingMan = default!;

    private Dictionary<string, GuideEntry> _entries = new();

    public GuidebookWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        Tree.OnSelectedItemChanged += OnSelectionChanged;

        SearchBar.OnTextChanged += _ =>
        {
            HandleFilter();
        };
    }

    private void OnSelectionChanged(TreeItem? item)
    {
        if (item != null && item.Metadata is GuideEntry entry)
            ShowGuide(entry);
        else
            ClearSelectedGuide();
    }

    public void ClearSelectedGuide()
    {
        Placeholder.Visible = true;
        EntryContainer.Visible = false;
        SearchContainer.Visible = false;
        EntryContainer.RemoveAllChildren();
    }

    private void ShowGuide(GuideEntry entry)
    {
        Scroll.SetScrollValue(default);
        Placeholder.Visible = false;
        EntryContainer.Visible = true;
        SearchBar.Text = "";
        EntryContainer.RemoveAllChildren();
        using var file = _resourceManager.ContentFileReadText(entry.Text);

        SearchContainer.Visible = entry.FilterEnabled;

        if (!_parsingMan.TryAddMarkup(EntryContainer, file.ReadToEnd()))
        {
            EntryContainer.AddChild(new Label() { Text = "ERROR: Failed to parse document." });
            Logger.Error($"Failed to parse contents of guide document {entry.Id}.");
        }
    }

    public void UpdateGuides(
        Dictionary<string, GuideEntry> entries,
        List<string>? rootEntries = null,
        string? forceRoot = null,
        string? selected = null)
    {
        _entries = entries;
        RepopulateTree(rootEntries, forceRoot);
        ClearSelectedGuide();

        Split.State = SplitContainer.SplitState.Auto;
        if (entries.Count == 1)
        {
            TreeBox.Visible = false;
            Split.ResizeMode = SplitContainer.SplitResizeMode.NotResizable;
            selected = entries.Keys.First();
        }
        else
        {
            TreeBox.Visible = true;
            Split.ResizeMode = SplitContainer.SplitResizeMode.RespectChildrenMinSize;
        }

        if (selected != null)
        {
            var item = Tree.Items.FirstOrDefault(x => x.Metadata is GuideEntry entry && entry.Id == selected);
            Tree.SetSelectedIndex(item?.Index);
        }
        else
        {
            var item = Tree.Items.First();
            Tree.SetSelectedIndex(item?.Index);
        }
    }

    private IEnumerable<GuideEntry> GetSortedRootEntries(List<string>? rootEntries)
    {
        if (rootEntries == null)
        {
            HashSet<string> entries = new(_entries.Keys);
            foreach (var entry in _entries.Values)
            {
                entries.ExceptWith(entry.Children);
            }
            rootEntries = entries.ToList();
        }

        return rootEntries
            .Select(x => _entries[x])
            .OrderBy(x => x.Priority)
            .ThenBy(x => Loc.GetString(x.Name));
    }

    private void RepopulateTree(List<string>? roots = null, string? forcedRoot = null)
    {
        Tree.Clear();

        HashSet<string> addedEntries = new();

        TreeItem? parent = forcedRoot == null ? null : AddEntry(forcedRoot, null, addedEntries);
        foreach (var entry in GetSortedRootEntries(roots))
        {
            AddEntry(entry.Id, parent, addedEntries);
        }
        Tree.SetAllExpanded(true);
    }

    private TreeItem? AddEntry(string id, TreeItem? parent, HashSet<string> addedEntries)
    {
        if (!_entries.TryGetValue(id, out var entry))
            return null;

        if (!addedEntries.Add(id))
        {
            Logger.Error($"Adding duplicate guide entry: {id}");
            return null;
        }

        var item = Tree.AddItem(parent);
        item.Metadata = entry;
        var name = Loc.GetString(entry.Name);
        item.Label.Text = name;

        foreach (var child in entry.Children)
        {
            AddEntry(child, item, addedEntries);
        }

        return item;
    }

    public void HandleClick(string link)
    {
        if (!_entries.TryGetValue(link, out var entry))
            return;

        if (Tree.TryGetIndexFromMetadata(entry, out var index))
        {
            Tree.ExpandParentEntries(index.Value);
            Tree.SetSelectedIndex(index);
        }
        else
        {
            ShowGuide(entry);
        }
    }

    private void HandleFilter()
    {
        var emptySearch = SearchBar.Text.Trim().Length == 0;

        if (Tree.SelectedItem != null && Tree.SelectedItem.Metadata is GuideEntry entry && entry.FilterEnabled)
        {
            var foundElements = EntryContainer.GetSearchableControls();

            foreach (var element in foundElements)
            {
                element.SetHiddenState(true, SearchBar.Text.Trim());
            }
        }

    }
}
