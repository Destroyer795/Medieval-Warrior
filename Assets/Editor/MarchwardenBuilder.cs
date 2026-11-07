using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The original pack supplies the animation skeleton only. All replacement geometry is
// authored by Tools/ArtPipeline/marchwarden.py in Blender.
public static class MarchwardenBuilder
{
    public const string Source = "Assets/Toon_RTS_demo/models/ToonRTS_demo_Knight.FBX";
    [Serializable] public class Rig { public Bone[] bones; public Vector3[] vertices; public Matrix4x4 renderer; }
    [Serializable] public class Bone { public string name; public Vector3 position; public Matrix4x4 bind; }
    [Serializable] public class Export { public Part[] parts; public Surface[] surfaces; public float statureScale=1; public ArmorSource[] armorJoints; public HandGripSource[] handGrips; }
    [Serializable] public class HandGripSource { public string side; public Vector3 position,along,normal,drawContact; }
    [Serializable] public class ArmorSource { public int parent; public float angle; public Vector3 pivot; public Vector3[] probes; }
    [Serializable] public class Surface { public string name; public Color color; public float metal,smoothness; }
    [Serializable] public class Part {
        public string name;
        public Vector3[] vertices, normals;
        public Vector2[] uv,detailUV;
        public int[] triangles, materials, bones;
        public int[] bones2;
        public float[] blends;
        public int[] skinIndices;
        public int[] pieceIndices;
        public float[] skinWeights;
        public Shape[] shapes;
    }
    [Serializable] public class Shape { public string name; public int[] indices; public Vector3[] deltas; }

    const string Destination = "Assets/Resources/Marchwarden";

    static void AttachShieldGrip(Mesh shield,Part handle,Matrix4x4 pose)
    {
        int start=shield.vertexCount;
        float anchorCorrection=.006f-pose.MultiplyPoint3x4(new Vector3(0,0,.075f)).z;
        var vertices=shield.vertices.Concat(handle.vertices.Select(v=>{
            Vector3 point=pose.MultiplyPoint3x4(v);
            // Land the leather anchor ends on the plate's rear surface.
            // The bar itself remains exactly inside the fingers.
            point.z+=Mathf.Clamp01((v.z-.02f)/.055f)*anchorCorrection;
            return point;
        })).ToArray();
        var normals=shield.normals.Concat(handle.normals.Select(n=>pose.MultiplyVector(n).normalized)).ToArray();
        var uv=shield.uv.Concat(handle.uv).ToArray();var detail=shield.uv2.Concat(handle.detailUV).ToArray();
        var triangles=Enumerable.Range(0,shield.subMeshCount).Select(s=>shield.GetTriangles(s).Concat(
            Enumerable.Range(0,handle.materials.Length).Where(t=>handle.materials[t]==s)
                .SelectMany(t=>handle.triangles.Skip(t*3).Take(3).Select(i=>i+start))).ToArray()).ToArray();
        shield.vertices=vertices;shield.normals=normals;shield.uv=uv;shield.uv2=detail;
        for(int s=0;s<triangles.Length;s++)shield.SetTriangles(triangles[s],s);
        shield.RecalculateBounds();shield.RecalculateTangents();EditorUtility.SetDirty(shield);
    }

    public static void BuildAndSmoke()
    {
        if(!File.Exists("Artifacts/ClothReviewOnly.txt")&&!File.Exists("Artifacts/PerformanceReviewOnly.txt")&&!File.Exists("Artifacts/RunnerReviewOnly.txt")&&!File.Exists("Artifacts/DamageReviewOnly.txt"))
        {
            if(!File.Exists("Artifacts/ReuseArtExports.txt"))Build();
            CombatAnimationBuilder.Build();
            SwordClearanceReview.RenderComparison();
            SwordClearanceReview.Run();
        }
        SmokeTest.RunBatch();
    }

    [MenuItem("Medieval Warrior/Marchwarden/Import Blender exports")]
    public static void Build()
    {
        var input = JsonUtility.FromJson<Export>(File.ReadAllText("Tools/ArtPipeline/Marchwarden/meshes.json"));
        if(input.surfaces==null||input.surfaces.Length<8)throw new InvalidOperationException("Regenerate the Blender export with its surface palette first");
        var reference = JsonUtility.FromJson<Rig>(File.ReadAllText("Tools/ArtPipeline/Marchwarden/rig.json"));
        Directory.CreateDirectory(Destination);
        AssetDatabase.Refresh();
        ConfigureSurfaces();
        var data = AssetDatabase.LoadAssetAtPath<MarchwardenAsset>(Destination + "/Warrior.asset");
        if (data == null) { data = ScriptableObject.CreateInstance<MarchwardenAsset>(); AssetDatabase.CreateAsset(data, Destination + "/Warrior.asset"); }
        data.statureScale=input.statureScale>0?input.statureScale:1;
        int pelvisBone=Array.FindIndex(reference.bones,b=>b.name=="Bip001 Pelvis");
        Matrix4x4 toPelvis=reference.bones[pelvisBone].bind*reference.renderer.inverse;
        var armorFrames=input.armorJoints.Select(j=>Matrix4x4.TRS(j.pivot,
            Quaternion.LookRotation(new Vector3(Mathf.Sin(j.angle),0,Mathf.Cos(j.angle)),Vector3.up),Vector3.one)).ToArray();
        data.armorJoints=input.armorJoints.Select((j,i)=>new MarchwardenAsset.ArmorJoint{
            parent=j.parent,pelvisFrame=toPelvis*armorFrames[i],
            probes=j.probes.Select(p=>armorFrames[i].inverse.MultiplyPoint3x4(p)).ToArray()}).ToArray();
        data.quiverSuspension=new Vector3(-.055f,.315f,0);
        data.quiverMount=toPelvis*Matrix4x4.TRS(new Vector3(.27f,.735f,-.27f),Quaternion.Euler(-12,0,-7),Vector3.one);
        foreach (var part in input.parts)
        {
            if(part.name=="ShieldHandle")continue; // Fit to the palm after posing the guard.
            var mesh = new Mesh { name = "Marchwarden " + part.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            bool body = part.name == "Body"||part.name=="Tunic";
            var convert = body||part.name=="WaistArmor" ? reference.renderer.inverse : Matrix4x4.identity;
            mesh.vertices = part.vertices.Select(v => convert.MultiplyPoint3x4(v)).ToArray();
            mesh.normals = part.normals.Select(n => convert.MultiplyVector(n).normalized).ToArray();
            mesh.uv = part.uv;
            if(part.detailUV==null||part.detailUV.Length!=part.vertices.Length)
                throw new InvalidOperationException("Regenerate the Blender mesh with its independent detail UVs");
            mesh.uv2 = part.detailUV;
            mesh.subMeshCount = part.name=="Tunic"?1:input.surfaces.Length;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var indices = Enumerable.Range(0, part.materials.Length).Where(t => part.name=="Tunic"||part.materials[t] == s)
                    .SelectMany(t => part.triangles.Skip(t*3).Take(3)).ToArray();
                mesh.SetTriangles(indices, s);
            }
            if (body)
            {
                mesh.bindposes = reference.bones.Select(b => b.bind).ToArray();
                bool fourWeights=part.skinIndices!=null&&part.skinWeights!=null;
                if(fourWeights&&(part.skinIndices.Length!=part.vertices.Length*4||part.skinWeights.Length!=part.vertices.Length*4))
                    throw new InvalidOperationException("Incomplete four-influence skin export");
                mesh.boneWeights = part.bones.Select((b,i) => {
                    if(fourWeights)
                    {
                        int at=i*4;float sum=0;
                        for(int influence=0;influence<4;influence++)
                        {
                            int index=part.skinIndices[at+influence];float weight=part.skinWeights[at+influence];
                            if(index<0||index>=reference.bones.Length||weight<0||!float.IsFinite(weight))
                                throw new InvalidOperationException("Invalid skin influence at vertex "+i);
                            sum+=weight;
                        }
                        if(Mathf.Abs(sum-1)>.0001f)throw new InvalidOperationException("Unnormalized skin weights at vertex "+i);
                        return new BoneWeight {boneIndex0=part.skinIndices[at],boneIndex1=part.skinIndices[at+1],
                            boneIndex2=part.skinIndices[at+2],boneIndex3=part.skinIndices[at+3],
                            weight0=part.skinWeights[at],weight1=part.skinWeights[at+1],
                            weight2=part.skinWeights[at+2],weight3=part.skinWeights[at+3]};
                    }
                    float blend = part.blends != null && i < part.blends.Length ? part.blends[i] : 0;
                    int other = blend > 0 ? part.bones2[i] : b;
                    return new BoneWeight { boneIndex0 = blend > .5f ? other : b, weight0 = Mathf.Max(1-blend, blend),
                        boneIndex1 = blend > .5f ? b : other, weight1 = Mathf.Min(1-blend, blend) };
                }).ToArray();
                if(part.shapes!=null)foreach(var shape in part.shapes)
                {
                    if(shape.indices.Length!=shape.deltas.Length)throw new InvalidOperationException("Incomplete garment morph: "+shape.name);
                    var deltas=new Vector3[mesh.vertexCount];
                    for(int i=0;i<shape.indices.Length;i++)deltas[shape.indices[i]]=convert.MultiplyVector(shape.deltas[i]);
                    mesh.AddBlendShapeFrame(shape.name,100,deltas,null,null);
                }
            }
            if(part.name=="WaistArmor")
            {
                if(part.pieceIndices.Length!=part.vertices.Length)throw new InvalidOperationException("Missing rigid armor weights");
                mesh.bindposes=armorFrames.Select(frame=>frame.inverse*reference.renderer).ToArray();
                mesh.boneWeights=part.pieceIndices.Select(piece=>new BoneWeight{boneIndex0=piece,weight0=1}).ToArray();
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            string path = Destination + "/" + part.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Rebuild through Mesh's setters so resized vertex/index buffers are
                // uploaded as well as serialized when regenerating an existing asset.
                existing.Clear(); existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.uv = mesh.uv;
                existing.uv2 = mesh.uv2;
                existing.subMeshCount = mesh.subMeshCount;
                for (int s = 0; s < mesh.subMeshCount; s++) existing.SetTriangles(mesh.GetTriangles(s), s);
                existing.bindposes = mesh.bindposes; existing.boneWeights = mesh.boneWeights;
                existing.ClearBlendShapes();
                for(int shape=0;shape<mesh.blendShapeCount;shape++)
                {
                    var deltas=new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(shape,0,deltas,null,null);
                    existing.AddBlendShapeFrame(mesh.GetBlendShapeName(shape),100,deltas,null,null);
                }
                existing.tangents = mesh.tangents; existing.bounds = mesh.bounds;
                existing.UploadMeshData(false); EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(mesh); mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, path);
            if (mesh.vertexCount != part.vertices.Length || mesh.triangles.Length != part.triangles.Length)
                throw new InvalidOperationException("Imported geometry does not match the Blender export: " + part.name);
            switch (part.name) {
                case "Body": data.body = mesh; break; case "Sword": data.sword = mesh; break;
                case "Tunic": data.tunic=mesh;break;
                case "WaistArmor": data.waistArmor=mesh;break;
                case "Shield": data.shield = mesh; break; case "Bow": data.bow = mesh; break;
                case "Quiver": data.quiver = mesh; break; case "Arrow": data.arrow = mesh; break;
            }
        }
        string[] names = input.surfaces.Select(s=>s.name).ToArray();
        Color[] colors = input.surfaces.Select(s=>s.color).ToArray();
        data.materials = new Material[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string path = Destination + "/" + names[i] + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
            mat.name = "Marchwarden " + names[i]; mat.color = colors[i];
            mat.SetFloat("_Metallic", input.surfaces[i].metal);
            mat.SetFloat("_Glossiness", input.surfaces[i].smoothness);
            mat.enableInstancing = true;
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Destination + "/Textures/Finish.png");
            mat.SetTexture("_DetailAlbedoMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Destination+"/Textures/"+names[i]+"_Detail.png"));
            mat.SetFloat("_UVSec",1);
            mat.EnableKeyword("_DETAIL_MULX2");
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Destination + "/Textures/Finish_Normal.png"));
            mat.SetFloat("_BumpScale", .85f);
            if(i==8)
            {
                // The curved finger webs expose tangent seams in the baked
                // equipment atlas. Keep skin's fine normal tile on its own.
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Destination+"/Textures/Skin_Normal.png"));
                mat.SetFloat("_BumpScale",.35f);
            }
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Destination + "/Textures/Finish_Surface.png"));
            mat.EnableKeyword("_METALLICGLOSSMAP");
            mat.SetFloat("_GlossMapScale", 1f);
            if(i==8)mat.SetFloat("_GlossMapScale",.6f); // Broad skin highlights instead of a wet plastic sheen.
            mat.SetFloat("_SpecularHighlights",1);mat.SetFloat("_GlossyReflections",1);
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");mat.DisableKeyword("_GLOSSYREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat); data.materials[i] = mat;
        }
        data.meshToCharacter=reference.renderer;
        string linenPath=Destination+"/LinenSimulation.mat";
        var linen=AssetDatabase.LoadAssetAtPath<Material>(linenPath);
        if(linen==null){linen=new Material(data.materials[2]);AssetDatabase.CreateAsset(linen,linenPath);}
        linen.CopyPropertiesFromMaterial(data.materials[2]);linen.shader=Shader.Find("MedievalWarrior/LinenCloth");
        linen.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");linen.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        linen.name="Marchwarden simulated linen";EditorUtility.SetDirty(linen);data.linenMaterial=linen;
        ConfigureClothCapsules(data,reference);
        data.swordBone = Array.FindIndex(reference.bones, b => b.name == "Bip001 R Hand");
        data.shieldBone = Array.FindIndex(reference.bones, b => b.name == "Bip001 L Forearm");
        int chestIndex=Array.FindIndex(reference.bones,b=>b.name=="Bip001 Spine");
        int headIndex=Array.FindIndex(reference.bones,b=>b.name=="Bip001 Head");
        data.chestFrameRotation=(reference.bones[chestIndex].bind*reference.renderer.inverse).rotation;
        data.headEyeLocal=(reference.bones[headIndex].bind*reference.renderer.inverse).MultiplyPoint3x4(new Vector3(0,1.378f,.03f));
        Matrix4x4 Palm(string side)
        {
            var grip=input.handGrips.First(g=>g.side==side);
            int index=Array.FindIndex(reference.bones,b=>b.name=="Bip001 "+side+" Hand");
            return reference.bones[index].bind*reference.renderer.inverse*
                Matrix4x4.TRS(grip.position,PoseMath.AlongBlade(grip.along,grip.normal),Vector3.one);
        }
        data.leftPalm=Palm("L");data.rightPalm=Palm("R");
        int rightForearm=Array.FindIndex(reference.bones,b=>b.name=="Bip001 R Forearm");
        int leftHand=Array.FindIndex(reference.bones,b=>b.name=="Bip001 L Hand");
        data.rightWristRest=(reference.bones[rightForearm].bind*reference.bones[data.swordBone].bind.inverse).rotation;
        data.leftWristRest=(reference.bones[data.shieldBone].bind*reference.bones[leftHand].bind.inverse).rotation;
        data.drawHook=(reference.bones[data.swordBone].bind*reference.renderer.inverse).MultiplyPoint3x4(input.handGrips.First(g=>g.side=="R").drawContact);
        // The hilt's centre is -0.352 along the sword mesh. Seat that centre
        // inside the authored fingers, rather than mounting it at the wrist.
        data.swordMount=data.rightPalm*Matrix4x4.Translate(new Vector3(.352f,0,0));
        var gripReference = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        // The old demo's shield centroid is not an enarme attachment point for
        // this differently sized shield. Fit its straps to the actual forearm.
        var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/KnightController.controller");
        controller.animationClips.First(c=>c.name.Contains("05_combat_idle")).SampleAnimation(gripReference,0);
        CombatAnimationBuilder.GuardPose(gripReference);
        var gripBones=gripReference.GetComponentInChildren<SkinnedMeshRenderer>().bones;
        var shieldFore=gripBones[data.shieldBone];
        var shieldHand=gripBones.First(b=>b.name=="Bip001 L Hand");
        // The lowered arm points partly forward. Projecting the shield normal
        // against that axis turned its face sideways. Fit a forward-facing guard
        // with enough standoff for the bracer and palm behind the wooden shell.
        var shieldNormal=Vector3.forward;
        var shieldUp=Vector3.up;
        var shieldCenter=Vector3.Lerp(shieldFore.position,shieldHand.position,.55f)+shieldNormal*.170f;
        var shieldPose=Matrix4x4.TRS(shieldCenter,PoseMath.AlongBlade(shieldUp,shieldNormal),Vector3.one);
        data.shieldMount=shieldFore.worldToLocalMatrix*shieldPose;
        data.shieldGripMount=shieldPose.inverse*shieldHand.localToWorldMatrix*data.leftPalm;
        AttachShieldGrip(data.shield,input.parts.First(p=>p.name=="ShieldHandle"),data.shieldGripMount);
        data.nativeWalkSpeed=MeasureGaitSpeed(gripReference,controller.animationClips.First(c=>c.name.Contains("06_combat_walk")),2f);
        data.nativeRunSpeed=MeasureGaitSpeed(gripReference,controller.animationClips.First(c=>c.name.Contains("04_charge")),4.4f);
        UnityEngine.Object.DestroyImmediate(gripReference);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        CreatePrefab(data);
        Debug.Log($"[Marchwarden] Imported body, independent cloth and equipment with {names.Length} PBR materials");
    }

    static void ConfigureClothCapsules(MarchwardenAsset data,Rig reference)
    {
        var specs=new System.Collections.Generic.List<MarchwardenAsset.ClothCapsule>();
        int Index(string name)=>Array.FindIndex(reference.bones,b=>b.name=="Bip001 "+name);
        Vector3 Joint(string name)=>reference.bones[Index(name)].position;
        void Add(string name,string bone,Vector3 start,Vector3 end,float radius)
        {
            int index=Index(bone);Matrix4x4 toBone=reference.bones[index].bind*reference.renderer.inverse;
            specs.Add(new MarchwardenAsset.ClothCapsule{name=name,bone=index,start=toBone.MultiplyPoint3x4(start),
                end=toBone.MultiplyPoint3x4(end),radius=radius});
        }
        foreach(string side in new[]{"L","R"})
        {
            Vector3 hip=Joint(side+" Thigh"),knee=Joint(side+" Calf"),ankle=Joint(side+" Foot");
            // Include the gathered trouser surface, folds and seam thickness,
            // not only the underlying limb centerline.
            Add(side+" upper thigh",side+" Thigh",Vector3.Lerp(hip,knee,.18f),Vector3.Lerp(hip,knee,.52f),.118f);
            Add(side+" lower thigh",side+" Thigh",Vector3.Lerp(hip,knee,.57f),Vector3.Lerp(hip,knee,.87f),.102f);
            Add(side+" upper calf",side+" Calf",Vector3.Lerp(knee,ankle,.10f),Vector3.Lerp(knee,ankle,.46f),.082f);
            Add(side+" lower calf",side+" Calf",Vector3.Lerp(knee,ankle,.50f),Vector3.Lerp(knee,ankle,.90f),.058f);
            Add(side+" heel and instep",side+" Foot",new Vector3(ankle.x,.057f,ankle.z-.055f),new Vector3(ankle.x,.057f,ankle.z+.025f),.060f);
            Add(side+" forefoot and toe",side+" Foot",new Vector3(ankle.x,.050f,ankle.z+.022f),new Vector3(ankle.x,.050f,ankle.z+.111f),.056f);
        }
        Add("pelvis","Pelvis",new Vector3(-.08f,.755f,-.046f),new Vector3(.08f,.755f,-.046f),.090f);
        data.clothCapsules=specs.ToArray();
    }

    // Match the locomotion clock to the distance travelled by a supporting foot
    // in the source cycle. Attack clips have their own clock and stay unchanged.
    static float MeasureGaitSpeed(GameObject go,AnimationClip clip,float fallback)
    {
        var feet=go.GetComponentsInChildren<Transform>().Where(t=>t.name=="Bip001 L Foot"||t.name=="Bip001 R Foot").ToArray();
        const int samples=120;
        var positions=new Vector3[feet.Length,samples+1];
        for(int sample=0;sample<=samples;sample++)
        {
            clip.SampleAnimation(go,sample*clip.length/samples);
            for(int foot=0;foot<feet.Length;foot++)positions[foot,sample]=go.transform.InverseTransformPoint(feet[foot].position);
        }
        var speeds=new System.Collections.Generic.List<float>();
        for(int foot=0;foot<feet.Length;foot++)
        {
            float floor=Enumerable.Range(0,samples+1).Min(i=>positions[foot,i].y);
            for(int sample=1;sample<samples;sample++)
            {
                float speed=-(positions[foot,sample+1].z-positions[foot,sample-1].z)/(2*clip.length/samples);
                if(positions[foot,sample].y<floor+.025f&&speed>.15f)speeds.Add(speed);
            }
        }
        speeds.Sort();
        float native=speeds.Count>=6?speeds[speeds.Count/2]:fallback;
        native=Mathf.Clamp(native,.6f,7f);
        Debug.Log($"[Marchwarden] {clip.name} supporting-foot speed {native:F3} m/s");
        return native;
    }

    static void ConfigureSurfaces()
    {
        foreach (var path in Directory.GetFiles(Destination + "/Textures", "*.png"))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            bool normal = path.EndsWith("_Normal.png"), surface = path.EndsWith("_Surface.png");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal && !surface;
            ti.maxTextureSize = path.Contains("Finish") ? 4096 : 1024;
            ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8;
            ti.wrapMode = path.Contains("Finish") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            // BC7 preserves gradual cloth/skin tones; BC5 avoids blocky normal
            // gradients. Do not let legacy DXT platform overrides survive a rebuild.
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name="Standalone",overridden=true,maxTextureSize=ti.maxTextureSize,
                format=normal?TextureImporterFormat.BC5:TextureImporterFormat.BC7,
                compressionQuality=100,crunchedCompression=false });
            ti.alphaIsTransparency = false;
            ti.SaveAndReimport();
        }
    }

    static void CreatePrefab(MarchwardenAsset data)
    {
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        go.name = "Marchwarden";
        var renderer = go.GetComponentInChildren<SkinnedMeshRenderer>();
        renderer.sharedMesh = data.body; renderer.sharedMaterials = data.materials;
        renderer.quality = SkinQuality.Bone4;
        var animationBounds=data.body.bounds;animationBounds.Expand(.25f);
        renderer.localBounds = animationBounds;
        MarchwardenStature.Ensure(go,data);
        MarchwardenCloth.Prepare(go,renderer,data);
        MarchwardenArmor.Prepare(go,renderer,data,null);
        void Gear(string name, Mesh mesh, int bone, Matrix4x4 mount)
        {
            var prop = new GameObject(name); prop.transform.SetParent(go.transform, false);
            prop.AddComponent<MeshFilter>().sharedMesh = mesh;
            prop.AddComponent<MeshRenderer>().sharedMaterials = data.materials;
            var pose = renderer.bones[bone].localToWorldMatrix * mount;
            prop.transform.SetPositionAndRotation(pose.GetColumn(3), Quaternion.LookRotation(pose.GetColumn(2),pose.GetColumn(1)));
        }
        Gear("Marchwarden Sword",data.sword,data.swordBone,data.swordMount);
        Gear("Marchwarden Shield",data.shield,data.shieldBone,data.shieldMount);
        go.AddComponent<KnightAppearance>();
        PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/Prefabs/Marchwarden.prefab");
        UnityEngine.Object.DestroyImmediate(go);
    }

    [MenuItem("Medieval Warrior/Marchwarden/Export animation rig reference")]
    public static void ExportRig()
    {
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var r = go.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = r.sharedMesh;
        var rig = new Rig {
            renderer = r.transform.localToWorldMatrix,
            bones = r.bones.Select((b,i) => new Bone { name = b.name,
                position = r.transform.TransformPoint(mesh.bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero)),
                bind = mesh.bindposes[i] }).ToArray(),
            vertices = new [] { r.bounds.min, r.bounds.max }
        };
        Directory.CreateDirectory("Tools/ArtPipeline/Marchwarden");
        File.WriteAllText("Tools/ArtPipeline/Marchwarden/rig.json", JsonUtility.ToJson(rig, true));
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("[Marchwarden] Exported animation rig reference");
    }
}
