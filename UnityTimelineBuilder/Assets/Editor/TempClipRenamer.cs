using UnityEditor;
using UnityEngine;

// 一時スクリプト: fixture FBX の take 名 (Anim|Walk / Anim|Run) を Walk / Run の
// clipAnimations として .meta に定義する。実行後に削除する。
public static class TempClipRenamer
{
    public static void Run()
    {
        const string path = "Packages/com.hidano.unity-timeline-builder/Tests/Fixtures/external-multiple-clips.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogError("[TempClipRenamer] ModelImporter not found: " + path);
            EditorApplication.Exit(1);
            return;
        }

        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
            clip.name = clip.name.EndsWith("Walk") ? "Walk" : "Run";
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip && !asset.name.StartsWith("__preview__"))
                Debug.Log("[TempClipRenamer] clip: " + asset.name);
        EditorApplication.Exit(0);
    }
}
