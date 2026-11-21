using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class HordeTotem : MonoBehaviour
{
    public const float AuraRadius=12f;
    public float Health { get; private set; }=18;
    public float HealthFraction=>Health/18f;
    public bool IsBroken=>Health<=0;
    public bool IsRaised=>age>=3.2f;
    public Vector3 Target=>ground+Vector3.up*1.3f;
    Vector3 ground;
    Transform stone;
    GameObject aura;
    float age,lastDeflect=-99;
    Mesh mesh;
    Collider hull;
    NavMeshObstacle obstacle;

    public static HordeTotem Create(Vector3 position,Transform owner)
    {
        var go=new GameObject("Cursed Obelisk");go.transform.SetParent(owner,false);go.transform.position=position;
        var totem=go.AddComponent<HordeTotem>();totem.ground=position;
        var shaft=new GameObject("Bevelled infected stone");shaft.transform.SetParent(go.transform,false);totem.stone=shaft.transform;
        totem.mesh=BuildStone();shaft.AddComponent<MeshFilter>().sharedMesh=totem.mesh;
        var renderer=shaft.AddComponent<MeshRenderer>();renderer.sharedMaterial=HordeVFX.Stone;
        var properties=new MaterialPropertyBlock();properties.SetFloat("_Curse",1);properties.SetColor("_Color",new Color(.28f,.31f,.34f));renderer.SetPropertyBlock(properties);
        var collider=shaft.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.2f,0);collider.size=new Vector3(.95f,2.4f,.95f);collider.enabled=false;totem.hull=collider;
        totem.obstacle=go.AddComponent<NavMeshObstacle>();totem.obstacle.shape=NavMeshObstacleShape.Box;
        totem.obstacle.center=new Vector3(0,1.2f,0);totem.obstacle.size=new Vector3(1.05f,2.4f,1.05f);
        totem.obstacle.carving=true;totem.obstacle.enabled=false;
        totem.aura=HordeVFX.Ring(go.transform,Vector3.up*.055f,AuraRadius,new Color(.62f,.18f,.9f,.5f),.035f,true);
        // Carved glyphs follow the bevelled faces. Four faces are readable from any approach.
        for(int face=0;face<4;face++)
        {
            var rotation=Quaternion.Euler(0,face*90,0);
            for(int rune=0;rune<3;rune++)
            {
                float y=.65f+rune*.54f;
                var points=new[]{new Vector3(-.19f,y+.16f,.473f),new Vector3(0,y,.48f),new Vector3(.19f,y+.16f,.473f),
                    new Vector3(0,y-.16f,.48f),new Vector3(-.19f,y+.16f,.473f)};
                for(int i=0;i<points.Length;i++)points[i]=rotation*points[i];
                HordeVFX.Line(shaft.transform,points,new Color(.75f,.24f,1,.9f),.022f);
            }
        }
        shaft.transform.localPosition=Vector3.down*3.1f;
        AudioManager.PlayAt("emerge",position,.9f);FX.Dust(position,1.6f,28);
        return totem;
    }
    static Mesh BuildStone()
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
        // Chamfered square section, a stepped footing and a tapered broken crown.
        Vector2[] section={new Vector2(-.78f,-1),new Vector2(.78f,-1),new Vector2(1,-.78f),new Vector2(1,.78f),new Vector2(.78f,1),new Vector2(-.78f,1),new Vector2(-1,.78f),new Vector2(-1,-.78f)};
        float[] heights={0,.08f,.18f,.27f,.34f,2.35f,2.43f,2.8f},radii={.7f,.78f,.78f,.62f,.48f,.45f,.39f,.04f};
        void Tri(Vector3 a,Vector3 b,Vector3 c)
        {
            int index=vertices.Count;
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),new Vector3((a+b+c).x,0,(a+b+c).z))<0){var temp=b;b=c;c=temp;}
            Vector3 normal=Vector3.Cross(b-a,c-a);
            foreach(var v in new[]{a,b,c}){vertices.Add(v);uv.Add(Mathf.Abs(normal.x)>Mathf.Abs(normal.z)?new Vector2(v.z,v.y):new Vector2(v.x,v.y));}
            triangles.Add(index);triangles.Add(index+1);triangles.Add(index+2);
        }
        Vector3 P(int ring,int side)=>new Vector3(section[side%8].x*radii[ring],heights[ring],section[side%8].y*radii[ring]);
        for(int ring=0;ring<heights.Length-1;ring++)for(int s=0;s<8;s++)
        { Tri(P(ring,s),P(ring,s+1),P(ring+1,s+1));Tri(P(ring,s),P(ring+1,s+1),P(ring+1,s)); }
        for(int s=0;s<8;s++)Tri(P(7,s),P(7,s+1),new Vector3(0,2.83f,0));
        var mesh=new Mesh{name="Chamfered ritual obelisk"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
    public bool Protects(Vector3 position)=>(isActiveAndEnabled&&!IsBroken&&IsRaised&&(position-ground).sqrMagnitude<=AuraRadius*AuraRadius);
    public void Deflect(Vector3 contact)
    {
        if(Time.time-lastDeflect<.16f)return;lastDeflect=Time.time;
        HordeVFX.Arc(Target,contact,new Color(.72f,.26f,1));
    }
    public bool ReceiveHit(float damage,Vector3 point)
    {
        if(IsBroken||!IsRaised||damage<=0)return false;
        Health=Mathf.Max(0,Health-damage);FX.Sparks(point,point-Target,1.1f);
        AudioManager.PlayAt("shield_block",point,.7f);
        if(IsBroken)
        {
            hull.enabled=false;obstacle.enabled=false;aura.SetActive(false);
            FX.Explosion(Target,1.6f,new Color(.7f,.26f,1));FX.Dust(ground,1.8f,32);
            AudioManager.PlayAt("explosion",Target,.7f);FX.Shake(.35f);
            GameHUD.Banner("THE CURSE IS BROKEN","The dead can fall again",2.7f);
        }
        return true;
    }
    void Update()
    {
        age+=Time.deltaTime;
        if(IsBroken){stone.localPosition=Vector3.MoveTowards(stone.localPosition,Vector3.down*3.2f,Time.deltaTime*2.5f);return;}
        stone.localPosition=Vector3.down*3.1f*(1-Mathf.SmoothStep(0,1,age/3.2f));
        hull.enabled=IsRaised;obstacle.enabled=IsRaised;
        aura.transform.localScale=Vector3.one*Mathf.SmoothStep(0,1,age/3.2f);
    }
    void OnDestroy(){if(mesh!=null)Destroy(mesh);}
}
