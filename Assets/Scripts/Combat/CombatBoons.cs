using System.Linq;
using UnityEngine;

// Contact-driven run modifiers. No per-frame searches and no recursive on-hit procs.
public static class CombatBoons
{
    public static readonly Color PhantomColor=new Color(.36f,.7f,1f,.65f);

    static bool ClearLine(Vector3 from,Vector3 to)
    {
        var delta=to-from;
        foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            if(hit.collider.GetComponentInParent<EnemyController>()==null&&hit.collider.GetComponentInParent<PlayerHealth>()==null)
                return false;
        return true;
    }
    public static void Parry(PlayerHealth player,Vector3 contact)
    {
        if(Boons.RiposteHeal>0)player.Heal(Boons.RiposteHeal);
        if(!Boons.ThunderGuard)return;
        Vector3 center=player.transform.position+Vector3.up;
        FX.Shockwave(center,4.5f);AudioManager.PlayAt("thunder_guard",contact,.8f);
        // Stagger can alter attack ownership; damage can remove enemies from All.
        foreach(var enemy in EnemyController.All.ToArray())
            if(enemy!=null&&!enemy.IsDead&&(enemy.transform.position-player.transform.position).sqrMagnitude<4.5f*4.5f&&
                ClearLine(center,enemy.transform.position+Vector3.up))
                enemy.Stagger(player.transform.position,8f,enemy.isBoss?.4f:.85f);
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
