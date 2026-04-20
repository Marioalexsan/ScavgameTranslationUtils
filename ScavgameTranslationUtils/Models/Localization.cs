using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ScavgameTranslationUtils.Models;

// Note: JSON order should match the order of EN.json keys in the game
public class Localization
{
    public class Note
    {
        [JsonPropertyName("Item1")]
        [JsonPropertyOrder(1)]
        public string? Text { get; set; }

        [JsonPropertyName("Item2")]
        [JsonPropertyOrder(2)]
        public string? Sprite { get; set; }

        [JsonPropertyName("Item3")]
        [JsonPropertyOrder(3)]
        public string? Font { get; set; }

        public Note Clone()
        {
            return new Note()
            {
                Text = Text,
                Sprite = Sprite,
                Font = Font,
            };
        }
    }

    public class PdaNote
    {
        [JsonPropertyName("Item1")]
        [JsonPropertyOrder(1)]
        public string? Text { get; set; }

        [JsonPropertyName("Item2")]
        [JsonPropertyOrder(2)]
        public string? Sprite { get; set; }

        public PdaNote Clone()
        {
            return new PdaNote()
            {
                Text = Text,
                Sprite = Sprite,
            };
        }
    }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(1)]
    public string Name { get; set; } = "Unnamed translation";

    [JsonPropertyName("description")]
    [JsonPropertyOrder(2)]
    public string Description { get; set; } = "";

    [JsonPropertyName("main")]
    [JsonPropertyOrder(3)]
    public OrderedDictionary<string, string> Main { get; set; } = [];

    [JsonPropertyName("buildings")]
    [JsonPropertyOrder(4)]
    public OrderedDictionary<string, string> Buildings { get; set; } = [];

    [JsonPropertyName("moodles")]
    [JsonPropertyOrder(5)]
    public OrderedDictionary<string, string> Moodles { get; set; } = [];

    [JsonPropertyName("other")]
    [JsonPropertyOrder(6)]
    public OrderedDictionary<string, string> Other { get; set; } = [];

    [JsonPropertyName("character")]
    [JsonPropertyOrder(7)]
    public List<OrderedDictionary<string, List<string>>> Character { get; set; } = [];

    [JsonPropertyName("notes")]
    [JsonPropertyOrder(8)]
    public List<List<Note>> Notes { get; set; } = [];

    [JsonPropertyName("pdaNotes")]
    [JsonPropertyOrder(9)]
    public List<PdaNote> PdaNotes { get; set; } = [];

    public List<string> GetPaths()
    {
        var paths = new List<string>();
        
        paths.AddRange(Main.Keys.Select(key => $"main:{key}"));
        paths.AddRange(Buildings.Keys.Select(key => $"buildings:{key}"));
        paths.AddRange(Moodles.Keys.Select(key => $"moodles:{key}"));
        paths.AddRange(Other.Keys.Select(key => $"other:{key}"));

        for (int characterIndex = 0; characterIndex < Character.Count; characterIndex++)
        {
            var texts = Character[characterIndex];

            foreach (var (key, variants) in texts)
            {
                for (int textVariant = 0; textVariant < variants.Count; textVariant++)
                    paths.Add($"character:{characterIndex}:{key}:{textVariant}");
            }
        }

        for (int biomeIndex = 0; biomeIndex < Notes.Count; biomeIndex++)
        {
            for (int noteIndex = 0; noteIndex < Notes[biomeIndex].Count; noteIndex++)
            {
                paths.Add($"notes:{biomeIndex}:{noteIndex}:text");
                paths.Add($"notes:{biomeIndex}:{noteIndex}:sprite");
                paths.Add($"notes:{biomeIndex}:{noteIndex}:font");
            }
        }

        for (int pdaNotesIndex = 0; pdaNotesIndex < PdaNotes.Count; pdaNotesIndex++)
        {
            paths.Add($"pdaNotes:{pdaNotesIndex}:text");
            paths.Add($"pdaNotes:{pdaNotesIndex}:sprite");
        }

        return paths;
    }

    public string? GetTextByPath(string path)
    {
        var parts = path.Split(':');

        if (parts.Length == 1)
            return null;

        else if (parts.Length == 2)
        {
            return parts[0] switch
            {
                "main" => Main.TryGetValue(parts[1], out var value) ? value : null,
                "buildings" => Buildings.TryGetValue(parts[1], out var value) ? value : null,
                "moodles" => Moodles.TryGetValue(parts[1], out var value) ? value : null,
                "other" => Other.TryGetValue(parts[1], out var value) ? value : null,
                _ => null
            };
        }

        else if (parts.Length == 3)
        {
            return parts[0] switch
            {
                "pdaNotes" =>
                    int.TryParse(parts[1], out var index)
                    && 0 <= index && index < PdaNotes.Count
                        ? parts[2] switch
                        {
                            "text" => PdaNotes[index].Text,
                            "sprite" => PdaNotes[index].Sprite,
                            _ => null
                        }
                        : null,
                _ => null
            };
        }
        
        else if (parts.Length == 4)
        {
            return parts[0] switch
            {
                "character" =>
                    int.TryParse(parts[1], out var characterIndex)
                    && 0 <= characterIndex && characterIndex < Character.Count
                    && Character[characterIndex].TryGetValue(parts[2], out var variants)
                    && int.TryParse(parts[3], out var variantIndex)
                    && 0 <= variantIndex && variantIndex < variants.Count
                        ? variants[variantIndex]
                        : null,
                "notes" => 
                    int.TryParse(parts[1], out var biomeIndex)
                    && 0 <= biomeIndex && biomeIndex < Notes.Count
                    && int.TryParse(parts[2], out var noteIndex)
                    && 0 <= noteIndex && noteIndex < Notes[biomeIndex].Count
                    ? parts[3] switch
                    {
                        "text" => Notes[biomeIndex][noteIndex].Text,
                        "sprite" => Notes[biomeIndex][noteIndex].Sprite,
                        "font" => Notes[biomeIndex][noteIndex].Font,
                        _ => null
                    }
                    : null,
                _ => null
            };
        }

        else return null;
    }
    
    public void SetTextByPath(string path, string text)
    {
        var parts = path.Split(':');

        text = text.ReplaceLineEndings("\n");

        if (parts.Length == 1)
            return;

        else if (parts.Length == 2)
        {
            if (parts[0] == "main")
                Main[parts[1]] = text;
            
            else if (parts[0] == "buildings")
                Buildings[parts[1]] = text;
            
            else if (parts[0] == "moodles")
                Moodles[parts[1]] = text;
            
            else if (parts[0] == "other")
                Other[parts[1]] = text;
        }

        else if (parts.Length == 3)
        {
            if (parts[0] == "pdaNotes")
            {
                if (int.TryParse(parts[1], out var index) && index >= 0)
                {
                    // Backfill if needed
                    while (index >= PdaNotes.Count)
                        PdaNotes.Add(new PdaNote());
                    
                    if (parts[2] == "text")
                        PdaNotes[index].Text = text;
                    
                    else if (parts[2] == "sprite")
                        PdaNotes[index].Sprite = text;
                }
            }
        }
        
        else if (parts.Length == 4)
        {
            if (parts[0] == "character")
            {
                if (int.TryParse(parts[1], out var characterIndex) && characterIndex >= 0)
                {
                    // Backfill if needed
                    while (characterIndex >= Character.Count)
                        Character.Add([]);
                    
                    if (Character[characterIndex].TryGetValue(parts[2], out var variants))
                    {
                        if (int.TryParse(parts[3], out var variantIndex) && variantIndex >= 0)
                        {
                            // Backfill if needed
                            while (variantIndex >= variants.Count)
                                variants.Add("");
                            
                            variants[variantIndex] = text;
                        }
                    }
                }
            }
            
            else if (parts[0] == "notes")
            {
                if (int.TryParse(parts[1], out var biomeIndex) && biomeIndex >= 0)
                {
                    // Backfill if needed
                    while (biomeIndex >= Notes.Count)
                        Notes.Add([]);

                    if (int.TryParse(parts[2], out var noteIndex) && noteIndex >= 0)
                    {
                        var biomeNotes = Notes[biomeIndex];
                        
                        // Backfill if needed
                        while (noteIndex >= biomeNotes.Count)
                            biomeNotes.Add(new Note());

                        if (parts[3] == "text")
                            biomeNotes[noteIndex].Text = text;
                    
                        else if (parts[3] == "sprite")
                            biomeNotes[noteIndex].Sprite = text;
                    
                        else if (parts[3] == "font")
                            biomeNotes[noteIndex].Font = text;
                    }
                }
            }
        }
    }

    public Localization Clone()
    {
        return new Localization()
        {
            Name = Name,
            Description = Description,
            Main = new OrderedDictionary<string, string>(Main),
            Buildings = new OrderedDictionary<string, string>(Buildings),
            Moodles = new OrderedDictionary<string, string>(Moodles),
            Other = new OrderedDictionary<string, string>(Other),
            Character = new List<OrderedDictionary<string, List<string>>>(
                Character.Select(
                    x => new OrderedDictionary<string, List<string>>(
                        x.Select(y => new KeyValuePair<string, List<string>>(
                            y.Key, 
                            new List<string>(y.Value))
                        )
                    )
                )
            ),
            Notes = Notes.Select(x => x.Select(y => y.Clone()).ToList()).ToList(),
            PdaNotes = PdaNotes.Select(x => x.Clone()).ToList()
        };
    }
}