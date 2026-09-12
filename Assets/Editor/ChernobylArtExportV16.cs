using UnityEngine;
using UnityEditor;
using System.IO;

public static class ChernobylArtExportV16
{
    [MenuItem("Tools/INFECTED AREA/Export Chernobyl V16 Pixel Layers")]
    public static void Export()
    {
        Sprite body, ring, core;
        if (!ChernobylArtV16.TryLoad(out body, out ring, out core))
        { Debug.LogError("Chernobyl V16 textures missing or Read/Write disabled."); return; }
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Chernobyl_V16_Export"));
        Directory.CreateDirectory(directory);
        foreach (Sprite sprite in new[] { body, ring, core })
            File.WriteAllBytes(Path.Combine(directory, sprite.texture.name + ".png"), sprite.texture.EncodeToPNG());
        Debug.Log("Chernobyl V16 pixel layers exported to " + directory);
    }
}
