using UnityEngine;
using UnityEngine.Rendering;

// Shared materials, bounded particles and timed ribbons. No per-frame mesh creation.
public static class HordeVFX
{
    static Material ribbon,gas,stone;
    public static Material Stone=>stone!=null?stone:stone=Resources.Load<Material>("Horde/Stone");
    static Material Ribbon=>ribbon!=null?ribbon:ribbon=new Material(Resources.Load<Shader>("Horde/Ribbon")){name="Horde energy"};
    public static LineRenderer Line(Transform parent,Vector3[] points,Color color,float width)
    {
        var go=new GameObject("Ritual light");go.transform.SetParent(parent,false);
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=Ribbon;line.useWorldSpace=false;
        line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=width;
        line.startColor=line.endColor=color;line.numCapVertices=3;line.numCornerVertices=3;
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
    }
    public static GameObject Ring(Transform parent,Vector3 center,float radius,Color color,float width,bool groundFollowing=false)
    {
        var points=new Vector3[129];
        for(int i=0;i<points.Length;i++)
        {
            float a=i*Mathf.PI*2/(points.Length-1);points[i]=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
            if(groundFollowing&&parent!=null)points[i]=parent.InverseTransformPoint(AcidPool.GroundBelow(parent.TransformPoint(points[i]))+Vector3.up*.05f);
        }
        return Line(parent,points,color,width).gameObject;
    }
    public static void Arc(Vector3 from,Vector3 to,Color color)
    {
        var points=new Vector3[15];Vector3 axis=(to-from).normalized;
        Vector3 across=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;
        for(int i=0;i<points.Length;i++)
        {
            float t=i/(float)(points.Length-1),wave=Mathf.Sin(t*Mathf.PI);
            points[i]=Vector3.Lerp(from,to,t)+(across*Random.Range(-.22f,.22f)+Vector3.up*Random.Range(-.14f,.14f))*wave;
        }
        var line=Line(null,points,color,.045f);line.gameObject.AddComponent<HordeRibbonLife>().Init(.23f);
    }
    public static void Vortex(Vector3 center,float radius)
    {
        var points=new Vector3[110];
        for(int i=0;i<points.Length;i++)
        {float t=i/(float)(points.Length-1),a=t*Mathf.PI*6;points[i]=center+new Vector3(Mathf.Cos(a)*radius*(1-t),t*.9f,Mathf.Sin(a)*radius*(1-t));}
        var line=Line(null,points,new Color(.68f,.2f,.27f,.75f),.055f);line.gameObject.AddComponent<HordeRibbonLife>().Init(.45f);
        FX.Dust(center,1.2f,20,new Color(.38f,.14f,.16f,.5f));
    }
    public static GameObject Gas(HordeDirector.Lane lane,Transform parent)
    {
        var go=new GameObject("Low alley gas");go.transform.SetParent(parent,false);
        go.transform.position=(lane.start+lane.end)*.5f+Vector3.up*.4f;
        Vector3 delta=lane.end-lane.start;
        go.transform.rotation=Quaternion.LookRotation(delta.sqrMagnitude>.01f?delta:Vector3.forward);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=true;main.playOnAwake=false;main.startLifetime=new ParticleSystem.MinMaxCurve(2.2f,3.6f);
        main.startSpeed=.055f;main.startSize=new ParticleSystem.MinMaxCurve(1.2f,2.2f);main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
        main.startColor=new Color(.34f,.52f,.13f,.28f);main.maxParticles=55;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(lane.width*.7f,.45f,Mathf.Max(1,delta.magnitude));
        var emission=ps.emission;emission.rateOverTime=10;
        var color=ps.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.55f,.75f,.3f),1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.3f),new GradientAlphaKey(.65f,.65f),new GradientAlphaKey(0,1)});color.color=gradient;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();
        if(gas==null)gas=new Material(Resources.Load<Shader>("Horde/Gas")){name="Layered alley mist"};
        renderer.sharedMaterial=gas;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        ps.Play();return go;
    }
}

public sealed class HordeRibbonLife : MonoBehaviour
{
    float age,life;LineRenderer ribbon;Color color;
    public void Init(float duration){life=duration;ribbon=GetComponent<LineRenderer>();color=ribbon.startColor;}
    void Update(){age+=Time.deltaTime;if(age>=life){Destroy(gameObject);return;}var c=color;c.a*=1-age/life;ribbon.startColor=ribbon.endColor=c;}
}
