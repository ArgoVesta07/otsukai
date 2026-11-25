using UnityEditor;
using UnityEngine;

// Import settings for the blockout sprites in Assets/Art/Buildings and Assets/Art/Interior (Design/sprites/buildings.js):
// pixel-art sprites at 16 pixels per unit, point filter, no compression, centre pivot (Visual sits at half height).
public class BuildingSpriteImporter : AssetPostprocessor
{
    static readonly string[] Folders = { "Assets/Art/Buildings/", "Assets/Art/Interior/" };
    const float PixelsPerUnit = 16f;

    void OnPreprocessTexture()
    {
        bool ours = false;
        foreach (string f in Folders) ours |= assetPath.StartsWith(f);
        if (!ours) return;

        TextureImporter ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PixelsPerUnit;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
    }
}
