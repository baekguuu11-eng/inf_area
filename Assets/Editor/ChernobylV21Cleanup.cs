#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
[InitializeOnLoad]
public static class ChernobylV21Cleanup
{
    static ChernobylV21Cleanup() { EditorApplication.delayCall+=Run; }
    private static readonly Dictionary<string,string> Retired=new Dictionary<string,string>
    {
        {"Assets/Resources/Bosses/Chernobyl/SFX/CHN_Activation.ogg","0cf476f79bd84a76b2285061b99ca079294d5fb37ab32499f94d090898155ba7"},
        {"Assets/Resources/Bosses/Chernobyl/SFX/CHN_Explosion.ogg","1a696afbab70b8c54f3d9d31c52a4d7f600557fb5b616771f3ebc871eacd1319"},
        {"Assets/Resources/Bosses/Chernobyl/SFX/CHN_Overload.ogg","316222472bd3e10bd52bdb3766f5e8cc3195b1f11624bef8342013e7d3ed2b46"},
        {"Assets/Resources/Bosses/Chernobyl/SFX/CHN_Shutdown.ogg","9fd87683fc694a1ce9c8bd043b08237fcecc838c62c457f6aa1368daafe2fb37"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/14_Chernobyl_Hit.ogg","4a2b76f20052819c6ace57cc5ba55ec58deffcf74e3ed30694d619c582cc8e8e"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/15_Chernobyl_Heavy_Hit.ogg","ef29c3943ee701616f65e3d596c13eb3ff6b11ca5b3ac01e7bc923366a5c6f76"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/16_Grid_Warning.ogg","885b28175c7b511118bcffab95675b48bbac9005cf6514656ebdad979276b3ab"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/17_Grid_Explosion.ogg","cdda50600a4928f2f1b8371224d53c171ac799c1cf2c30b11999508985250b3b"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/18_Line_Sweep.ogg","3c7b3c9d856ee31fa1b92d29322f6ee472446627381595baeaba2af35c07135f"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/19_Cross_Blast.ogg","5574e69dd04e5f59322c5ddffde5978f077b09170ae5e29cec0bd9901828cd90"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/20_Checker_Pattern.ogg","ca12dd32103f401481d5aff56d5e9831174e99bb577139c09b07d046eb8b837e"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/21_Delayed_Tile_Warning.ogg","9d057b918024c787bbe6c00beed13a7ef72a3fa0c259add3cef36582792f4230"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/22_Safe_Zone_Shift.ogg","b44550fa8d3276c072ce7c3c1315d949f7a28a95bc22b3618ad4eea62eac8abe"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/23_Closing_Walls.ogg","01c7d15e90ae1b218812597d04c20e3df872367ebf22cd99d66dddd5fdb542a4"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/24_Rotating_Lines.ogg","ce286b2d637b0d71c230f0f12bc95c1d2a8cf0419aa13e66fcdde1f9096acae7"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/25_Target_Lock.ogg","cd4390dd2b24f09016a5fa1be63334fa0f1859811f4cf29c02e8e2750f6a2cd0"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/26_Shockwave_Charge.ogg","160584c22f05b89cf5deb232b9a69d1d61ea3d521a55b572071b6ae607398c09"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/27_Shockwave_Fire.ogg","3cb48d86dab63140cfb9aaeea4d4a6846abaca5831d157d8adbec0490da26343"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/28_Anti_Burst.ogg","05609bb296cbc287ae95ac8924c89809d69c89d7bb357cc6a117ec1b1ef65e09"},
        {"Assets/Resources/Bosses/Chernobyl/TeamSFXV20/29_Page_Transition.ogg","320c6aafb2c7a503eae508dc443b72d7f7c0a317bfd16813157ed400cd04cf16"},
        {"Assets/Resources/Bosses/JHL/JHL_BeamCharge.ogg","f103c1d10f669ad4722ff648bb64197dd519b03b99972c7f475c7b7d28cd9e30"},
        {"Assets/Resources/Bosses/JHL/JHL_BeamFire.ogg","c52da63f1049bfc101e6ad167787c36c9d8a2767c38ff4f0269a083bf6526b59"},
        {"Assets/Resources/Bosses/JHL/JHL_Compression.ogg","22807185e74eaf3c443b8ea57b9b26ae3f58d685958d2c5c384f44648da7a989"},
        {"Assets/Resources/Bosses/JHL/JHL_Death.ogg","2217da9c65702fb71b96113ed0c658ad388c32534c9756f403012f79126fa9c4"},
        {"Assets/Resources/Bosses/JHL/JHL_FullAccess.ogg","0cf476f79bd84a76b2285061b99ca079294d5fb37ab32499f94d090898155ba7"},
        {"Assets/Resources/Bosses/JHL/JHL_HandImpact.ogg","2ba5012301d0086d61aed17b6e71debea075fd79040b93947e37e3a93689a093"},
        {"Assets/Resources/Bosses/JHL/JHL_HandWallLock.ogg","702180244bb8fd6500c39d1b5b38c574ba23f8a62b98287dcacc627bf5567bc5"},
        {"Assets/Resources/Bosses/JHL/JHL_HandWindup.ogg","b5080d8ce8399d83391a9129e4e593e38abcdd7522008f776c4a94bd39006270"},
        {"Assets/Resources/Bosses/JHL/JHL_Hit.ogg","4cee55d1337483a0007c92c2a805595d8ed8b5128744c1b8d517c3ceace49572"},
        {"Assets/Resources/Bosses/JHL/JHL_IntroFace.ogg","a3b1024dff22a85deae07df9dad3691f10f430a646e8569c6da6840e1a380acf"},
        {"Assets/Resources/Bosses/JHL/JHL_IntroHand.ogg","0cf108b910a2007af552af578ab3ec992dafa11cfaabf8ce7852cb3e5997cea4"},
        {"Assets/Resources/Bosses/JHL/JHL_PhaseChange.ogg","316222472bd3e10bd52bdb3766f5e8cc3195b1f11624bef8342013e7d3ed2b46"},
        {"Assets/Resources/Bosses/JHL/JHL_Projectile.ogg","8062c456a8319620918847c16703d8ddd882f688a646ff1a35db48bd172c847e"},
        {"Assets/Resources/Bosses/JHL/JHL_RemoteLock.ogg","536a347174f538fc6d79013c0905963aca0fdb1f35d6609a7e2a432b4b9a57f5"},
        {"Assets/Resources/Bosses/JHL/JHL_Roar.ogg","8cc68d308425f29e5ac3f540965ce8afbf6878faedb801f6195675c196379845"},
        {"Assets/Resources/Bosses/JHL/JHL_Sweep.ogg","446d45b68483e9e1ce0bcc695b21f715ea93208b51fefc2a4b3ddbf4c724a44f"},
        {"Assets/Resources/ExternalAudioV18/SOURCES.json","a5b60fc450f3d85af2d39503f81d97e86dd23ae6393395237cb3e26b4355d032"},
        {"Assets/Resources/ExternalAudioV18/beam.ogg","a56d95794cd732d6c2d66ce488c14cf557fe526c282897c9a77675c2bd9b77e6"},
        {"Assets/Resources/ExternalAudioV18/charge.ogg","c2916f2a062c8ddd1aca2826d134fe90847037db31342726ffb0f9097afe339c"},
        {"Assets/Resources/ExternalAudioV18/death.ogg","3cb48d86dab63140cfb9aaeea4d4a6846abaca5831d157d8adbec0490da26343"},
        {"Assets/Resources/ExternalAudioV18/explosion.ogg","4b597d658d0ae101f0a030fbeea5fc3a4292ab85f017470a8254a8e7959cbd69"},
        {"Assets/Resources/ExternalAudioV18/heavy.ogg","e07045693e4a2b3d165c424e3dab4c781d9ff8880a386880ac89a51315d7f831"},
        {"Assets/Resources/ExternalAudioV18/hit.ogg","33b5e6e37c6e9d54e07bf5a89b12c76e879f40c1ea83cdd82714df1d6f9fec6d"},
        {"Assets/Resources/ExternalAudioV18/impact_LICENSE.txt","b49aa9c56b04528b95913de13e506a0f7c5e807b9925db9bfef86af1f91120db"},
        {"Assets/Resources/ExternalAudioV18/laser.ogg","72b589eadd41781257ac859e4f4d030222e623390e4a1a83f6d05329a6e026f1"},
        {"Assets/Resources/ExternalAudioV18/pulse.ogg","5574e69dd04e5f59322c5ddffde5978f077b09170ae5e29cec0bd9901828cd90"},
        {"Assets/Resources/ExternalAudioV18/scifi_LICENSE.txt","a9767b25c3533f69d03af136480efad08efba19a9f0d89616992b34c79fd6186"},
        {"Assets/Resources/ExternalAudioV18/slam.ogg","112d4f93ddcc370b410630f971c0f5d991856102da9c76bc5c5540d388e75aaa"},
        {"Assets/Resources/ExternalAudioV18/sweep.ogg","c9134651ebbd7c016f90393aee495ff8120bd343bb7569b689a7506987ebd6c2"},
        {"Assets/Resources/ExternalAudioV18/windup.ogg","5b7e07eccd4413ad9ef3720c2d14f9cc699f4c50111e9b2f48bd60e6f1a164c3"},
        {"Sound/03_Chernobyl/14_Chernobyl_Hit.ogg","4a2b76f20052819c6ace57cc5ba55ec58deffcf74e3ed30694d619c582cc8e8e"},
        {"Sound/03_Chernobyl/15_Chernobyl_Heavy_Hit.ogg","ef29c3943ee701616f65e3d596c13eb3ff6b11ca5b3ac01e7bc923366a5c6f76"},
        {"Sound/03_Chernobyl/16_Grid_Warning.ogg","885b28175c7b511118bcffab95675b48bbac9005cf6514656ebdad979276b3ab"},
        {"Sound/03_Chernobyl/17_Grid_Explosion.ogg","cdda50600a4928f2f1b8371224d53c171ac799c1cf2c30b11999508985250b3b"},
        {"Sound/03_Chernobyl/18_Line_Sweep.ogg","3c7b3c9d856ee31fa1b92d29322f6ee472446627381595baeaba2af35c07135f"},
        {"Sound/03_Chernobyl/19_Cross_Blast.ogg","5574e69dd04e5f59322c5ddffde5978f077b09170ae5e29cec0bd9901828cd90"},
        {"Sound/03_Chernobyl/20_Checker_Pattern.ogg","ca12dd32103f401481d5aff56d5e9831174e99bb577139c09b07d046eb8b837e"},
        {"Sound/03_Chernobyl/21_Delayed_Tile_Warning.ogg","9d057b918024c787bbe6c00beed13a7ef72a3fa0c259add3cef36582792f4230"},
        {"Sound/03_Chernobyl/22_Safe_Zone_Shift.ogg","b44550fa8d3276c072ce7c3c1315d949f7a28a95bc22b3618ad4eea62eac8abe"},
        {"Sound/03_Chernobyl/23_Closing_Walls.ogg","01c7d15e90ae1b218812597d04c20e3df872367ebf22cd99d66dddd5fdb542a4"},
        {"Sound/03_Chernobyl/24_Rotating_Lines.ogg","ce286b2d637b0d71c230f0f12bc95c1d2a8cf0419aa13e66fcdde1f9096acae7"},
        {"Sound/03_Chernobyl/25_Target_Lock.ogg","cd4390dd2b24f09016a5fa1be63334fa0f1859811f4cf29c02e8e2750f6a2cd0"},
        {"Sound/03_Chernobyl/26_Shockwave_Charge.ogg","160584c22f05b89cf5deb232b9a69d1d61ea3d521a55b572071b6ae607398c09"},
        {"Sound/03_Chernobyl/27_Shockwave_Fire.ogg","3cb48d86dab63140cfb9aaeea4d4a6846abaca5831d157d8adbec0490da26343"},
        {"Sound/03_Chernobyl/28_Anti_Burst.ogg","05609bb296cbc287ae95ac8924c89809d69c89d7bb357cc6a117ec1b1ef65e09"},
        {"Sound/03_Chernobyl/29_Page_Transition.ogg","320c6aafb2c7a503eae508dc443b72d7f7c0a317bfd16813157ed400cd04cf16"},
        {"Sound/04_JHL/31_JHL_Heavy_Hit_Stagger.ogg","fdd04d6f0032d4d57c134ad37f3587d2d00e58c9572715275bd8cf4f43593576"},
        {"Sound/04_JHL/32_Hand_Windup.ogg","315773e2a85694eda0f16dec373d90177043609dcd2b7b04e1e68225d458d19e"},
        {"Sound/04_JHL/33_Hand_Slam.ogg","8c7197bb3a1c504690319c3abe0e62a5423ee246dd70caab57963d0b7aa8144f"},
        {"Sound/04_JHL/34_Hand_Sweep.ogg","e678aca631495b7dfef4ac625f0349875ccac81a60f538d530e072241af3e4bd"},
        {"Sound/04_JHL/36_Hand_Wall_Lock.ogg","e94dfab6fd6558068ab3ea97e22e4b4f16b83ee440e6abf33ec4c4e9fbe8a70e"},
        {"Sound/04_JHL/37_Compression.ogg","dc0f5af8524269f6eae3b35b56e942a2e7aa725cf74433cdf7646896b76a27f8"},
        {"Sound/04_JHL/38_Hand_Prison.ogg","051b0eafc479695af4ca3607d41fd4be41bae7c21f4f9508d004722b09f1bd63"},
        {"Sound/04_JHL/39_Projectile_Fire.ogg","a464e34436f3c2a09f0e3a03dc0658a9036148062bbf293722007df4cfd8ff08"},
        {"Sound/04_JHL/40_Laser_Charge.ogg","9bb87740f71fb753ac6a06eb709b8b5dcbac273f1cb96cb110c76a59248937ce"},
        {"Sound/04_JHL/41_Laser_Fire.ogg","86c749483b40e1bba9bfea6a04e884d479e5481e52efb6f113341141de516b3b"},
        {"Sound/04_JHL/42_Massive_Tracking_Beam.ogg","8b3ec95ef1746767334a0c9c03b41dfb5873aaa73365bd0651fec9c42c0461e5"},
        {"Sound/04_JHL/43_Roar_Repulse.ogg","afa2946952224e8de4aeb6b4a85ae407bc7a5a37d26e0b3cb513bdaa967125aa"},
        {"Sound/04_JHL/44_JHL_Phase_Transition_Full_Access.ogg","9091c0d97faea18420e18937c5af8e7e0fd30348d876cf9a78c2b846e55c4b5c"},
    };
    private static void Run()
    {
        foreach(var item in Retired)
        {
            string p=item.Key;if(!File.Exists(p)) continue;
            string hash;
            using(SHA256 sha=SHA256.Create()) hash=System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant();
            if(hash!=item.Value) {Debug.LogWarning("V21: preserved modified audio: "+p);continue;}
            if(p.StartsWith("Assets/")) AssetDatabase.DeleteAsset(p);
            else File.Delete(p);
        }
    }
}
#endif
