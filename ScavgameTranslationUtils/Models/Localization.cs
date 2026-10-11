using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScavgameTranslationUtils.Models;

public class Localization
{
    private JsonObject _root;

    private Localization(JsonObject root)
    {
        _root = root;
    }

    public Localization()
    {
        _root = new JsonObject();
    }

    public string? Name
    {
        get => GetTextByPath("name");
        set => SetTextByPath("name", value);
    }
    
    public string? Description
    {
        get => GetTextByPath("description");
        set => SetTextByPath("description", value);
    }
    
    public string? Version
    {
        get => GetTextByPath("version");
        set => SetTextByPath("version", value);
    }
    
    public static Localization? LoadFromText(string text)
    {
        var node = JsonNode.Parse(text);

        if (node == null || node is not JsonObject objNode)
            return null;

        return new Localization(objNode);
    }

    // TODO: Async?
    public static Localization? LoadFromStream(Stream stream)
    {
        var node = JsonNode.Parse(stream);

        if (node == null || node is not JsonObject objNode)
            return null;

        return new Localization(objNode);
    }

    // TODO: Async?
    public void SaveToStream(Stream stream, JsonWriterOptions options)
    {
        using var jsonWriter = new Utf8JsonWriter(stream, options);
        _root.WriteTo(jsonWriter);
    }
    
    public List<string> GetPaths()
    {
        var paths = new List<string>();
        AddPaths(_root, "");
        return paths;

        void AddPaths(JsonNode? current, string currentPath)
        {
            if (current == null)
                return;
            
            switch (current)
            {
                case JsonArray arr:
                    var length = arr.Count;
                    for (int index = 0; index < length; index++)
                    {
                        AddPaths(arr[index], currentPath == "" ? $"{index}" : $"{currentPath}:{index}");
                    }
                    break;
                case JsonObject obj:
                    foreach (var (key, value) in obj)
                    {
                        AddPaths(value, currentPath == "" ? $"{key}" : $"{currentPath}:{key}");
                    }
                    break;
                case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                    paths.Add(currentPath);
                    break;
            }
        }
    }

    public void CopyOrderFrom(Localization source)
    {
        JsonNode? nextRoot = _root;
        CopyOrder(source._root, ref nextRoot);
        
        Debug.Assert(nextRoot != null && nextRoot is JsonObject);
        
        if (nextRoot is JsonObject nextRootObj)
            _root = nextRootObj;
        
        static void CopyOrder(JsonNode? source, ref JsonNode? target)
        {
            if (source == null || target == null)
                return;
            
            if (source is JsonObject objSource && target is JsonObject objTarget)
            {
                var keys = objSource.Select(x => x.Key).ToArray();

                var newTarget = new JsonObject();
                foreach (var (key, value) in objTarget.OrderBy(x => keys.IndexOf(x.Key)).ToArray())
                {
                    objTarget.Remove(key);
                    newTarget.Add(key, value);
                }
                target.ReplaceWith(newTarget);
                target = newTarget;

                foreach (var (key, nextSource) in objSource)
                {
                    if (newTarget.TryGetPropertyValue(key, out var nextTarget))
                        CopyOrder(nextSource, ref nextTarget);
                }
            }
            else if (source is JsonArray arrSource && target is JsonArray arrTarget)
            {
                for (int index = 0; index < Math.Min(arrSource.Count, arrTarget.Count); index++)
                {
                    var nextTarget = arrTarget[index];
                    CopyOrder(arrSource[index], ref nextTarget);
                }
            }
        }
    }

    public string[] GetObjectKeyOrder(string path)
    {
        var pathParts = path.Split(':');
        var node = GetNode(_root, pathParts, false);
        return node is JsonObject obj ? obj.Select(x => x.Key).ToArray() : [];
    }

    public int GetArrayCount(string path)
    {
        var pathParts = path.Split(':');
        var node = GetNode(_root, pathParts, false);
        return node is JsonArray arr ? arr.Count : 0;
    }

    public void SetObjectKeyOrder(string path, string[] preferredOrder)
    {
        var pathParts = path.Split(':');
        var node = GetNode(_root, pathParts, false);

        if (node is not JsonObject obj)
            return;

        var newNode = new JsonObject();

        foreach (var (key, value) in obj.OrderBy(x => preferredOrder.IndexOf(x.Key)))
        {
            newNode.Add(key, value);
        }
        
        obj.ReplaceWith(newNode);
    }

    public string? GetTextByPath(string path)
    {
        var pathParts = path.Split(':');
        var node = GetNode(_root, pathParts, false);
        return
            node != null && 
            node is JsonValue jsonValue && 
            jsonValue.TryGetValue(out string? str) ? str : null;
    }
    
    public bool SetTextByPath(string path, string? text, bool forceConform = true)
    {
        var pathParts = path.Split(':');
        var node = GetNode(_root, pathParts, true, forceConform);

        if (node != null && node.GetValueKind() == JsonValueKind.String)
        {
            node.ReplaceWith(JsonValue.Create(text));
            return true;
        }

        return false;
    }

    public bool DeleteTextByPath(string path, string? text)
    {
        var pathParts = path.Split(':');
        var node = GetNode(_root, pathParts, false);

        if (node != null && node.GetValueKind() == JsonValueKind.String && node.Parent != null)
        {
            switch (node.Parent)
            {
                case JsonArray array:
                    var index = array.IndexOf(node);

                    if (index != -1)
                    {
                        // Can only delete items at the end of the array; we do not want to leave gaps
                        if (index != array.Count - 1)
                            return false;
                        
                        array.Remove(node);
                    }
                    break;
                case JsonObject obj:
                    var propertyName = obj.FirstOrDefault(x => x.Value == node).Key;
                    
                    if (propertyName != null)
                        obj.Remove(propertyName);
                    break;
            }
            return true;
        }

        return false;
    }

    private static JsonNode? GetNode(JsonNode current, Span<string> parts, bool initIntermediaryNotes, bool forceConform = false)
    {
        if (parts.Length == 0)
            return current;
            
        // The (intermediary) node to set should be an array if the next property is an integer, and an object otherwise
        bool? backfillAsArray = parts.Length <= 1 ? null : int.TryParse(parts[1], out _);

        static JsonNode GetBackfill(bool? type) => type switch
        {
            null => "",
            false => new JsonObject(),
            true => new JsonArray()
        };
            
        switch (current)
        {
            case JsonArray arr:
                if (!int.TryParse(parts[0], out int index) || index < 0)
                    return null;

                if (initIntermediaryNotes)
                {
                    while (index >= arr.Count)
                        arr.Add(GetBackfill(backfillAsArray));
                }

                if (index >= arr.Count)
                    return null;

                if (forceConform)
                {
                    bool doesNotMatch =
                        backfillAsArray == true && arr[index] is not JsonArray ||
                        backfillAsArray == false && arr[index] is not JsonObject;
                    
                    if (doesNotMatch)
                        arr[index]!.ReplaceWith(GetBackfill(backfillAsArray));
                }

                return GetNode(arr[index]!, parts[1..], initIntermediaryNotes, forceConform);
            case JsonObject obj:
                if (!obj.TryGetPropertyValue(parts[0], out var value) && initIntermediaryNotes)
                    obj[parts[0]] = value = GetBackfill(backfillAsArray);

                if (value == null)
                    return null;
                
                if (forceConform)
                {
                    bool doesNotMatch =
                        backfillAsArray == true && value is not JsonArray ||
                        backfillAsArray == false && value is not JsonObject;

                    if (doesNotMatch)
                    {
                        var newValue = GetBackfill(backfillAsArray);
                        value.ReplaceWith(newValue);
                        value = newValue;
                    }
                }

                return GetNode(value, parts[1..], initIntermediaryNotes, forceConform);
            default:
                return null;
        }
    }
}