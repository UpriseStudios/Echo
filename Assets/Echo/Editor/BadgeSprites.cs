using System.Collections.Generic;
using Echo.Editor.Art;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace Echo.Editor
{
    /// <summary>
    /// The badge icons as a TextMeshPro sprite asset (a user revision: little icons instead of [LABELS]), rebuilt on every
    /// scene build from PixelBadges: board rows and profile lines write <c>&lt;sprite name="duel_gold"&gt;</c>
    /// (BadgeNames.Icon). It has no face metrics of its own, so TMP sizes each icon to the line's ascent.
    /// </summary>
    public static class BadgeSprites
    {
        public const string TexturePath = EchoArt.Dir + "/ui_badges.png";
        public const string AssetPath = "Assets/Echo/Content/Fonts/Badges.asset";

        public static TMP_SpriteAsset Build()
        {
            PixelCanvas atlas = PixelBadges.Atlas(out List<string> names);
            EchoArt.Write(TexturePath, atlas);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            asset.spriteSheet = texture;
            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                var glyph = new TMP_SpriteGlyph((uint)i,
                    new GlyphMetrics(PixelBadges.Cell, PixelBadges.Cell, 0, PixelBadges.Cell - 2, PixelBadges.Cell),
                    new GlyphRect(i * PixelBadges.Cell, 0, PixelBadges.Cell, PixelBadges.Cell), 1f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = names[i] });
            }

            if (asset.material == null)
            {
                var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "Badges Material" };
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }
            asset.material.SetTexture(ShaderUtilities.ID_MainTex, texture);
            var so = new SerializedObject(asset);
            so.FindProperty("m_Version").stringValue = "1.1.0";
            so.ApplyModifiedPropertiesWithoutUndo();
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
