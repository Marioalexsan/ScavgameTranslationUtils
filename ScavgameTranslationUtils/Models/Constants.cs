using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ScavgameTranslationUtils.Models;

public static class Constants
{
    public static (string[] DisplayParts, string[] Parts) SplitToDisplayAndParts(Workspace workspace, string path)
    {
        static string UpperFirst(string part)
        {
            return part.Length >= 1
                ? char.ToUpper(part[0]) + part[1..]
                : part;
        }

        var parts = path.Split(':');
        var displayParts = parts.ToArray();

        if (parts[0] == "main"
            || parts[0] == "buildings"
            || parts[0] == "other"
            || parts[0] == "moodles")
        {
            displayParts[0] = UpperFirst(parts[0]);

            if (displayParts.Length >= 2)
                displayParts[1] = $"\"{parts[1]}\"";
        }

        else if (parts[0] == "character")
        {
            displayParts[0] = UpperFirst(parts[0]);
            
            if (displayParts.Length >= 2)
                displayParts[1] = int.TryParse(parts[1], out var characterIndex)
                    ? MapCharacterIndexToCharacter(characterIndex)
                    : parts[1];
            
            if (displayParts.Length >= 3)
                displayParts[2] = $"\"{parts[2]}\"";
        }

        else if (parts[0] == "notes")
        {
            displayParts[0] = UpperFirst(parts[0]);
            
            if (displayParts.Length >= 2)
                displayParts[1] = int.TryParse(parts[1], out var biomeIndex)
                    ? MapBiomeIndexToBiome(workspace, biomeIndex)
                    : parts[1];
            
            if (displayParts.Length >= 3)
                displayParts[2] = int.TryParse(parts[2], out var noteIndex)
                    ? MapNoteIndexToNote(workspace, noteIndex)
                    : parts[2];
            
            if (displayParts.Length >= 4)
                displayParts[3] = UpperFirst(parts[3]);
        }

        else if (parts[0] == "pdaNotes" && parts.Length >= 3)
        {
            displayParts[0] = UpperFirst(parts[0]);
            
            if (displayParts.Length >= 2)
                displayParts[1] = int.TryParse(parts[1], out var pdaIndex)
                    ? MapPdaNoteIndexToPdaNote(workspace, pdaIndex)
                    : parts[1];
            
            if (displayParts.Length >= 3)
                displayParts[2] = UpperFirst(parts[2]);
        }

        return (displayParts, parts);
    }

    public static string MapCharacterIndexToCharacter(int index)
    {
        return index switch
        {
            0 => "Experiment",
            1 => "Milky",
            2 => "Dune",
            _ => $"Character {index}"
        };
    }

    public static string MapBiomeIndexToBiome(Workspace workspace, int index)
    {
        if (index == 0)
            return "Generic";

        return workspace.GetOriginalText($"other:layertitle{index}") ?? $"Unknown biome {index}";
    }

    public static string MapNoteIndexToNote(Workspace workspace, int index)
    {
        // TODO: Add short, but descriptive names
        return $"Note {index}";
    }

    public static string MapPdaNoteIndexToPdaNote(Workspace workspace, int index)
    {
        // TODO: Add short, but descriptive names
        return $"PDA {index}";
    }

    public static string PreprocessText(string path, string text)
    {
        var parts = path.Split(':');

        switch (parts[0])
        {
            case "pdaNotes":
                return text.Replace("<>", "  ");
            default:
                return text;
        }
    }

    public static string GetTranslationNotes(Workspace workspace, string key)
    {
        // Custom notes
        var assembledNotes = key switch
        {
            "other:jsonthing" =>
                """
                As mentioned by the developer, this key is not necessary.
                Feel free to skip it.
                """,
            _ => "",
        };

        if ((key.StartsWith("pdaNotes:") || key.StartsWith("notes:")) && key.EndsWith(":sprite"))
        {
            assembledNotes += "\n";
            assembledNotes += "This should keep the same value from the English translation!";
        }

        if (key.StartsWith("notes:") && key.EndsWith(":font"))
        {
            assembledNotes += "\n";
            assembledNotes += "Fonts represent the character who wrote the note.";
            assembledNotes += "This should keep the same value from the English translation!";
        }

        return assembledNotes;
    }
}