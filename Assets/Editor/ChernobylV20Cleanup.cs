#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Security.Cryptography;
[InitializeOnLoad]
public static class ChernobylV20Cleanup
{
    static ChernobylV20Cleanup() { EditorApplication.delayCall += Cleanup; }
    private static void Cleanup()
    {
        const string path="Assets/Resources/ExternalAudioV18/metal.ogg";
        if(!File.Exists(path)) return;
        string hash;
        using(SHA256 sha=SHA256.Create()) hash=System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        // Remove only the exact unused V18 file. User replacements are preserved.
        if(hash=="956c6612a256aa1a67a2327fffe2454f6b1d82e4c1c2be28fd66916335d5b1d6") AssetDatabase.DeleteAsset(path);
    }
}
#endif
