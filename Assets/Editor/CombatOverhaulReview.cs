using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class CombatOverhaulReview
{
    public const string MenuHash="6c2a1751d11d452e9c205ed2bf22973f3cb048ec9b51854d7641bd64153e3ea0";
    [MenuItem("Medieval Warrior/Combat/Verify audio bank and protected menu")]
    public static void VerifyAudio()
    {
        AssetDatabase.Refresh();
        string folder="Assets/Resources/Audio";int count=0;
        foreach(string path in Directory.GetFiles(folder,"*.wav"))
        {
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(importer==null)throw new Exception("Missing audio importer: "+path);
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if(clip==null||clip.samples==0||clip.channels!=1)throw new Exception("Invalid audio resource: "+path);
            count++;
        }
        using(var sha=SHA256.Create())
        {
            var hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(folder+"/music_menu.wav"))).Replace("-","").ToLowerInvariant();
            if(hash!=MenuHash)throw new Exception("The protected menu WAV changed");
        }
        foreach(string name in new[]{"parry","thunder_guard","phantom_cleave","splinter","wave_horn","boss_horn","victory","defeat"})
            if(Resources.Load<AudioClip>("Audio/"+name)==null)throw new Exception("Missing combat cue: "+name);
        if(count<40)throw new Exception("Incomplete soundbank: "+count);
        Debug.Log($"[Combat audio] PASS: {count} imported mono WAVs. Protected menu hash and required combat cues verified.");
    }
}
