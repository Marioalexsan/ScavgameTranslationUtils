using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace ScavgameTranslationUtils.Models;

public static partial class Constants
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

        if (parts[0] == "name"
            || parts[0] == "description"
            || parts[0] == "version")
        {
            displayParts[0] = UpperFirst(parts[0]);
        }

        else if (parts[0] == "main"
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
                displayParts[3] = parts[3] switch
                {
                    "Item1" => "Text",
                    "Item2" => "Sprite",
                    "Item3" => "Font",
                    _ => UpperFirst(parts[3]),
                };
        }

        else if (parts[0] == "pdaNotes" && parts.Length >= 3)
        {
            displayParts[0] = UpperFirst(parts[0]);

            if (displayParts.Length >= 2)
                displayParts[1] = int.TryParse(parts[1], out var pdaIndex)
                    ? MapPdaNoteIndexToPdaNote(workspace, pdaIndex)
                    : parts[1];

            displayParts[2] = parts[2] switch
            {
                "Item1" => "Text",
                "Item2" => "Sprite",
                _ => UpperFirst(parts[2]),
            };
        }
        
        else if (parts[0] == "pauseQuotes")
        {
            displayParts[0] = "Pause Quotes";
            
            if (parts.Length >= 2)
                displayParts[1] = UpperFirst(parts[1]);
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
        var originalText = workspace.GetOriginalText(key);
        
        var assembledNotes = key switch
        {
            "name" =>
                """
                This should be the localized name of your language (i.e. "Polski" for Polish, etc.).
                It will be displayed on the selection button for your locale in the locale menu.
                """,
            "description" =>
                """
                This should be a localized description of your translation.
                It will be displayed when mousing over the selection button for your locale in the locale menu.
                You should include the contributor list at the end of the description.
                """,
            "version" =>
                """
                The version of the translation file as indicated by Orsoniks / moffee.
                Keep this the same as in the original.
                """,
            "other:jsonthing" =>
                """
                As mentioned by the developer, this key is not necessary.
                Feel free to skip it.
                """,
            "moodles:bleeding4dsc" =>
                """
                This may be a reference to "Memento mori" (latin for "remember [that you have] to die").
                https://en.wikipedia.org/wiki/Memento_mori.
                """,
            "moodles:thirst4dsc" =>
                """
                This may be a reference to Ecclesiastes 12:7 in the Bible.
                https://en.wikipedia.org/wiki/Ecclesiastes_12#Verse_7
                """,
            "moodles:sepsis3dsc" =>
                """
                This may be a reference to the poem "Because I could not stop for Death" by Emily Dickinson.
                https://en.wikipedia.org/wiki/Because_I_could_not_stop_for_Death
                """,
            "moodles:irradiated4dsc" =>
                """
                This may be a reference to the poem "Do not go gentle into that good night" by Dylan Thomas.
                https://en.wikipedia.org/wiki/Do_not_go_gentle_into_that_good_night
                """,
            _ => "",
        };

        if (key.StartsWith("pauseQuotes:"))
        {
            assembledNotes += "\nPause quotes are from the point of view of the \"Observer\"." +
                              "\nThe Observer is a \"paranormal entity that oversees everything and is driven by curiosity\" in the game's world.";
        }

        if (key.StartsWith("notes:") && key.EndsWith(":Item2"))
        {
            assembledNotes += "\nThe sprite that is shown when viewing the note (if any)." +
                              "\nThe sprite will be rendered on the right if you have loaded the game assets." +
                              "\nThis should keep the same value from the English translation!";
        }

        if (key.StartsWith("pdaNotes:") && key.EndsWith(":Item2"))
        {
            assembledNotes += "\nCurrently unused." +
                              "\nThis should keep the same value from the English translation!";
        }

        if (key.StartsWith("notes:") && key.EndsWith(":Item1"))
        {
            assembledNotes += "\nPay attention to which in-game character wrote this note!" + 
                              "\nMilkies have good writing, with proper grammar and punctuation." +
                              "\nExperiments have mediocre writing, with missing punctuation and capitals." +
                              "\nDunes have bad writing, with grammar issues, and refer to themselves in third person.";
        }

        if (key.StartsWith("notes:") && key.EndsWith(":Item3"))
        {
            assembledNotes += "\nFonts represent the character who wrote the note." +
                              "\nCurrently, the valid options are 'experiment', 'milky', and 'dune'." +
                              "\nThis should keep the same value from the English translation!";
        }
        
        if (originalText != null && originalText.Contains("<spoiler>"))
        {
            assembledNotes += "\nSpoilered content should be left as-is until it is revealed, i.e. \"<spoiler>\".";
        }

        if (assembledNotes == "")
            return "<None>";

        return assembledNotes.Trim();
    }

    public static bool HasTranslation(string key, string? original, string? translation)
    {
        return !string.IsNullOrWhiteSpace(translation)
            || translation == "" && original == "";
    }

    public static bool IsLikelyUntranslatedEnglish(string key, [NotNullWhen(true)] string? original, [NotNullWhen(true)] string? translation)
    {
        if (translation == null || original == null)
            return false;

        if (original != translation && KnownEnglishVersions.Values.All(x => string.IsNullOrWhiteSpace(x.GetTextByPath(key)) || x.GetTextByPath(key) != translation))
            return false; // Not equal to any non-empty English text from other versions

        if (original.All(x => char.IsPunctuation(x) || char.IsWhiteSpace(x)))
            return false; // Likely remains the same in translations

        return true;
    }

    [GeneratedRegex(@"^[\s.,;:?!\-_0-9\/\\'""{}[\]=+@~#]*$")]
    private static partial Regex OnlySymbolsRegex();

    /// <summary>
    /// Returns true if a key should normally remain identical in the translation.
    /// </summary>
    public static bool ShouldRemainIdenticalInTranslation(string key, string text)
    {
        // PDA sprites don't require "translation"
        if (key.StartsWith("notes:") && (key.EndsWith(":Item2") || key.EndsWith(":Item3")))
            return true;
        
        // PDA sprites (although unused right now) don't require translation
        if (key.StartsWith("pdaNotes:") && key.EndsWith(":Item2"))
            return true;
        
        // Spoilers should remain as-is
        if (text == "<spoiler>")
            return true;
        
        // Version should remain as-is
        if (key == "version")
            return true;
        
        // No actual text to translate here, so keep it identical
        if (OnlySymbolsRegex().IsMatch(text))
            return true;

        return false;
    }

    public static readonly string EnglishLocaleVersions = Path.Combine(Program.AppDataPath, "en_locales");

    public static string GetEnglishLocaleVersionPath(string version) =>
        Path.Combine(EnglishLocaleVersions, $"EN-{version}.json");
    
    public static Dictionary<string, Localization> KnownEnglishVersions { get; } = [];

    public static async Task<Dictionary<string, Localization>> FetchEnglishTranslationsAsync()
    {
        // Integrity checks, just in case the files are swapped with something malicious
        var localePaths = new Dictionary<string, (string Url, string Hash)>()
        {
            ["v5.1"] = (
                "https://raw.githubusercontent.com/orsoniks/scavgame-locale/refs/tags/v5.1/EN.json",
                "927B81F087664C8DFF06B1EF5011E9EB8B29581C8B489B5B26E29FBAE86E920B"
            ),
            ["v6.0"] = (
                "https://raw.githubusercontent.com/orsoniks/scavgame-locale/refs/tags/v6.0/EN.json",
                "754728144B867B5348E94F4124AF3F9491C6527E03032660E901A7F5896521CA"
            ),
            ["v6.1"] = (
                "https://raw.githubusercontent.com/orsoniks/scavgame-locale/refs/tags/v6.1/EN.json",
                "3FEC04F51139DF1B9312D8B10AE508E40DF68B2D00CB2BBC96A38DC7A0D62980"
            ),
            ["v7.0.1"] = (
                "https://raw.githubusercontent.com/orsoniks/scavgame-locale/refs/tags/v7.0.1/EN.json",
                "CD0D1C61FE202E532DA26FF3678852C60833A0AFEA8CEE04273B9424B000E080"
            ),
            ["1.0-pre3"] = (
                "https://raw.githubusercontent.com/orsoniks/scavgame-locale/refs/tags/v1.0-pre3/release/EN.json",
                "3D804E7491A5FC961D6C0A9D3D63CE168ADA1D70D0716B30DCB738A6E9436AAE"
            ),
        };

        var fetchedTranslations = new Dictionary<string, Localization>();

        // ReSharper disable once ShortLivedHttpClient - not used frequently
        using var httpClient = new HttpClient();
        var serializer = AppJsonContext.CreateContext(4);

        Directory.CreateDirectory(EnglishLocaleVersions);

        foreach (var (version, localeData) in localePaths)
        {
            var (url, hash) = localeData;
            try
            {
                var desiredPath = GetEnglishLocaleVersionPath(version);

                if (File.Exists(desiredPath))
                {
                    await using var stream = File.OpenRead(desiredPath);
                    fetchedTranslations[version] = Localization.LoadFromStream(stream)
                        ?? throw new InvalidOperationException($"Deserializer returned null for localization at {desiredPath}");
                    Program.LogDebug($"Read cached translation from {desiredPath}");
                    continue;
                }

                var response = await httpClient.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                using var sha256 = SHA256.Create();
                var responseHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(body));
                var expectedHash = Convert.FromHexString(hash);
                Program.LogDebug($"Got hash {Convert.ToHexString(responseHash)} for {version}");

                if (!responseHash.SequenceEqual(expectedHash))
                    throw new InvalidOperationException($"Hash integrity check failed for {version}");
                
                fetchedTranslations[version] = Localization.LoadFromText(body)
                    ?? throw new InvalidOperationException($"Deserializer returned null for localization at {desiredPath}");
                
                File.WriteAllText(desiredPath, body);
                Program.LogDebug($"Fetched and cached translation for {version} under {desiredPath}.");
            }
            catch (Exception e)
            {
                Program.LogDebug("Failed to retrieve translation files from the main repository.");
                Program.LogDebug($"Exception: {e}");
            }
        }

        return fetchedTranslations;
    }
}