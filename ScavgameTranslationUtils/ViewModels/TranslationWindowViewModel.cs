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
    public List<int> AllowedIndents { get; } = [2, 3, 4];

    public partial class DisplayNode(DisplayNode? parent) : ObservableObject
    {
        public DisplayNode? Parent { get; }= parent;
        
        public string DisplayLabel => AllItemsTranslated ? Label : $"{Label} (*)";
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayLabel))]
        private string _label = "";

        [ObservableProperty]
        private ObservableCollection<DisplayNode> _subNodes = [];

        [ObservableProperty]
        private string? _terminalKey;

        [NotifyPropertyChangedFor(nameof(DisplayLabel))]
        [ObservableProperty]
        private bool _allItemsTranslated;
    }
    
    public partial class DisplayKey(Workspace workspace) : ObservableObject
    {
        public string Display => IsTranslated 
            ? Constants.TurnToDisplayPathJoined(workspace, Key) 
            : $"{Constants.TurnToDisplayPathJoined(workspace, Key)} (*)";
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Display))]
        private string _key = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Display))]
        private bool _isTranslated;
    }

    private List<(string Category, int Start)> CategoryStarts { get; } = [];
    
    public TranslationWindowViewModel(Workspace workspace)
    {
        Workspace = workspace;
        DisplayKeys = new ObservableCollection<DisplayKey>(
            Workspace.Paths
                .Select(path => new DisplayKey(Workspace)
                {
                    IsTranslated = Workspace.GetText(path) != null, 
                    Key = path
                })
            );
        (AllNodes, DisplayNodes) = AssembleDisplayNodes();
        _currentKey = Workspace.Paths.Count > 0 ? Workspace.Paths[0] : "";

        var paths = Workspace.Paths;
        for (int i = 0; i < paths.Count; i++)
        {
            var category = GetKeyCategory(paths[i]);
            if (CategoryStarts.FindIndex(x => x.Category == category) == -1)
                CategoryStarts.Add((category, i));
        }
        
        CascadeCheckTranslatedNodes(DisplayNodes);
    }

    private (ObservableCollection<DisplayNode>, ObservableCollection<DisplayNode>) AssembleDisplayNodes()
    {
        var allNodes = new ObservableCollection<DisplayNode>();
        var nodes = new ObservableCollection<DisplayNode>();

        foreach (var path in Workspace.Paths)
        {
            ObservableCollection<DisplayNode> nextList = nodes;
            DisplayNode? targetNode = null;
            DisplayNode? parentNode = null;

            var parts = Constants.TurnToDisplayPath(Workspace, path);

            for (int i = 0; i < parts.Length; i++)
            {
                targetNode = nextList.FirstOrDefault(x => x.Label == parts[i]);

                if (targetNode == null)
                {
                    nextList.Add(targetNode = new DisplayNode(parentNode)
                    {
                        Label = parts[i]
                    });
                    allNodes.Add(targetNode);
                }

                nextList = targetNode.SubNodes;
                parentNode = targetNode;
            }

            if (targetNode != null)
                targetNode.TerminalKey = path;
        }

        return (allNodes, nodes);
    }

    private bool CascadeCheckTranslatedNodes(ObservableCollection<DisplayNode> nodes)
    {
        bool allTranslated = true;
        
        foreach (var node in nodes)
        {
            bool translated = node.TerminalKey != null
                ? Workspace.GetText(node.TerminalKey) != null
                : CascadeCheckTranslatedNodes(node.SubNodes);
            
            node.AllItemsTranslated = translated;
            allTranslated = allTranslated && translated;
        }

        return allTranslated;
    }

    public Workspace Workspace { get; }

    public ObservableCollection<DisplayKey> DisplayKeys { get; }
    
    public ObservableCollection<DisplayNode> AllNodes { get; }
    public ObservableCollection<DisplayNode> DisplayNodes { get; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaginationText))]
    [NotifyCanExecuteChangedFor(nameof(PreviousMainKeyCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextMainKeyCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCategoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCategoryCommand))]
    private int _currentKeyIndex;

    [ObservableProperty]
    private object? _selectedTreeViewItem;

    partial void OnSelectedTreeViewItemChanged(object? value)
    {
        if (value is not DisplayNode node || node.TerminalKey == null)
            return;

        var index = Workspace.Paths.IndexOf(node.TerminalKey);

        if (index == -1)
            return;

        CurrentKeyIndex = index;
    }
    
    partial void OnCurrentKeyIndexChanged(int value)
    {
        bool isValidIndex = 0 <= value && value < Workspace.Paths.Count;
        CurrentKey = isValidIndex ? Workspace.Paths[CurrentKeyIndex] : "";
    }

    private string GetKeyCategory(string key)
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
    private string _currentKey;

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
                DisplayKeys[CurrentKeyIndex].IsTranslated = true;
                OnPropertyChanged(nameof(CurrentKeyTranslationDisplay));
                OnPropertyChanged(nameof(TranslationProgress));

                var displayNode = AllNodes.FirstOrDefault(x => x.TerminalKey == CurrentKey);

                if (displayNode != null)
                {
                    displayNode.AllItemsTranslated = true;

                    // TODO: Optimize this
                    CascadeCheckTranslatedNodes(DisplayNodes);
                }
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
        CurrentKeyIndex = Math.Min(CurrentKeyIndex + 1, Workspace.Paths.Count - 1);
    }

    public bool CanExecuteNextMainKey() => CurrentKeyIndex < Workspace.Paths.Count - 1;

    [RelayCommand(CanExecute = nameof(CanExecutePreviousCategory))]
    public void PreviousCategory()
    {
        var category = GetKeyCategory(CurrentKey);

        var currentIndex = CategoryStarts.FindIndex(x => x.Category == category);

        if (currentIndex <= 0)
            return;

        CurrentKeyIndex = CategoryStarts[currentIndex - 1].Start;
    }

    public bool CanExecutePreviousCategory()
    {
        var category = GetKeyCategory(CurrentKey);

        var currentIndex = CategoryStarts.FindIndex(x => x.Category == category);

        return currentIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteNextCategory))]
    public void NextCategory()
    {
        var category = GetKeyCategory(CurrentKey);

        var currentIndex = CategoryStarts.FindIndex(x => x.Category == category);

        if (currentIndex == -1 || currentIndex >= CategoryStarts.Count - 1)
            return;

        CurrentKeyIndex = CategoryStarts[currentIndex + 1].Start;
    }

    public bool CanExecuteNextCategory()
    {
        var category = GetKeyCategory(CurrentKey);

        var currentIndex = CategoryStarts.FindIndex(x => x.Category == category);

        return currentIndex != -1 && currentIndex < CategoryStarts.Count - 1;
    }
    
    [RelayCommand]
    public void NextUntranslatedKey(Control control)
    {
        var keys = Workspace.Paths;

        var firstUntranslated = keys.FindIndex(x => Workspace.GetText(x) == null);

        if (firstUntranslated == -1)
            firstUntranslated = keys.Count - 1; // TODO: Replace with a notification that no such keys exist

        CurrentKeyIndex = Math.Clamp(firstUntranslated, 0, keys.Count - 1);
    }
}