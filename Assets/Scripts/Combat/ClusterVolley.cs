using UnityEngine;
using System.Linq;

// Each arrow may create one volley. Bomb damage never calls arrow/on-hit code.
public sealed class ClusterVolley : MonoBehaviour
{
    static int live;
    readonly Vector3[] points=new Vector3[5];
    readonly GameObject[] markers=new GameObject[5];
    float age,damage;
    int count,next;
    bool fire;
    public static void Create(Vector3 contact,Vector3 direction,float arrowDamage,bool burning)
    {
        int rank=Boons.Stacks(BoonId.ExplosiveQuiver);if(rank<=0||live>=24)return;
        var go=new GameObject("Headshot cluster volley");go.transform.position=contact;
        var volley=go.AddComponent<ClusterVolley>();live++;
        volley.count=Mathf.Min(5,3+rank/3);volley.damage=arrowDamage*(.5f+.3f*rank);volley.fire=burning;
        for(int i=0;i<volley.count;i++)
        {
            float angle=i*360f/volley.count+Vector3.SignedAngle(Vector3.forward,direction,Vector3.up);
            Vector3 at=contact+Quaternion.Euler(0,angle,0)*Vector3.forward*(i==0?.3f:1.35f);
            if(!CombatBoons.ClearLine(contact,at))at=contact;
            at=AcidPool.GroundBelow(at);volley.points[i]=at+Vector3.up*.15f;
            volley.markers[i]=HordeVFX.Ring(go.transform,volley.points[i]-contact,.48f,new Color(1,.64f,.22f,.8f),.025f);
        }
        FX.Splinters(contact,direction);AudioManager.PlayAt("splinter",contact,.7f);
    }
    void Update()
    {
        age+=Time.deltaTime;
        if(next<count&&age>=.22f+next*.09f)
        {
            Vector3 point=points[next];if(markers[next]!=null)Destroy(markers[next]);next++;
            FX.Explosion(point,.6f,new Color(1,.55f,.18f,.75f));FX.Splinters(point,Vector3.up);
            AudioManager.PlayAt("explosion",point,.3f);
            foreach(var enemy in EnemyController.All.ToArray())
            {
                if(enemy==null||enemy.IsDead)continue;
                Vector3 to=enemy.transform.position+Vector3.up*.8f;
                if((to-point).sqrMagnitude>2.2f*2.2f||!CombatBoons.ClearLine(point+Vector3.up*.35f,to))continue;
                enemy.TakeDamage(damage,(to-point).normalized,0);
                if(fire&&!enemy.IsDead)enemy.Ignite(2,.7f);
            }
        }
        if(next>=count)Destroy(gameObject);
    }
    void OnDestroy(){live=Mathf.Max(0,live-1);}
}
