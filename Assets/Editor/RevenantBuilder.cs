using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RevenantBuilder
{
    [Serializable] public class Bone { public string name; public int parent; public Vector3 position; public Matrix4x4 frame; }
    [Serializable] public class Rig { public Bone[] bones; }
    [Serializable] public class Grip { public string side; public Vector3 position,along,normal,distal,bladeTip; }
    [Serializable] public class Part
    {
        public string name;
        public Vector3[] vertices,normals;
        public Vector2[] uv;
        public Color[] colors;
        public int[] tiles,triangles,skinIndices;
        public float[] skinWeights;
        public Grip[] grips;
    }
    [Serializable] public class Export {public Part[] parts;}
    const string Destination="Assets/Resources/Revenants";
    public const string Source="Assets/Prefabs/Zombie1.prefab";
    [MenuItem("Medieval Warrior/Revenants/Export animation skeleton")]
    public static void ExportRig()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
        var bones=go.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s=>s.bones).Where(b=>b!=null).Distinct().ToArray();
        var rig=new Rig{bones=bones.Select(b=>new Bone{name=b.name,parent=Array.IndexOf(bones,b.parent),position=b.position,frame=b.localToWorldMatrix}).ToArray()};
        Directory.CreateDirectory("Tools/ArtPipeline/Revenants");
        File.WriteAllText("Tools/ArtPipeline/Revenants/rig.json",JsonUtility.ToJson(rig,true));
        UnityEngine.Object.DestroyImmediate(go);Debug.Log("[Revenants] Exported "+bones.Length+" animation joints");
    }

    [MenuItem("Medieval Warrior/Revenants/Import Blender roster")]
    public static void Build()
    {
        var input=JsonUtility.FromJson<Export>(File.ReadAllText("Tools/ArtPipeline/Revenants/meshes.json"));
        var reference=JsonUtility.FromJson<Rig>(File.ReadAllText("Tools/ArtPipeline/Revenants/rig.json"));
        Directory.CreateDirectory(Destination);AssetDatabase.Refresh();
        var data=AssetDatabase.LoadAssetAtPath<RevenantAsset>(Destination+"/Roster.asset");
        if(data==null){data=ScriptableObject.CreateInstance<RevenantAsset>();AssetDatabase.CreateAsset(data,Destination+"/Roster.asset");}
        data.bones=reference.bones.Select(b=>b.name).ToArray();data.forms=new RevenantAsset.Form[ZombieTypes.Count];
        var material=AssetDatabase.LoadAssetAtPath<Material>(Destination+"/Revenant.mat");
        if(material==null){material=new Material(Shader.Find("Medieval Warrior/Revenant PBR"));AssetDatabase.CreateAsset(material,Destination+"/Revenant.mat");}
        foreach(string label in new[]{"Albedo","Normal","Surface","Skin0","Skin1","Skin2","Skin3","SkinNormal"})
        {
            string path=Destination+"/Textures/"+label+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=label=="Albedo"||label.StartsWith("Skin")&&label!="SkinNormal";
            importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            if(label=="Albedo"||label=="Normal"||label=="Surface")
                material.SetTexture(label=="Albedo"?"_MainTex":label=="Normal"?"_NormalAtlas":"_SurfaceAtlas",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }
        material.SetTexture("_SkinTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Destination+"/Textures/Skin0.png"));
        material.SetTexture("_SkinNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(Destination+"/Textures/SkinNormal.png"));
        data.skinSurfaces=new Material[4];
        for(int i=0;i<4;i++)
        {
            string path=Destination+"/Revenant-Skin"+i+".mat";var skin=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(skin==null){skin=new Material(material);AssetDatabase.CreateAsset(skin,path);}else skin.CopyPropertiesFromMaterial(material);
            skin.SetTexture("_SkinTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Destination+"/Textures/Skin"+i+".png"));data.skinSurfaces[i]=skin;EditorUtility.SetDirty(skin);
        }
        data.surface=material;EditorUtility.SetDirty(material);
        var hairMaterial=AssetDatabase.LoadAssetAtPath<Material>(Destination+"/Hair.mat");
        if(hairMaterial==null){hairMaterial=new Material(Shader.Find("Medieval Warrior/Revenant Hair"));AssetDatabase.CreateAsset(hairMaterial,Destination+"/Hair.mat");}
        hairMaterial.SetTexture("_MainTex",material.GetTexture("_MainTex"));hairMaterial.SetTexture("_NormalAtlas",material.GetTexture("_NormalAtlas"));data.hairSurface=hairMaterial;EditorUtility.SetDirty(hairMaterial);
        foreach(var p in input.parts)
        {
            var mesh=new Mesh{name="Revenant "+p.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            bool hairPart=p.name.Contains("_Hair");
            var headInverse=reference.bones.First(b=>b.name=="Base HumanHead").frame.inverse;
            mesh.vertices=hairPart?p.vertices.Select(v=>headInverse.MultiplyPoint3x4(v)).ToArray():p.vertices;
            mesh.normals=hairPart?p.normals.Select(v=>headInverse.MultiplyVector(v).normalized).ToArray():p.normals;mesh.uv=p.uv;
            mesh.uv2=p.tiles.Select(t=>new Vector2(t,0)).ToArray();mesh.colors=p.colors;mesh.triangles=p.triangles;
            mesh.bindposes=reference.bones.Select(b=>b.frame.inverse).ToArray();
            mesh.boneWeights=Enumerable.Range(0,p.vertices.Length).Select(i=>
            {
                int j=i*4;
                if(Mathf.Abs(p.skinWeights.Skip(j).Take(4).Sum()-1)>.0001f)throw new Exception("Invalid skin weights: "+p.name+" "+i);
                return new BoneWeight{boneIndex0=p.skinIndices[j],boneIndex1=p.skinIndices[j+1],boneIndex2=p.skinIndices[j+2],boneIndex3=p.skinIndices[j+3],
                    weight0=p.skinWeights[j],weight1=p.skinWeights[j+1],weight2=p.skinWeights[j+2],weight3=p.skinWeights[j+3]};
            }).ToArray();
            mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=Destination+"/"+p.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null)AssetDatabase.CreateAsset(mesh,path);
            else
            {
                // CopySerialized doesn't reliably refresh Mesh's native vertex
                // streams when layouts/size change. Use the same explicit upload
                // path as the warrior importer, preserving this asset's GUID.
                existing.Clear();existing.indexFormat=mesh.indexFormat;
                existing.vertices=mesh.vertices;existing.normals=mesh.normals;
                existing.uv=mesh.uv;existing.uv2=mesh.uv2;existing.colors=mesh.colors;
                existing.triangles=mesh.triangles;existing.bindposes=mesh.bindposes;existing.boneWeights=mesh.boneWeights;
                existing.tangents=mesh.tangents;existing.bounds=mesh.bounds;existing.UploadMeshData(false);
                UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;
            }
            if(mesh.vertexCount!=p.vertices.Length||!mesh.uv2.Select(v=>(int)v.x).SequenceEqual(p.tiles))
                throw new Exception("The live vertex streams do not match the Blender export: "+p.name);
            if(p.name.Contains("_LOD"))
            {
                var target=data.forms[(int)Enum.Parse(typeof(ZombieType),p.name.Split('_')[0])];
                if(p.name.EndsWith("LOD1"))target.lod1=mesh;else target.lod2=mesh;
                EditorUtility.SetDirty(mesh);continue;
            }
            if(hairPart)
            {
                var target=data.forms[(int)Enum.Parse(typeof(ZombieType),p.name.Split('_')[0])];
                target.hair[int.Parse(p.name.Substring(p.name.Length-1))]=mesh;EditorUtility.SetDirty(mesh);continue;
            }
            var form=new RevenantAsset.Form{type=(ZombieType)Enum.Parse(typeof(ZombieType),p.name),mesh=mesh,hair=new Mesh[3]};
            form.leftWristRest=Quaternion.Inverse(reference.bones.First(b=>b.name=="Base HumanLArmForearm").frame.rotation)*reference.bones.First(b=>b.name=="Base HumanLArmPalm").frame.rotation;
            foreach(var g in p.grips)
            {
                var b=reference.bones.First(x=>x.name=="Base Human"+g.side+"ArmPalm");
                var grip=new RevenantAsset.Grip{position=b.frame.inverse.MultiplyPoint3x4(g.position),along=b.frame.inverse.MultiplyVector(g.along),
                    normal=b.frame.inverse.MultiplyVector(g.normal),distal=b.frame.inverse.MultiplyVector(g.distal)};
                grip.rotation=Quaternion.LookRotation(grip.normal,Vector3.Cross(grip.normal,grip.along));
                if(g.side=="R")
                {
                    form.right=grip;form.bladeBase=grip.position+grip.along*.075f;
                    form.bladeTip=b.frame.inverse.MultiplyPoint3x4(g.bladeTip);
                }
                else form.left=grip;
            }
            data.forms[(int)form.type]=form;EditorUtility.SetDirty(mesh);
        }
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        RevenantAnimationBuilder.Build(data,reference);
        // Keep the legacy rig as an authoring reference only. The shipped spawn
        // prefab has no old meshes/materials or humanoid-avatar dependency.
        var shell=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));shell.name="Revenant";
        foreach(var old in shell.GetComponentsInChildren<Renderer>())UnityEngine.Object.DestroyImmediate(old);
        foreach(var old in shell.GetComponentsInChildren<MeshFilter>())UnityEngine.Object.DestroyImmediate(old);
        // The unanimated actor root owns navigation. Pelvis translation remains
        // an ordinary keyed channel, including the fall in both death clips.
        var generic=AvatarBuilder.BuildGenericAvatar(shell,"");generic.name="RevenantAvatar";
        var avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Destination+"/RevenantAvatar.asset");
        if(avatar==null){AssetDatabase.CreateAsset(generic,Destination+"/RevenantAvatar.asset");avatar=generic;}
        else{EditorUtility.CopySerialized(generic,avatar);UnityEngine.Object.DestroyImmediate(generic);}
        if(!avatar.isValid||avatar.isHuman)throw new Exception("Invalid generic Revenant avatar");
        data.avatar=avatar;EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        var animator=shell.GetComponent<Animator>();animator.avatar=avatar;animator.runtimeAnimatorController=data.forms[0].controller;
        var prefab=PrefabUtility.SaveAsPrefabAsset(shell,Destination+"/Revenant.prefab");UnityEngine.Object.DestroyImmediate(shell);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab,out string guid,out long fileID);
        const string scene="Assets/Scenes/Semi-Final.unity";
        string sceneText=File.ReadAllText(scene);
        string linked=System.Text.RegularExpressions.Regex.Replace(sceneText,@"(?m)^  zombiePrefab: .*",$"  zombiePrefab: {{fileID: {fileID}, guid: {guid}, type: 3}}");
        if(linked!=sceneText){File.WriteAllText(scene,linked);AssetDatabase.ImportAsset(scene);}
        Debug.Log("[Revenants] Imported eight anatomical forms with one shared PBR atlas");
    }
    public static void BuildAndReview(){Build();RevenantReview.Run();}
    public static void BuildAndSmoke(){Build();RevenantReview.Run();SmokeTest.RunBatch();}
    public static void AnimateAndReview()
    {
        RevenantAnimationBuilder.Build(AssetDatabase.LoadAssetAtPath<RevenantAsset>(Destination+"/Roster.asset"),JsonUtility.FromJson<Rig>(File.ReadAllText("Tools/ArtPipeline/Revenants/rig.json")));
        RevenantReview.Run();
    }
}
