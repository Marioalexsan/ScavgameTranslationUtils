using System;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace ScavgameTranslationUtils.Models;

public static class Constants
{
    public static string UpperFirst(string part)
    {
        return part.Length >= 1
            ? char.ToUpper(part[0]) + part[1..]
            : part;
    }

    public static (string[] DisplayParts, string[] Parts) SplitToDisplayAndParts(Workspace workspace, string path)
    {
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

    public static InlineCollection RenderSprite(Bitmap? image)
    {
        if (image != null)
        {
            return
            [
                new InlineUIContainer(new Image()
                {
                    Source = image,
                    Width = image.Size.Width,
                    Height = image.Size.Height,
                })
            ];
        }

        return
        [
            new Run("<Sprite not available>")
            {
                Foreground = Brushes.Yellow,
                FontStyle = FontStyle.Italic
            }
        ];
    }

    // Assumes TextMeshPro rendering
    public static InlineCollection RenderText(GameAssets? gameAssets, string text)
    {
        InlineCollection inlines = [];
        var currentIndex = 0;

        IBrush foreground = Brushes.White;
        bool bold = false;
        bool italic = false;
        float fontSizePct = 100;
        float baseFontSize = 18; // TODO: Somewhat hardcoded right now

        while (currentIndex < text.Length)
        {
            var openBracketIndex = text.IndexOf('<', currentIndex);
            var closingBracketIndex = openBracketIndex != -1 ? text.IndexOf('>', openBracketIndex + 1) : -1;

            bool foundTag = closingBracketIndex != -1;

            var runLength = (foundTag ? openBracketIndex : text.Length) - currentIndex;

            if (runLength > 0)
            {
                inlines.Add(
                    new Run(text.Substring(currentIndex, runLength))
                    {
                        Foreground = foreground,
                        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
                        FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
                        FontSize = baseFontSize * fontSizePct / 100
                    }
                );
            }

            currentIndex += runLength;

            if (foundTag)
            {
                var tagContentsLength = closingBracketIndex - openBracketIndex - 1;
                var tag = text.Substring(openBracketIndex + 1, tagContentsLength);

                Match match;

                bool shouldPrintRaw = false;

                // TODO: This parsing sucks
                if (tag == "b")
                    bold = true;
                else if (tag == "/b")
                    bold = false;
                else if (tag == "i")
                    italic = true;
                else if (tag == "/i")
                    italic = false;
                else if (tag.StartsWith("sprite"))
                {
                    var indexMatch = Regex.Match(tag, "\\bindex=([0-9]+)\\b");

                    Bitmap? image;

                    if (gameAssets != null
                        && indexMatch.Success
                        && int.TryParse(indexMatch.Groups[1].Value, out var index)
                        && (image = gameAssets.GetTMPSprite(index)) != null)
                    {
                        inlines.Add(new InlineUIContainer(new Image()
                        {
                            Source = image,
                            Width = image.PixelSize.Width,
                            Height = image.PixelSize.Height,
                        }));
                    }
                    else
                    {
                        inlines.Add(new Run("<Sprite not available>")
                        {
                            Foreground = Brushes.Yellow,
                            FontStyle = FontStyle.Italic,
                            FontSize = baseFontSize
                        });
                    }
                }
                else if ((match = Regex.Match(tag, "^size=([0-9]*)%$")).Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out var pct))
                    {
                        fontSizePct = Math.Max(0, pct);
                    }
                    else
                    {
                        shouldPrintRaw = true;
                    }
                }
                else if ((match = Regex.Match(tag, "^color=\"([a-z]*)\"$")).Success)
                {
                    var color = match.Groups[1].Value;

                    foreground = color switch
                    {
                        "black" => Brushes.Black,
                        "blue" => Brushes.Blue,
                        "green" => Brushes.Green,
                        "orange" => Brushes.Orange,
                        "purple" => Brushes.Purple,
                        "red" => Brushes.Red,
                        "white" => Brushes.White,
                        "yellow" => Brushes.Yellow,
                        "grey" => Brushes.Gray,
                        _ => foreground
                    };
                }
                else if ((match = Regex.Match(tag, "^color=#([0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)$")).Success)
                {
                    var colorHex = match.Groups[1].Value;

                    var bytes = Convert.FromHexString(colorHex);

                    var alpha = bytes.Length == 4 ? bytes[3] : (byte)0xFF;

                    foreground = new ImmutableSolidColorBrush(new Color(alpha, bytes[0], bytes[1], bytes[2]));
                }
                else if (tag == "/color")
                    foreground = Brushes.White;
                else
                    shouldPrintRaw = true;
                
                if (shouldPrintRaw)
                {
                    inlines.Add(
                        new Run(text.Substring(openBracketIndex, tagContentsLength + 2))
                        {
                            Foreground = foreground,
                            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
                            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
                            FontSize = baseFontSize * fontSizePct / 100
                        }
                    );
                }

                currentIndex += tagContentsLength + 2;
            }
        }

        return inlines;
    }

    public static string ReplacePlaceholders(string path, string text)
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
        // TODO: Move to a JSON file
        var assembledNotes = key switch
        {
            "other:jsonthing" =>
                """
                As mentioned by the developer, this key is not necessary.
                Feel free to skip it.
                """,
            _ => "",
        };
        
        if (key.StartsWith("notes:") && key.EndsWith(":sprite"))
        {
            assembledNotes += "\nThe sprite that is shown when viewing the note (if any).";
            assembledNotes += "\nThe sprite will be rendered on the right if you have loaded the game assets.";
            assembledNotes += "\nThis should keep the same value from the English translation!";
        }

        if (key.StartsWith("pdaNotes:") && key.EndsWith(":sprite"))
        {
            assembledNotes += "\nCurrently unused.";
            assembledNotes += "\nThis should keep the same value from the English translation!";
        }
        
        if (key.StartsWith("notes:") && key.EndsWith(":text"))
        {
            assembledNotes += "\nPay attention to which in-game character wrote this note!";
            assembledNotes += "\nMilkies have good writing, with proper grammar and punctuation.";
            assembledNotes += "\nExperiments have mediocre writing, with missing punctuation and capitals.";
            assembledNotes += "\nDunes have bad writing, with grammar issues, and refer to themselves in third person.";
        }

        if (key.StartsWith("notes:") && key.EndsWith(":font"))
        {
            assembledNotes += "\nFonts represent the character who wrote the note.";
            assembledNotes += "\nCurrently, the valid options are 'experiment', 'milky', and 'dune'.";
            assembledNotes += "\nThis should keep the same value from the English translation!";
        }

        if (assembledNotes == "")
            return "<None>";

        return assembledNotes.Trim();
    }
}