#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
[InitializeOnLoad]
public static class ChernobylV22Cleanup
{
    static ChernobylV22Cleanup() {EditorApplication.delayCall+=Run;}
    private static readonly Dictionary<string,string> Retired=new Dictionary<string,string>
    {
        {"Assets/Resources/Bosses/Chernobyl/V21/GridExplosion8.png","54824e1ae7eb91a328c33c1ceff0280c7e76b150d96ecad1e418244201cc95fa"},
    };
    private static void Run()
    {
        foreach(var pair in Retired)
        {
            if(!File.Exists(pair.Key)) continue;
            string hash;using(var sha=SHA256.Create()) hash=System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(pair.Key))).Replace("-","").ToLowerInvariant();
            if(hash==pair.Value) AssetDatabase.DeleteAsset(pair.Key);
            else Debug.LogWarning("V22: modified old explosion sheet retained: "+pair.Key);
        }
    }
}
#endif
