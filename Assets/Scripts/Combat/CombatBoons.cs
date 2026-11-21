using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// Contact-driven run modifiers. No per-frame searches and no recursive on-hit procs.
public static class CombatBoons
{
    public static readonly Color PhantomColor=new Color(.36f,.7f,1f,.65f);

    static readonly RaycastHit[] sightHits=new RaycastHit[64];
    public static bool ClearLine(Vector3 from,Vector3 to,Transform target=null)
    {
        var delta=to-from;
        int count=Physics.RaycastNonAlloc(from,delta.normalized,sightHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        if(count==sightHits.Length)return false;
        for(int i=0;i<count;i++)
        {
            var hit=sightHits[i];
            if(target!=null&&hit.transform.IsChildOf(target))continue;
            if(hit.collider.GetComponentInParent<EnemyController>()==null&&hit.collider.GetComponentInParent<PlayerHealth>()==null)
                return false;
        }
        return true;
    }
    public static void Parry(PlayerHealth player,Vector3 contact)
    {
        if(Boons.RiposteHeal>0)player.Heal(Boons.RiposteHeal);
        int rank=Boons.Stacks(BoonId.ShockwaveParry);
        if(!Boons.ThunderGuard&&rank==0)return;
        Vector3 center=player.transform.position+Vector3.up;
        FX.Shockwave(center,4.5f);AudioManager.PlayAt("thunder_guard",contact,.8f);
        // Stagger can alter attack ownership; damage can remove enemies from All.
        foreach(var enemy in EnemyController.All.ToArray())
            if(enemy!=null&&!enemy.IsDead&&(enemy.transform.position-player.transform.position).sqrMagnitude<4.5f*4.5f&&
                ClearLine(center,enemy.transform.position+Vector3.up))
                enemy.Stagger(player.transform.position,8f,enemy.isBoss?.4f:.85f);
        if(rank>0)
        {
            var candidates=EnemyController.All.ToArray();var visited=new HashSet<EnemyController>();
            Vector3 origin=center;
            for(int jump=0;jump<Mathf.Min(12,3+rank);jump++)
            {
                EnemyController nearest=null;float best=jump==0?36:20.25f;
                foreach(var enemy in candidates)
                {
                    if(enemy==null||enemy.IsDead||visited.Contains(enemy))continue;
                    float distance=(enemy.transform.position+Vector3.up-origin).sqrMagnitude;
                    if(distance<best&&ClearLine(origin,enemy.transform.position+Vector3.up)){nearest=enemy;best=distance;}
                }
                if(nearest==null)break;
                visited.Add(nearest);Vector3 end=nearest.transform.position+Vector3.up*1.2f;
                HordeVFX.Arc(origin,end,new Color(.45f,.78f,1));
                nearest.TakeDamage((.7f+rank*.45f)*Boons.SwordDamage,Vector3.up,0);
                if(!nearest.IsDead)nearest.Stagger(origin,4,nearest.isBoss?.3f:1.1f);
                origin=end;
            }
        }
    }
    public static float WhirlwindRadius=>3.1f+Mathf.Min(1.4f,Mathf.Sqrt(Boons.Stacks(BoonId.WhirlwindStrike))*.3f);
    public static void Whirlwind(Transform knight)
    {
        int rank=Boons.Stacks(BoonId.WhirlwindStrike);if(rank<=0)return;
        Vector3 center=knight.position+knight.forward*.75f;
        HordeVFX.Vortex(center+Vector3.up*.15f,WhirlwindRadius);AudioManager.PlayAt("phantom_cleave",center,.7f);
        foreach(var enemy in EnemyController.All.ToArray())
        {
            if(enemy==null||enemy.IsDead||(enemy.transform.position-center).sqrMagnitude>WhirlwindRadius*WhirlwindRadius||
               !ClearLine(center+Vector3.up,enemy.transform.position+Vector3.up))continue;
            enemy.PullTowards(center,.48f,Mathf.Min(8,3.5f+rank*.4f));
            enemy.Bleed(4,.5f*rank*Boons.SwordDamage);
        }
    }
    public static void PhantomHit(EnemyController enemy,Vector3 contact)
    {
        if(!Boons.ReapersCleave)return;
        FX.Sparkle(contact,PhantomColor,8);
        if(!enemy.IsDead)enemy.Ignite(3f,.6f,PhantomColor);
    }
    public static void Splinters(Vector3 point,Vector3 direction,EnemyController primary,float damage,bool fire=false)
    {
        if(!Boons.SplinterVolley)return;
        FX.Splinters(point,direction);AudioManager.PlayAt("splinter",point,.7f);
        // Start just outside the impacted surface. Raycasts originating exactly
        // on/inside a wall can otherwise miss that wall and damage through it.
        var origin=point-direction.normalized*.035f;
        foreach(var enemy in EnemyController.All.ToArray())
        {
            if(enemy==null||enemy==primary||enemy.IsDead)continue;
            var contact=enemy.transform.position+Vector3.up;var offset=contact-point;
            if(offset.sqrMagnitude>2.6f*2.6f||!ClearLine(origin,contact))continue;
            // Goes through normal physical shield/armour rules. The primary target
            // isn't hit twice, and shrapnel never recursively triggers more shrapnel.
            bool landed=enemy.ReceiveWeaponHit(Mathf.Min(2f,damage*.55f),offset.normalized,0,contact);
            if(landed&&fire&&!enemy.IsDead)enemy.Ignite(2f,.5f);
        }
    }
}
