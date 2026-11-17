using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class BuildingBuilder
{
    static int bakedGroundProbes,bakedGroundTriangles;
    static Texture2D SaveEnvironmentTexture(string name,Texture2D image,bool mipmaps)
    {
        string path=Output+"/Textures/"+name+".png";File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture=false;importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=mipmaps;importer.wrapMode=TextureWrapMode.Clamp;
        importer.maxTextureSize=2048;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=name=="GroundHeight"?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void BakeTerrainContact(Scene game,Terrain terrain)
    {
        var origin=terrain.transform.position;var size=terrain.terrainData.size;int resolution=terrain.terrainData.heightmapResolution;
        var heights=terrain.terrainData.GetHeights(0,0,resolution,resolution);var pixels=new Color[resolution*resolution];
        for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
        {
            float value=Mathf.Min(heights[z,x],.999999f),low=Mathf.Repeat(value*255,1);
            pixels[z*resolution+x]=new Color(value-low/255,low,0,1);
        }
        var heightImage=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false,true);heightImage.SetPixels(pixels);heightImage.Apply();
        var heightTexture=SaveEnvironmentTexture("GroundHeight",heightImage,false);
        var world=SceneManager.GetSceneByPath(Scenes[1]);
        var root=world.GetRootGameObjects().FirstOrDefault(g=>g.name=="Environment Surface Lighting");
        if(root==null){root=new GameObject("Environment Surface Lighting");SceneManager.MoveGameObjectToScene(root,world);}
        var binding=root.GetComponent<EnvironmentSurfaceLighting>();if(binding==null)binding=root.AddComponent<EnvironmentSurfaceLighting>();binding.groundHeight=heightTexture;binding.terrainOrigin=origin;binding.terrainSize=size;
        binding.enabled=false;binding.enabled=true;EditorUtility.SetDirty(binding);

        // Bake only near architecture, using actual colliders rather than rectangular fake shadows.
        var candidates=new HashSet<GameObject>();var crate=EnvironmentArtAsset.Asset.supplyCrate;
        for(int s=0;s<SceneManager.sceneCount;s++)foreach(var go in SceneManager.GetSceneAt(s).GetRootGameObjects())foreach(var filter in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null)continue;string path=AssetDatabase.GetAssetPath(filter.sharedMesh);
            if(!path.StartsWith(Output+"/Meshes/")&&filter.sharedMesh!=crate)continue;
            candidates.Add(PrefabUtility.GetNearestPrefabInstanceRoot(filter.gameObject)??filter.gameObject);
        }
        const int n=2048;var ao=new float[n*n];var visited=new bool[n*n];Array.Fill(ao,1);
        var directions=new[]{new Vector3(1,.65f,0).normalized,new Vector3(-1,.65f,0).normalized,new Vector3(0,.65f,1).normalized,new Vector3(0,.65f,-1).normalized,
            new Vector3(1,.75f,1).normalized,new Vector3(-1,.75f,1).normalized,new Vector3(1,.75f,-1).normalized,new Vector3(-1,.75f,-1).normalized};
        var player=game.GetRootGameObjects().FirstOrDefault(g=>g.CompareTag("Player"));bool wasActive=player!=null&&player.activeSelf;if(player!=null)player.SetActive(false);
        Physics.SyncTransforms();int probes=0;
        try
        {
            foreach(var building in candidates)
            {
                var filters=building.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null).ToArray();if(filters.Length==0)continue;
                var bounds=new Bounds(building.transform.TransformPoint(BoundsOf(building).center),Vector3.zero);
                foreach(var renderer in building.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(renderer.bounds);
                float baseHeight=terrain.terrainData.GetInterpolatedHeight(Mathf.Clamp01((bounds.center.x-origin.x)/size.x),Mathf.Clamp01((bounds.center.z-origin.z)/size.z))+origin.y;
                if(bounds.min.y-baseHeight>1.1f)continue; // raised bridges cast light shadows, not ground contact AO
                int x0=Mathf.Clamp(Mathf.FloorToInt((bounds.min.x-origin.x-1.8f)/size.x*n),0,n-1),x1=Mathf.Clamp(Mathf.CeilToInt((bounds.max.x-origin.x+1.8f)/size.x*n),0,n-1);
                int z0=Mathf.Clamp(Mathf.FloorToInt((bounds.min.z-origin.z-1.8f)/size.z*n),0,n-1),z1=Mathf.Clamp(Mathf.CeilToInt((bounds.max.z-origin.z+1.8f)/size.z*n),0,n-1);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    int index=z*n+x;if(visited[index])continue;visited[index]=true;probes++;
                    float u=(x+.5f)/n,v=(z+.5f)/n;var point=new Vector3(origin.x+u*size.x,origin.y+terrain.terrainData.GetInterpolatedHeight(u,v)+.045f,origin.z+v*size.z);
                    float occlusion=0;
                    foreach(var direction in directions)if(Physics.Raycast(point,direction,out var hit,1.6f,~0,QueryTriggerInteraction.Ignore))occlusion+=1-hit.distance/1.6f;
                    ao[index]=1-occlusion/directions.Length*.42f;
                }
            }
        }
        finally{if(player!=null)player.SetActive(wasActive);}
        // A small spatial filter yields a soft contact penumbra without a rectangular footprint.
        var softened=(float[])ao.Clone();
        for(int z=1;z<n-1;z++)for(int x=1;x<n-1;x++)
        {
            int i=z*n+x;if(!visited[i])continue;softened[i]=(ao[i]*4+ao[i-1]+ao[i+1]+ao[i-n]+ao[i+n])/8;
        }
        var colours=new Color32[n*n];for(int i=0;i<colours.Length;i++){byte value=(byte)Mathf.RoundToInt(softened[i]*255);colours[i]=new Color32(value,value,value,255);}
        var contactImage=new Texture2D(n,n,TextureFormat.RGBA32,false,true);contactImage.SetPixels32(colours);contactImage.Apply();
        var contactTexture=SaveEnvironmentTexture("GroundContactAO",contactImage,true);

        // Emit only terrain cells containing contact shading. No collider, no navigation source.
        int cells=Mathf.Min(1024,resolution-1);var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int z=0;z<cells;z++)for(int x=0;x<cells;x++)
        {
            int ax=x*n/cells,az=z*n/cells;float darkest=1;
            for(int dz=0;dz<=n/cells;dz++)for(int dx=0;dx<=n/cells;dx++)darkest=Mathf.Min(darkest,softened[Mathf.Min(n-1,az+dz)*n+Mathf.Min(n-1,ax+dx)]);
            if(darkest>.996f)continue;
            int first=vertices.Count;
            foreach(var corner in new[]{new Vector2(x,z),new Vector2(x,z+1),new Vector2(x+1,z+1),new Vector2(x+1,z)})
            {
                float u=corner.x/cells,v=corner.y/cells;vertices.Add(new Vector3(origin.x+u*size.x,origin.y+terrain.terrainData.GetInterpolatedHeight(u,v)+.018f,origin.z+v*size.z));uv.Add(new Vector2(u,v));
            }
            triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
        }
        string meshPath=Output+"/GroundContactMesh.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,meshPath);}else mesh.Clear();
        mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        string materialPath=Output+"/GroundContact.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Shader.Find("Medieval Warrior/Baked Ground Contact"));AssetDatabase.CreateAsset(material,materialPath);}
        material.SetTexture("_MainTex",contactTexture);EditorUtility.SetDirty(material);
        var decal=root.transform.Find("Baked ground contact");if(decal==null){decal=new GameObject("Baked ground contact").transform;decal.SetParent(root.transform,false);}
        decal.gameObject.layer=2;var mf=decal.GetComponent<MeshFilter>();if(mf==null)mf=decal.gameObject.AddComponent<MeshFilter>();mf.sharedMesh=mesh;
        var mr=decal.GetComponent<MeshRenderer>();if(mr==null)mr=decal.gameObject.AddComponent<MeshRenderer>();mr.sharedMaterial=material;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
        if(decal.GetComponent<Collider>()!=null)throw new Exception("Ground contact bake must never add a collider");
        Debug.Log("[Environment finish] Baked ground contact: "+probes+" terrain probes, "+triangles.Count/3+" sparse triangles, no collision/navigation geometry");
        bakedGroundProbes=probes;bakedGroundTriangles=triangles.Count/3;
    }
}
