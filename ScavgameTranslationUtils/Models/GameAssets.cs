using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace ScavgameTranslationUtils.Models;

public class GameAssets : IDisposable
{
    private Dictionary<string, Bitmap> _imagesByPaths = new(StringComparer.InvariantCultureIgnoreCase);
    private Dictionary<int, Bitmap> _tmpSpritesByIndex = new();

    public Bitmap? GetGameSprite(string path) => _imagesByPaths.TryGetValue(path, out var image) ? image : null;
    public Bitmap? GetTMPSprite(int index) => _tmpSpritesByIndex.TryGetValue(index, out var image) ? image : null;
    
    public void LoadFrom(string gameDataPath)
    {
        var manager = new AssetsManager();
        
        /* TODO:
         * Mono.Cecil generates trim warnings. While I haven't noticed any issues, ideally we should have
         * *zero* trim warnings in the application and all of its dependencies.
         */
        manager.MonoTempGenerator = new MonoCecilTempGenerator(Path.Combine(gameDataPath, "Managed"));

        using var tpk = AssetLoader.Open(new Uri("avares://ScavgameTranslationUtils/Assets/lz4.tpk"));

        manager.LoadClassPackage(tpk);
        
        var globalGameManagers = manager.LoadAssetsFile(Path.Combine(gameDataPath, "globalgamemanagers"));
        var resourcesAssets = manager.LoadAssetsFile(Path.Combine(gameDataPath, "resources.assets"));
        var globalGameManagersAssets = manager.LoadAssetsFile(Path.Combine(gameDataPath, "globalgamemanagers.assets"));
        
        manager.LoadClassDatabaseFromPackage(globalGameManagers.file.Metadata.UnityVersion);

        var resourceManager = globalGameManagers.file.GetAssetsOfType(AssetClassID.ResourceManager)[0];
        var resourceManagerRoot = manager.GetBaseField(globalGameManagers, resourceManager);

        foreach (var keyPptr in resourceManagerRoot["m_Container.Array"])
        {
            var assetExt = manager.GetExtAsset(globalGameManagers, keyPptr[1]);
            if (assetExt.info == null)
                continue;

            AssetExternal texture = default;

            if (assetExt.info.TypeId == (int)AssetClassID.Sprite)
            {
                // Extract texture
                texture = manager.GetExtAsset(assetExt.file, manager.GetBaseField(assetExt.file, assetExt.info)["m_RD.texture"]);
            }
            else if (assetExt.info.TypeId == (int)AssetClassID.GameObject)
            {
                // Extract renderer, sprite, then texture
                AssetExternal spriteRenderer = default;

                foreach (var componentKeyPptr in manager.GetBaseField(assetExt.file, assetExt.info)["m_Component.Array"])
                {
                    var componentInstance = manager.GetExtAsset(assetExt.file, componentKeyPptr[0]);
                    if (componentInstance.info != null && componentInstance.info.TypeId == (int)AssetClassID.SpriteRenderer)
                    {
                        spriteRenderer = componentInstance;
                        break;
                    }
                }

                AssetExternal sprite = spriteRenderer.info != null
                    ? manager.GetExtAsset(assetExt.file, manager.GetBaseField(spriteRenderer.file, spriteRenderer.info)["m_Sprite"])
                    : default;

                texture = sprite.info != null
                    ? manager.GetExtAsset(assetExt.file, manager.GetBaseField(sprite.file, sprite.info)["m_RD.texture"])
                    : default;
            }

            if (texture.info != null)
            {
                _imagesByPaths[keyPptr[0].AsString] = LoadBitmap(texture);
            }
        }

        var monoscripts = globalGameManagersAssets.file.GetAssetsOfType(AssetClassID.MonoScript);
        var tmpSpriteAssetClass = monoscripts.FirstOrDefault(x => manager.GetBaseField(globalGameManagersAssets, x)["m_Name"].AsString == "TMP_SpriteAsset");
        
        var spriteAssets = tmpSpriteAssetClass != null
            ? resourcesAssets.file.GetAssetsOfType(AssetClassID.MonoBehaviour)
                .Where(x =>
                {
                    var script = manager.GetExtAsset(resourcesAssets, manager.GetBaseField(resourcesAssets, x)["m_Script"]);

                    return script.baseField != null && script.baseField["m_Name"].AsString == "TMP_SpriteAsset";
                })
                .FirstOrDefault()
            : null;

        AssetExternal spriteSheetObj = spriteAssets != null
            ? manager.GetExtAsset(resourcesAssets, manager.GetBaseField(resourcesAssets, spriteAssets)["spriteSheet"])
            : default;

        if (spriteSheetObj.baseField != null)
        {
            var baseField = manager.GetBaseField(resourcesAssets, spriteAssets);
            var arr = baseField["m_SpriteGlyphTable.Array"];

            foreach (var glyph in arr.Children)
            {
                var index = glyph["m_Index"].AsInt;
                var x = glyph["m_GlyphRect.m_X"].AsInt;
                var y = glyph["m_GlyphRect.m_Y"].AsInt;
                var width = glyph["m_GlyphRect.m_Width"].AsInt;
                var height = glyph["m_GlyphRect.m_Height"].AsInt;

                if (!_tmpSpritesByIndex.ContainsKey(index))
                    _tmpSpritesByIndex[index] = LoadBitmap(spriteSheetObj, x, y, width, height);
            }
        }
    }

    private static Bitmap LoadBitmap(AssetExternal texture, int x = 0, int y = 0, int width = -1, int height = -1)
    {
        var texFile = TextureFile.ReadTextureFile(texture.baseField);

        if (width == -1)
            width = texFile.m_Width;
        if (height == -1)
            height = texFile.m_Height;

        byte[] bgraRaw = texFile.FillPictureData(texture.file);

        GCHandle handle = GCHandle.Alloc(bgraRaw, GCHandleType.Pinned);
        try
        {
            using var original = new SKBitmap(texFile.m_Width, texFile.m_Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            original.SetPixels(handle.AddrOfPinnedObject());
            using var region = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

            // Need to flip vertically due to Avalonia
            using (var canvas = new SKCanvas(region))
            {
                canvas.Scale(1, -1, width / 2f, height / 2f);
                canvas.DrawBitmap(original, -x, -y);
            }

            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, region.GetPixels(), new PixelSize(width, height), new Vector(100, 100), width * 4);
        }
        finally
        {
            handle.Free();
        }
    }

    private void ReleaseUnmanagedResources()
    {
        foreach (var bitmap in _imagesByPaths)
            bitmap.Value.Dispose();

        foreach (var bitmap in _tmpSpritesByIndex)
            bitmap.Value.Dispose();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~GameAssets()
    {
        ReleaseUnmanagedResources();
    }
}