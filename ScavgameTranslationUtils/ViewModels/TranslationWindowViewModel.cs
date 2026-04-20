using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScavgameTranslationUtils.Models;

namespace ScavgameTranslationUtils.ViewModels;

public partial class TranslationWindowViewModel : ObservableObject
{
    public partial class DisplayNode(DisplayNode? parent) : ObservableObject
    {
        public DisplayNode? Parent { get; } = parent;
        
        public string PathPartView => AllItemsTranslated ? PathPartDisplay : $"{PathPartDisplay} (*)";
        public string FullPathView => AllItemsTranslated ? FullPathDisplay : $"{FullPathDisplay} (*)";
        
        public required string PathPart { get; init; }
        public required string FullPath { get; init; }
        
        public required string PathPartDisplay { get; init; }
        public required string FullPathDisplay { get; init; }

        public ObservableCollection<DisplayNode> SubNodes { get; } = [];

        public bool IsTerminalNode => SubNodes.Count == 0;

        [NotifyPropertyChangedFor(nameof(PathPartView))]
        [NotifyPropertyChangedFor(nameof(FullPathView))]
        [ObservableProperty]
        private bool _allItemsTranslated;

        // This is a shallow check
        public void UpdateTranslationStatus(Workspace workspace)
        {
            if (IsTerminalNode)
            {
                AllItemsTranslated = workspace.GetText(FullPath) != null;
            }
            else
            {
                bool allTranslated = true;

                foreach (var node in SubNodes)
                    allTranslated = allTranslated && node.AllItemsTranslated;
                

                AllItemsTranslated = allTranslated;
            }
            
            Parent?.UpdateTranslationStatus(workspace);
        }

        public override string ToString() => $"FullPath={FullPath}, IsTranslated={AllItemsTranslated}";
    }
    
    public class NavigationData
    {
        public static NavigationData Create(Workspace workspace, bool sortInnerKeysAlphabetically)
        {
            var treeViewNodes = new ObservableCollection<DisplayNode>();

            foreach (var path in workspace.Paths)
            {
                ObservableCollection<DisplayNode> parentList = treeViewNodes;
                DisplayNode? parentNode = null;

                var (displayParts, parts) = Constants.SplitToDisplayAndParts(workspace, path);

                for (int i = 0; i < parts.Length; i++)
                {
                    DisplayNode? targetNode = parentList.FirstOrDefault(x => x.PathPart == parts[i]);

                    if (targetNode == null)
                    {
                        parentList.Add(targetNode = new DisplayNode(parentNode)
                        {
                            PathPart = parts[i],
                            FullPath = parentNode != null ? $"{parentNode.FullPath}:{parts[i]}" : parts[i],
                            PathPartDisplay = displayParts[i],
                            FullPathDisplay = parentNode != null ? $"{parentNode.FullPathDisplay} > {displayParts[i]}" : displayParts[i]
                        });
                    }

                    parentList = targetNode.SubNodes;
                    parentNode = targetNode;
                }
            }
            
            if (sortInnerKeysAlphabetically)
            {
                // TODO: Bad string comparison here
                List<DisplayNode?> nodesToSort = new[] { "main", "buildings", "moodles", "other" }
                    .Select(label => treeViewNodes.FirstOrDefault(node => node.PathPart.Equals(label, StringComparison.InvariantCultureIgnoreCase)))
                    .ToList();

                var characters = treeViewNodes.FirstOrDefault(node => node.PathPart.Equals("character", StringComparison.InvariantCultureIgnoreCase));

                if (characters != null)
                    nodesToSort.AddRange(characters.SubNodes);

                foreach (var node in nodesToSort.Where(x => x != null).Cast<DisplayNode>())
                {
                    var sortedNodes = node.SubNodes.OrderBy(x => x.PathPart).ToArray();
                    node.SubNodes.Clear();
                    
                    foreach (var sortedNode in sortedNodes)
                        node.SubNodes.Add(sortedNode);
                }
            }

            var listViewNodes = new ObservableCollection<DisplayNode>();
            
            var stack = new Stack<DisplayNode>(treeViewNodes.Reverse());

            while (stack.Count > 0)
            {
                var nextNode = stack.Pop();

                if (nextNode.IsTerminalNode)
                {
                    listViewNodes.Add(nextNode);
                }
                else
                {
                    foreach (var node in nextNode.SubNodes.Reverse())
                        stack.Push(node);
                }
            }

            var categoryStarts = new List<(string Category, int Start)>();
            
            for (int i = 0; i < listViewNodes.Count; i++)
            {
                var category = GetKeyCategory(listViewNodes[i].FullPath);
                if (categoryStarts.FindIndex(x => x.Category == category) == -1)
                    categoryStarts.Add((category, i));
            }

            return new NavigationData()
            {
                CategoryStarts = categoryStarts,
                TreeView = treeViewNodes,
                ListView = listViewNodes,
            };
        }
        
        public required ObservableCollection<DisplayNode> TreeView { get; init; }
        public required ObservableCollection<DisplayNode> ListView { get; init; }
        public required List<(string Category, int Start)> CategoryStarts { get; init; }
    }
    
    public TranslationWindowViewModel(Workspace workspace)
    {
        Workspace = workspace;
        
        _englishJsonNavData = NavigationData.Create(workspace, false);
        _alphabeticNavData = NavigationData.Create(workspace, true);
        NavData = _englishJsonNavData;
        
        // This length check is *in 99% of cases* not needed
        _currentKey = NavData.ListView.Count > 0 ? NavData.ListView[0].FullPath : "";
        
        foreach (var node in NavData.ListView)
            node.UpdateTranslationStatus(workspace);
    }

    public List<int> AllowedIndents { get; } = [2, 3, 4];

    public List<string> DisplayKeySortOptions { get; } = ["Like EN.json", "Alphabetically"];

    public Workspace Workspace { get; }

    [ObservableProperty]
    private string _selectedDisplayKeySorting = "Like EN.json";

    partial void OnSelectedDisplayKeySortingChanged(string value)
    {
        var currentKey = CurrentKey;
        
        switch (value)
        {
            case "Alphabetically":
                NavData = _alphabeticNavData;
                break;
            default:
                NavData = _englishJsonNavData;
                break;
        }
        
        foreach (var node in NavData.ListView)
            node.UpdateTranslationStatus(Workspace);
        
        // Has to be done *after* setting the new item source!
        var updatedNode = NavData.ListView.FirstOrDefault(x => x.FullPath == currentKey);
        CurrentKeyIndex = updatedNode == null ? 0 : NavData.ListView.IndexOf(updatedNode);
    }

    [ObservableProperty]
    private NavigationData _navData;

    private NavigationData _alphabeticNavData;
    private NavigationData _englishJsonNavData;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaginationText))]
    [NotifyCanExecuteChangedFor(nameof(PreviousMainKeyCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextMainKeyCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCategoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCategoryCommand))]
    private int _currentKeyIndex;
    
    partial void OnCurrentKeyIndexChanged(int value)
    {
        bool isValidIndex = 0 <= value && value < NavData.ListView.Count;
        CurrentKey = isValidIndex ? NavData.ListView[CurrentKeyIndex].FullPath : "";
        SelectedTreeViewItem = isValidIndex ? NavData.ListView[CurrentKeyIndex] : null;
    }

    [ObservableProperty]
    private object? _selectedTreeViewItem;
    
    partial void OnSelectedTreeViewItemChanged(object? value)
    {
        if (value is not DisplayNode node || !node.IsTerminalNode)
            return;

        var index = NavData.ListView.IndexOf(node);

        if (index == -1)
            return;

        CurrentKeyIndex = index;
    }

    private static string GetKeyCategory(string key)
    {
        var category = key.Split(':')[0];
        return category.Length > 1 ? char.ToUpper(category[0]) + category[1..] : category;
    }
    
    public string PaginationText => $"Key {CurrentKeyIndex + 1} / Total {Workspace.Paths.Count} / {GetKeyCategory(CurrentKey)}";

    public string TranslationProgress
    {
        get
        {
            // TODO: Not very efficient
            int counter = 0;
            foreach (var path in Workspace.Paths)
            {
                if (Workspace.GetText(path) != null)
                    counter++;
            }

            return $"{counter} / {Workspace.Paths.Count} Keys ({counter * 100f / Workspace.Paths.Count:F2} %)";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentKeyNotes))]
    [NotifyPropertyChangedFor(nameof(CurrentKeyEnglish))]
    [NotifyPropertyChangedFor(nameof(CurrentKeyTranslation))]
    [NotifyPropertyChangedFor(nameof(CurrentKeyEnglishDisplay))]
    [NotifyPropertyChangedFor(nameof(CurrentKeyTranslationDisplay))]
    private string _currentKey = "";

    public string CurrentKeyNotes => Constants.GetTranslationNotes(Workspace, CurrentKey);

    public string? CurrentKeyEnglish => Workspace.GetOriginalText(CurrentKey);
    
    public string? CurrentKeyTranslation
    {
        get => Workspace.GetText(CurrentKey) ?? "";
        set
        {
            var oldValue = Workspace.GetText(CurrentKey);
            if (SetProperty(ref oldValue, value) && value != null)
            {
                Workspace.SetText(CurrentKey, value);
                NavData.ListView[CurrentKeyIndex].UpdateTranslationStatus(Workspace);
                
                OnPropertyChanged(nameof(CurrentKeyTranslationDisplay));
                OnPropertyChanged(nameof(TranslationProgress));
            }
        }
    }
    
    public string? CurrentKeyEnglishDisplay => CurrentKeyEnglish != null
        ? Constants.PreprocessText(CurrentKey, CurrentKeyEnglish)
        : null;

    public string? CurrentKeyTranslationDisplay => CurrentKeyTranslation != null
        ? Constants.PreprocessText(CurrentKey, CurrentKeyTranslation)
        : null;
    
    public string CurrentName
    {
        get => Workspace.Name;
        set
        {
            var oldValue = Workspace.Name;
            if (SetProperty(ref oldValue, value))
                Workspace.Name = value;
        }
    }

    public string CurrentDescription
    {
        get => Workspace.Description;
        set
        {
            var oldValue = Workspace.Description;
            if (SetProperty(ref oldValue, value))
                Workspace.Description = value;
        }
    }

    public int IndentSize
    {
        get => Workspace.IndentSize;
        set 
        {
            var oldValue = Workspace.IndentSize;
            if (SetProperty(ref oldValue, value))
                Workspace.IndentSize = value;    
        }
    }

    public bool SortKeys
    {
        get => Workspace.SortKeys;
        set 
        {
            var oldValue = Workspace.SortKeys;
            if (SetProperty(ref oldValue, value))
                Workspace.SortKeys = value;    
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecutePreviousMainKey))]
    public void PreviousMainKey()
    {
        CurrentKeyIndex = Math.Max(CurrentKeyIndex - 1, 0);
    }

    public bool CanExecutePreviousMainKey() => CurrentKeyIndex > 0;
    
    [RelayCommand(CanExecute = nameof(CanExecuteNextMainKey))]
    public void NextMainKey()
    {
        CurrentKeyIndex = Math.Min(CurrentKeyIndex + 1, NavData.ListView.Count - 1);
    }

    public bool CanExecuteNextMainKey() => CurrentKeyIndex < NavData.ListView.Count - 1;

    [RelayCommand(CanExecute = nameof(CanExecutePreviousCategory))]
    public void PreviousCategory()
    {
        var category = GetKeyCategory(CurrentKey);

        var currentIndex = NavData.CategoryStarts.FindIndex(x => x.Category == category);

        if (currentIndex <= 0)
            return;

        CurrentKeyIndex = NavData.CategoryStarts[currentIndex - 1].Start;
    }

    public bool CanExecutePreviousCategory()
    {
        var category = GetKeyCategory(CurrentKey);

        var currentIndex = NavData.CategoryStarts.FindIndex(x => x.Category == category);

        return currentIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteNextCategory))]
    public void NextCategory()
    {
        var category = GetKeyCategory(CurrentKey);

        var currentIndex = NavData.CategoryStarts.FindIndex(x => x.Category == category);

        if (currentIndex == -1 || currentIndex >= NavData.CategoryStarts.Count - 1)
            return;

        CurrentKeyIndex = NavData.CategoryStarts[currentIndex + 1].Start;
    }

    public bool CanExecuteNextCategory()
    {
        var category = GetKeyCategory(CurrentKey);

        var currentIndex = NavData.CategoryStarts.FindIndex(x => x.Category == category);

        return currentIndex != -1 && currentIndex < NavData.CategoryStarts.Count - 1;
    }
    
    [RelayCommand]
    public void NextUntranslatedKey(Control control)
    {
        var keys = NavData.ListView;

        var firstUntranslated = keys.FirstOrDefault(x => Workspace.GetText(x.FullPath) == null);
        var firstUntranslatedIndex = firstUntranslated != null ? keys.IndexOf(firstUntranslated) : -1;

        if (firstUntranslatedIndex == -1)
            firstUntranslatedIndex = keys.Count - 1; // TODO: Replace with a notification that no such keys exist

        CurrentKeyIndex = Math.Clamp(firstUntranslatedIndex, 0, keys.Count - 1);
    }
}