using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RevenantReview
{
    [MenuItem("Medieval Warrior/Revenants/Review roster")]
    public static void Run()
    {
        const string output="Artifacts/Revenants/Review";Directory.CreateDirectory(output);
        var report=new StringBuilder();int failures=0;
        void Check(bool ok,string what){report.AppendLine((ok?"PASS ":"FAIL ")+what);if(!ok)failures++;}
        var data=AssetDatabase.LoadAssetAtPath<RevenantAsset>("Assets/Resources/Revenants/Roster.asset");
        Check(data!=null&&data.forms.Length==8,"Eight Blender archetypes imported");
        var previous=SceneManager.GetActiveScene();bool replace=string.IsNullOrEmpty(previous.path);
        float shadowDistance=QualitySettings.shadowDistance;int cascades=QualitySettings.shadowCascades;var resolution=QualitySettings.shadowResolution;
        if(replace&&previous.isDirty)throw new InvalidOperationException("Save the untitled scene before opening the roster review");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,replace?NewSceneMode.Single:NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            QualitySettings.shadowDistance=25;QualitySettings.shadowCascades=2;QualitySettings.shadowResolution=UnityEngine.ShadowResolution.VeryHigh;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.42f,.47f,.53f);RenderSettings.ambientEquatorColor=new Color(.26f,.28f,.30f);RenderSettings.ambientGroundColor=new Color(.16f,.15f,.14f);
            RenderSettings.fog=false;
            void Light(string n,Vector3 angles,Color color,float intensity)
            {
                var go=new GameObject(n);go.transform.eulerAngles=angles;var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.shadows=LightShadows.Soft;light.shadowBias=.03f;light.shadowNormalBias=.15f;
            }
            Light("Large warm key",new Vector3(35,-35,0),new Color(1,.88f,.73f),1.15f);
            Light("Cool rim",new Vector3(45,150,0),new Color(.55f,.72f,1),.8f);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.localScale=Vector3.one*3;
            var floor=new Material(Shader.Find("Standard"));floor.color=new Color(.13f,.14f,.15f);floor.SetFloat("_Glossiness",.13f);ground.GetComponent<Renderer>().sharedMaterial=floor;
            var camera=new GameObject("Roster camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.075f,.085f);camera.nearClipPlane=.05f;camera.farClipPlane=100;
            void Capture(string file,Vector3 position,Vector3 focus,float ortho,int width=900,int height=1100)
            {
                camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position));camera.orthographic=true;camera.orthographicSize=ortho;
                // Camera.Render in edit mode does not reliably advance GPU skinning.
                // Bake the exact sampled pose for each still, then restore its SMR.
                var baked=new List<(SkinnedMeshRenderer skin,GameObject go,Mesh mesh)>();
                foreach(var skin in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(s=>s.enabled&&s.gameObject.scene==scene))
                {
                    var mesh=UnityEngine.Object.Instantiate(skin.sharedMesh);
                    var v=mesh.vertices;var n=mesh.normals;var weights=mesh.boneWeights;
                    var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();
                    for(int i=0;i<v.Length;i++)
                    {
                        var w=weights[i];Vector3 p=v[i],normal=n[i];
                        v[i]=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                        n[i]=(matrices[w.boneIndex0].MultiplyVector(normal)*w.weight0+matrices[w.boneIndex1].MultiplyVector(normal)*w.weight1+matrices[w.boneIndex2].MultiplyVector(normal)*w.weight2+matrices[w.boneIndex3].MultiplyVector(normal)*w.weight3).normalized;
                    }
                    mesh.vertices=v;mesh.normals=n;mesh.RecalculateBounds();mesh.RecalculateTangents();
                    var go=new GameObject("Review pose");
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;
                    var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
                    skin.enabled=false;baked.Add((skin,go,mesh));
                }
                var rt=new RenderTexture(width,height,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
                var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(output+"/"+file+".png",tex.EncodeToPNG());
                RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
                foreach(var b in baked){b.skin.enabled=true;UnityEngine.Object.DestroyImmediate(b.go);UnityEngine.Object.DestroyImmediate(b.mesh);}
            }
            var actors=new List<GameObject>();
            foreach(var form in data.forms)
            {
                UnityEngine.Random.InitState(17+(int)form.type);
                var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RevenantBuilder.Source));actor.name=form.type.ToString();actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);actor.transform.localScale=Vector3.one;
                var visual=RevenantVisual.Install(actor,form.type);var animator=actor.GetComponent<Animator>();animator.enabled=false;
                var mesh=form.mesh;
                Check(mesh.vertexCount>10000&&mesh.triangles.Length/3<65000,form.type+": continuous anatomy within crowd triangle budget");
                Check(mesh.subMeshCount==1&&data.skinSurfaces.Contains(visual.Body.sharedMaterial),form.type+": one shared matte body draw");
                Check(form.hair!=null&&form.hair.Length==3&&form.hair.All(h=>h!=null&&h.vertexCount>200),form.type+": three fitted hair/beard options");
                Check(data.skinSurfaces.Length==4&&data.skinSurfaces.All(m=>m.GetTexture("_SkinTex")!=null&&m.GetTexture("_SkinTex").width==2048),form.type+": four detailed anatomical UV skin maps");
                Check(mesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.001f),form.type+": normalized four-influence skin");
                Check(form.lod1!=null&&form.lod2!=null&&form.lod1.triangles.Length<mesh.triangles.Length*.55f&&form.lod2.triangles.Length<mesh.triangles.Length*.25f,form.type+": two reduced distance meshes preserve crowd performance");
                Check(actor.GetComponentsInChildren<Renderer>().Count(r=>r.enabled)<=2,form.type+": legacy zombie surfaces hidden, optional fitted hair");
                Check(form.attacks.Length==2&&form.controller!=null,form.type+": two baked combat clips and controller");
                Check(form.attacks.All(c=>Mathf.Abs(c.length-(ZombieTypes.Get(form.type).windup+.55f))<.02f),form.type+": clip contact/recovery timing matches brain");
                var idle=form.controller.animationClips.First(c=>c.name.EndsWith("_Guard")||c.name=="Z_Idle");idle.SampleAnimation(actor,0);
                Capture(form.type+"-front",new Vector3(2.1f,1.55f,4.5f),new Vector3(0,.91f,0),1.11f);
                Capture(form.type+"-side",new Vector3(4.5f,1.45f,.05f),new Vector3(0,.91f,0),1.11f);
                Capture(form.type+"-back",new Vector3(-1.6f,1.5f,-4.5f),new Vector3(0,.91f,0),1.11f);
                Capture(form.type+"-head",new Vector3(.37f,1.72f,1.25f),new Vector3(0,1.63f,.02f),.24f,800,800);
                if(form.type==ZombieType.Walker)
                {
                    foreach(var oldHair in actor.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Matted hair and beard"))oldHair.enabled=false;
                    var hairObject=new GameObject("Hair option review");hairObject.transform.SetParent(visual.Body.bones.First(b=>b.name=="Base HumanHead"),false);
                    var filter=hairObject.AddComponent<MeshFilter>();hairObject.AddComponent<MeshRenderer>().sharedMaterial=data.hairSurface;
                    for(int option=0;option<3;option++)
                    {
                        filter.sharedMesh=form.hair[option];Capture("Walker-hair-"+option,new Vector3(.37f,1.78f,1.25f),new Vector3(0,1.68f,.02f),.25f,800,800);
                        Capture("Walker-hair-"+option+"-side",new Vector3(1.25f,1.76f,.08f),new Vector3(0,1.67f,.02f),.25f,800,800);
                    }
                    Check(form.hair.All(h=>h.uv2.Any(uv=>uv.x==9)&&h.uv2.Any(uv=>uv.x==8)),"Hair meshes retain separate strand and scalp surface coordinates");
                    UnityEngine.Object.DestroyImmediate(hairObject);
                }
                var hand=visual.SwordHand.position;
                Capture(form.type+"-hand",hand+new Vector3(.4f,.12f,.65f),hand+new Vector3(0,-.015f,.025f),.22f,800,800);
                foreach(bool left in new[]{false,true})
                {
                    var wrist=left?visual.ShieldHand:visual.SwordHand;var frame=left?form.left:form.right;
                    string side=left?"L":"R";var index=visual.Body.bones.First(b=>b.name=="Base Human"+side+"ArmDigit21");var pinky=visual.Body.bones.First(b=>b.name=="Base Human"+side+"ArmDigit51");
                    Check(Vector3.Dot(wrist.TransformDirection(frame.along).normalized,(index.position-pinky.position).normalized)>.94f,
                        form.type+": "+side+" thumb-side axis agrees with the actual finger bones");
                    if(form.type==ZombieType.Walker)
                    {
                        Vector3 normal=wrist.TransformDirection(frame.normal).normalized;Vector3 center=wrist.TransformPoint(frame.position);
                        Capture("Walker-"+side+"-palm",center+normal*(left?.55f:-.55f)+Vector3.up*.08f,center,.16f,800,800);
                    }
                }
                var walk=form.controller.animationClips.First(c=>c.name.EndsWith("_Walk"));
                float worstFloor=0;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var sole=Enumerable.Range(0,vertices.Length).Where(i=>vertices[i].y<.11f&&(data.bones[weights[i].boneIndex0].Contains("Foot")||data.bones[weights[i].boneIndex0].EndsWith("Digit11"))).ToArray();
                for(int frame=0;frame<16;frame++)
                {
                    walk.SampleAnimation(actor,walk.length*frame/16);
                    var matrices=visual.Body.bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();
                    float floorY=float.PositiveInfinity;
                    foreach(int i in sole)
                    {
                        var p=vertices[i];var w=weights[i];
                        var q=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                        floorY=Mathf.Min(floorY,q.y);
                    }
                    worstFloor=Mathf.Max(worstFloor,Mathf.Abs(floorY));
                    if(frame%4==0&&(form.type==ZombieType.Walker||form.type==ZombieType.Swordsman))
                        Capture(form.type+"-walk-"+frame,new Vector3(2.1f,1.55f,4.5f),new Vector3(0,.91f,0),1.11f);
                }
                Check(worstFloor<.015f,form.type+": walking soles remain on the authored floor (max "+worstFloor.ToString("F4")+"m)");
                if(form.type==ZombieType.Walker||form.type==ZombieType.Swordsman)
                {
                    var death=form.controller.animationClips.First(c=>c.name=="Revenant_DeathBack");
                    for(int frame=1;frame<=3;frame++)
                    {
                        death.SampleAnimation(actor,death.length*frame/3);
                        Capture(form.type+"-death-"+frame,new Vector3(2.6f,2.1f,4.5f),new Vector3(0,.65f,-.3f),1.3f);
                    }
                }
                for(int style=0;style<2;style++)
                {
                    form.attacks[style].SampleAnimation(actor,ZombieTypes.Get(form.type).windup);
                    var baked=new Mesh();visual.Body.BakeMesh(baked);
                    Check(baked.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z))&&baked.bounds.size.magnitude<5,form.type+": finite posed mesh, strike "+style);
                    if(RevenantVisual.Armed(form.type))
                    {
                        float scale=ZombieTypes.Get(form.type).scale;
                        Vector3 target=new Vector3(0,1.1f/scale,ZombieTypes.Get(form.type).attackRange*.9f/scale);float miss=float.PositiveInfinity;
                        for(int sample=0;sample<=8;sample++)
                        {
                            form.blades[style].Sample(ZombieTypes.Get(form.type).windup+.015f*sample,out var a,out var b);var d=b-a;
                            float t=Mathf.Clamp01(Vector3.Dot(target-a,d)/d.sqrMagnitude);miss=Mathf.Min(miss,Vector3.Distance(a+d*t,target));
                        }
                        Check(miss*scale<.64f,form.type+": blade passes through melee contact corridor at gameplay scale, strike "+style+" ("+(miss*scale).ToString("F3")+"m)");
                    }
                    UnityEngine.Object.DestroyImmediate(baked);
                    Capture(form.type+"-strike-"+style,new Vector3(2.4f,1.75f,4.5f),new Vector3(0,1,.18f),1.2f);
                }
                idle.SampleAnimation(actor,0);
                if(form.type==ZombieType.Walker)
                {
                    var original=new MaterialPropertyBlock();visual.Body.GetPropertyBlock(original);
                    var forearm=visual.Body.bones.First(b=>b.name=="Base HumanLArmForearm");
                    Vector3 contact=Vector3.Lerp(forearm.position,visual.ShieldHand.position,.5f)+Vector3.forward*.07f;
                    visual.Injuries.AddHit(contact,Vector3.back);Vector3 focus=visual.Injuries.LastBloodPoint;
                    Capture("Walker-fresh-blood",focus+new Vector3(-.25f,.12f,.6f),focus,.18f,800,800);
                    visual.Body.SetPropertyBlock(original);
                }
                actor.SetActive(false);actors.Add(actor);
            }
            for(int i=0;i<actors.Count;i++)
            {
                actors[i].SetActive(true);actors[i].transform.position=new Vector3((i-3.5f)*1.5f,0,0);actors[i].transform.localScale=Vector3.one*ZombieTypes.Get((ZombieType)i).scale;
            }
            Capture("roster",new Vector3(0,3.2f,16),new Vector3(0,1.8f,0),2.35f,2400,900);
            UnityEngine.Object.DestroyImmediate(floor);
        }
        finally
        {
            QualitySettings.shadowDistance=shadowDistance;QualitySettings.shadowCascades=cascades;QualitySettings.shadowResolution=resolution;
            if(replace)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        report.AppendLine("Failures: "+failures);File.WriteAllText(output+"/report.txt",report.ToString());Debug.Log("[Revenants] Review complete: "+failures+" failures");
        if(failures>0)throw new InvalidOperationException("Revenant authoring review failed; see "+output+"/report.txt");
    }
}
