using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Conservative volumes cover the whole blade, guard and grip. The shield uses
// its actual bounds, including rim and back straps. Positive clearance means
// these volumes do not overlap; it is stronger than testing only a blade tip.
public static class SwordClearanceReview
{
    public static float Clearance(Matrix4x4 sword,Matrix4x4 shield,Bounds bounds)
    {
        return EquippedWeaponClearance.Measure(sword,shield,bounds);
    }

    [MenuItem("Medieval Warrior/Review sword and shield clearance")]
    public static void Run()
    {
        var data=MarchwardenVisual.Asset;
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MarchwardenBuilder.Source));
        MarchwardenStature.Ensure(go,data);
        var bones=go.GetComponentInChildren<SkinnedMeshRenderer>().bones;
        var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/KnightController.controller");
        var idle=controller.animationClips.First(c=>c.name.Contains("05_combat_idle"));
        var nodes=go.GetComponentsInChildren<Transform>();
        Quaternion[] Pose(AnimationClip clip,float time)
        {
            idle.SampleAnimation(go,0);CombatAnimationBuilder.GuardPose(go);if(clip!=null)clip.SampleAnimation(go,time);
            return nodes.Select(n=>n.localRotation).ToArray();
        }
        var rest=Pose(null,0);
        int gloveBone=Array.FindIndex(bones,b=>b.name=="Bip001 L Hand");
        var vertices=data.body.vertices;var weights=data.body.boneWeights;
        var glove=Enumerable.Range(0,vertices.Length).Where(i=>weights[i].boneIndex0==gloveBone&&weights[i].weight0>.99f).Select(i=>vertices[i]).ToArray();
        float gloveGap=float.MaxValue;
        float swordGripGap=0,shieldGripGap=0;
        float maximumWristBend=0;
        var rightFore=bones.First(b=>b.name=="Bip001 R Forearm");
        void MeasureGrips()
        {
            Vector3 forearmAxis=bones[data.swordBone].position-rightFore.position;
            Vector3 handAxis=bones[data.swordBone].TransformDirection(data.rightPalm.MultiplyVector(Vector3.up));
            maximumWristBend=Mathf.Max(maximumWristBend,Vector3.Angle(forearmAxis,handAxis));
            var sword=bones[data.swordBone].localToWorldMatrix*data.swordMount;
            var shield=bones[data.shieldBone].localToWorldMatrix*data.shieldMount;
            Vector3 palm=bones[data.swordBone].TransformPoint(data.rightPalm.GetColumn(3));
            swordGripGap=Mathf.Max(swordGripGap,Vector3.Distance(palm,sword.MultiplyPoint3x4(new Vector3(-.352f,0,0))));
            Vector3 left=bones[gloveBone].TransformPoint(data.leftPalm.GetColumn(3));
            shieldGripGap=Mathf.Max(shieldGripGap,Vector3.Distance(left,(shield*data.shieldGripMount).GetColumn(3)));
        }
        void Blend(Quaternion[] a,Quaternion[] b,float weight)
        {for(int i=0;i<nodes.Length;i++)nodes[i].localRotation=Quaternion.Slerp(a[i],b[i],weight);}
        float Gap()=>Clearance(bones[data.swordBone].localToWorldMatrix*data.swordMount,
            bones[data.shieldBone].localToWorldMatrix*data.shieldMount,data.shield.bounds);
        var report=new StringBuilder();int failures=0;
        report.AppendLine("Conservative full-weapon capsule / shield bounds clearance, metres. Samples at 180 Hz.");
        // The eye line is measured from the authored head in bind space, then
        // follows its animated bone. Check the entire actual rim, not its pivot.
        int headIndex=Array.FindIndex(bones,b=>b.name=="Bip001 Head");
        var rig=JsonUtility.FromJson<MarchwardenBuilder.Rig>(File.ReadAllText("Tools/ArtPipeline/Marchwarden/rig.json"));
        Vector3 eyeLocal=data.body.bindposes[headIndex].MultiplyPoint3x4(rig.renderer.inverse.MultiplyPoint3x4(new Vector3(0,1.378f,.03f)));
        foreach(bool blocking in new[]{false})
        {
            Pose(null,0);
            var mount=bones[data.shieldBone].localToWorldMatrix*data.shieldMount;
            float rim=data.shield.vertices.Max(v=>mount.MultiplyPoint3x4(v).y);
            float eyes=bones[headIndex].TransformPoint(eyeLocal).y;
            bool below=rim<eyes-.025f;
            if(!below)failures++;
            report.AppendLine($"{(below?"PASS":"FAIL")} {(blocking?"blocking":"neutral")} shield rim below eyes: {eyes-rim:F4} m");
        }
        Pose(null,0);var defense=new ShieldDefensePose(go);
        bool neutralClear=defense.ChestGap>=.008f&&defense.SightlineClear;
        if(!neutralClear)failures++;
        report.AppendLine($"{(neutralClear?"PASS":"FAIL")} neutral chest separation {defense.ChestGap:F4} m; sightline {defense.SightlineClear}");
        float chestGap=float.MaxValue,footDrift=0,maxBlockStep=0;bool sightline=true;string worstBlockJoint="";int worstBlockFrame=0;
        Quaternion[] lastBlock=nodes.Select(n=>n.localRotation).ToArray();
        for(int step=0;step<=60;step++)
        {
            defense.Restore();
            Pose(null,0);defense.Apply(step/60f);
            MeasureGrips();
            chestGap=Mathf.Min(chestGap,defense.ChestGap);footDrift=Mathf.Max(footDrift,defense.FootDrift);
            sightline&=defense.SightlineClear;
            var current=nodes.Select(n=>n.localRotation).ToArray();
            if(lastBlock!=null)for(int i=0;i<current.Length;i++){
                float angle=Quaternion.Angle(lastBlock[i],current[i]);
                if(angle>maxBlockStep){maxBlockStep=angle;worstBlockJoint=nodes[i].name;worstBlockFrame=step;}
            }
            lastBlock=current;
        }
        void DefenseCheck(bool ok,string label){if(!ok)failures++;report.AppendLine($"{(ok?"PASS":"FAIL")} {label}");}
        DefenseCheck(chestGap>=.015f,$"heater block/transition clears chest armor and rear fittings: {chestGap:F4} m");
        DefenseCheck(footDrift<.004f,$"braced stance retains both foot contacts: {footDrift:F4} m");
        DefenseCheck(sightline,"both eyes retain forward line of sight through entire block transition");
        DefenseCheck(maxBlockStep<12,$"block transition joint continuity: {maxBlockStep:F2} degrees per step, {worstBlockJoint} at {worstBlockFrame}/60");
        DefenseCheck(defense.CoversTorso,$"raised heater intercepts a forward strike toward the sternum: {defense.TorsoProbeLocal:F3}");
        DefenseCheck(defense.ShieldTop>=defense.EyeHeight-.06f&&defense.ShieldTop<defense.EyeHeight+.20f,
            $"heater covers upper torso/head flank: rim {defense.ShieldTop:F3}, eyes {defense.EyeHeight:F3}");
        defense.Restore();
        for(int variant=0;variant<SwordMoves.Count;variant++)
        {
            var clip=SwordMoves.Clip(variant);float minimum=float.MaxValue,worst=0;
            Quaternion[] prior=null;float maximumStep=0,stepAt=0;string stepBone="";
            for(int frame=0;frame<=Mathf.CeilToInt(clip.length*180);frame++)
            {
                float time=Mathf.Min(clip.length,frame/180f);
                var pose=Pose(clip,time);
                if(prior!=null)for(int joint=0;joint<pose.Length;joint++)
                {
                    float step=Quaternion.Angle(prior[joint],pose[joint]);
                    if(step>maximumStep){maximumStep=step;stepAt=time;stepBone=nodes[joint].name;}
                }
                prior=pose;
                float weight=Mathf.SmoothStep(0,1,time/SwordMoves.FadeIn)*(1-Mathf.SmoothStep(0,1,(time-clip.length+SwordMoves.RecoveryFade)/SwordMoves.RecoveryFade));
                Blend(rest,pose,weight);
                float gap=Gap();
                MeasureGrips();
                var shieldMatrix=bones[data.shieldBone].localToWorldMatrix*data.shieldMount;
                var gloveToShield=shieldMatrix.inverse*bones[gloveBone].localToWorldMatrix*data.body.bindposes[gloveBone];
                foreach(var vertex in glove)
                {
                    var point=gloveToShield.MultiplyPoint3x4(vertex);
                    // 0.015 is behind the shallowest back of the bowed shell;
                    // back straps are intentionally allowed to wrap the glove.
                    if(Mathf.Abs(point.x)<.35f&&Mathf.Abs(point.y)<.24f)gloveGap=Mathf.Min(gloveGap,.015f-point.z);
                }
                if(gap<minimum){minimum=gap;worst=time;}
            }
            if(minimum<.008f)failures++;
            report.AppendLine($"{(minimum>=.008f?"PASS":"FAIL")} {variant:00} {SwordMoves.All[variant].name}: {minimum:F4} m at {worst:F3} s");
            if(maximumStep>15)failures++;
            report.AppendLine($"{(maximumStep<=15?"PASS":"FAIL")} {variant:00} quaternion continuity: maximum {maximumStep:F2} degrees per 180 Hz sample, {stepBone} at {stepAt:F3}s");
        }
        // Exercise the same cross-fade and retained overlay used
        // by PlayerControlSpeed, at every allowed combo start time.
        for(int previous=0;previous<SwordMoves.Count;previous++)
        {
            int next=(previous+1)%SwordMoves.Count;float minimum=float.MaxValue;
            for(float start=SwordMoves.All[previous].chain;start<=SwordMoves.All[previous].duration+.12f;start+=.02f)
                for(int frame=0;frame<=22;frame++)
                {
                    float time=frame*.005f;
                    var a=Pose(SwordMoves.Clip(previous),Mathf.Min(SwordMoves.All[previous].duration,start+time));
                    var b=Pose(SwordMoves.Clip(next),time);
                    Blend(a,b,Mathf.SmoothStep(0,1,time/SwordMoves.Crossfade));
                    var mixed=nodes.Select(n=>n.localRotation).ToArray();
                    float entryWeight=1-Mathf.SmoothStep(0,1,(start-SwordMoves.All[previous].duration+SwordMoves.RecoveryFade)/SwordMoves.RecoveryFade);
                    Blend(rest,mixed,Mathf.Lerp(entryWeight,1,Mathf.SmoothStep(0,1,time/SwordMoves.FadeIn)));
                    minimum=Mathf.Min(minimum,Gap());
                }
            if(minimum<.008f)failures++;
            report.AppendLine($"{(minimum>=.008f?"PASS":"FAIL")} blend {previous:00}->{next:00}: {minimum:F4} m");
        }
        if(gloveGap<.005f)failures++;
        report.AppendLine($"{(gloveGap>=.005f?"PASS":"FAIL")} glove behind shield shell: {gloveGap:F4} m");
        DefenseCheck(swordGripGap<.002f,$"sword hilt remains inside the right palm through all strikes and defense: {swordGripGap:F4} m");
        DefenseCheck(shieldGripGap<.003f,$"heater handle remains inside the left palm through all strikes and defense: {shieldGripGap:F4} m");
        DefenseCheck(maximumWristBend<60,$"right wrist never folds backwards through strikes and defense: {maximumWristBend:F1} degrees maximum");
        UnityEngine.Object.DestroyImmediate(go);
        Directory.CreateDirectory("Artifacts/Marchwarden");
        File.WriteAllText("Artifacts/Marchwarden/sword-clearance.txt",report.ToString());
        Debug.Log("[SwordClearance] "+report);
        if(failures>0)throw new InvalidOperationException($"{failures} combat clearance or pose-continuity checks failed; inspect sword-clearance.txt");
    }
    public static void BuildAndReview(){MarchwardenBuilder.Build();CombatAnimationBuilder.Build();RenderComparison();Run();}

    public static void RenderComparison()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.48f,.51f,.55f);
        var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/KnightController.controller");
        var idle=controller.animationClips.First(c=>c.name.Contains("05_combat_idle"));
        var original=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MarchwardenBuilder.Source));
        var hero=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Marchwarden.prefab"));
        original.transform.position=Vector3.left*.67f;hero.transform.position=Vector3.right*.67f;
        idle.SampleAnimation(original,0);idle.SampleAnimation(hero,0);
        CombatAnimationBuilder.GuardPose(hero);
        void Mount()
        {
            var data=MarchwardenVisual.Asset;var bones=hero.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            foreach(var item in new[]{(name:"Marchwarden Sword",bone:data.swordBone,mount:data.swordMount),(name:"Marchwarden Shield",bone:data.shieldBone,mount:data.shieldMount)})
            {
                var matrix=bones[item.bone].localToWorldMatrix*item.mount;
                hero.transform.Find(item.name).SetPositionAndRotation(matrix.GetColumn(3),matrix.rotation);
            }
        }
        Mount();
        var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.15f;key.color=new Color(1,.94f,.86f);key.transform.rotation=Quaternion.Euler(35,-30,0);
        var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.color=new Color(.67f,.80f,1);fill.transform.rotation=Quaternion.Euler(20,150,0);
        var camera=new GameObject("Art review").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.07f,.085f);
        camera.transform.position=new Vector3(2.3f,1.7f,6.5f);camera.transform.LookAt(new Vector3(0,.90f,0));camera.orthographic=true;camera.orthographicSize=1.10f;
        void Shot(string name,int width,int height)
        {
            // Camera.Render in edit-mode does not advance GPU skinning after
            // SampleAnimation. Bake the measured pose for an accurate still.
            var skins=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
            var temporary=new System.Collections.Generic.List<GameObject>();
            var meshes=new System.Collections.Generic.List<Mesh>();
            foreach(var skin in skins)
            {
                var baked=new Mesh();skin.BakeMesh(baked);meshes.Add(baked);
                var copy=new GameObject("Baked pose for review");copy.transform.SetParent(skin.transform,false);
                copy.AddComponent<MeshFilter>().sharedMesh=baked;copy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                skin.enabled=false;temporary.Add(copy);
            }
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,4);
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            Directory.CreateDirectory("Artifacts/Marchwarden");File.WriteAllBytes("Artifacts/Marchwarden/"+name+".png",image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);
            foreach(var item in temporary)UnityEngine.Object.DestroyImmediate(item);
            foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
            foreach(var skin in skins)skin.enabled=true;
        }
        Shot("ToonRTS-vs-Marchwarden",1920,1080);
        original.SetActive(false);hero.transform.position=Vector3.zero;idle.SampleAnimation(hero,0);CombatAnimationBuilder.GuardPose(hero);Mount();
        camera.transform.position=new Vector3(1.3f,1.45f,4.5f);camera.transform.LookAt(new Vector3(0,.90f,0));camera.orthographicSize=1.02f;
        Shot("Unity-warrior",1200,1600);
        Vector3 fullPosition=camera.transform.position;Quaternion fullRotation=camera.transform.rotation;
        var helmetSkin=hero.GetComponentInChildren<SkinnedMeshRenderer>();
        var helmetHead=helmetSkin.bones.First(b=>b.name=="Bip001 Head");
        var helmetData=MarchwardenVisual.Asset;
        int helmetBone=Array.IndexOf(helmetSkin.bones,helmetHead);
        var helmetBaked=new Mesh();helmetSkin.BakeMesh(helmetBaked);
        var helmetVertices=helmetBaked.vertices;var helmetWeights=helmetData.body.boneWeights;
        var helmetBounds=new Bounds(helmetHead.position,Vector3.zero);
        for(int i=0;i<helmetVertices.Length;i++)if(helmetWeights[i].boneIndex0==helmetBone&&helmetWeights[i].weight0>.98f)
            helmetBounds.Encapsulate(helmetSkin.transform.TransformPoint(helmetVertices[i]));
        UnityEngine.Object.DestroyImmediate(helmetBaked);
        Vector3 helmetFocus=helmetBounds.center;
        camera.orthographicSize=Mathf.Max(helmetBounds.extents.y,helmetBounds.extents.z)*1.22f;
        camera.transform.position=helmetFocus+new Vector3(.65f,.09f,2);camera.transform.LookAt(helmetFocus);
        Shot("Unity-helmet",1200,1200);
        camera.transform.position=helmetFocus+new Vector3(-1.2f,.08f,1.6f);camera.transform.LookAt(helmetFocus);
        Shot("Unity-helmet-side",1200,1200);
        camera.transform.SetPositionAndRotation(fullPosition,fullRotation);camera.orthographicSize=1.02f;
        // Keep a geometric-normal reference to distinguish mesh/weight defects
        // from tangent-map artefacts when reviewing close-ups.
        var reviewMaterials=hero.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials;
        foreach(var mat in reviewMaterials)mat.DisableKeyword("_NORMALMAP");
        Shot("Unity-warrior-geometric-normals",1200,1600);
        foreach(var mat in reviewMaterials)mat.EnableKeyword("_NORMALMAP");
        var reviewDefense=new ShieldDefensePose(hero);reviewDefense.Apply(1);Mount();
        Shot("Unity-warrior-block",1200,1600);
        reviewDefense.Restore();
        for(int i=0;i<SwordMoves.Count;i++)
        {
            idle.SampleAnimation(hero,0);SwordMoves.Clip(i).SampleAnimation(hero,SwordMoves.All[i].contact);Mount();
            Shot($"strike-clearance-{i:00}",640,800);
        }
    }
}
