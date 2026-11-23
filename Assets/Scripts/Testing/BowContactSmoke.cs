#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public partial class SmokeTestDriver
{
    IEnumerator ReviewBowContact(PlayerControlSpeed player)
    {
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        var cameraField = typeof(PlayerControlSpeed).GetField("cameraTransform", fields);
        var rigField = typeof(PlayerControlSpeed).GetField("rig", fields);
        var release = typeof(PlayerControlSpeed).GetMethod("Fire", fields);
        var oldCamera = cameraField.GetValue(player);
        var oldRig = rigField.GetValue(player);
        var body = player.GetComponent<Rigidbody>();
        var position = body.position;
        var rotation = body.rotation;
        var velocity = body.linearVelocity;
        bool wasKinematic = body.isKinematic;
        int oldArrows = player.Arrows;
        var view = new GameObject("Close bow test view");
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject shot = null;
        try
        {
            body.isKinematic = true;
            body.position = player.transform.position = position + Vector3.up * 35f;
            player.transform.rotation = Quaternion.identity;
            var origin = player.transform.position + Vector3.up * 1.4f;
            view.transform.SetPositionAndRotation(origin + new Vector3(.22f, 0, -3f), Quaternion.identity);
            cameraField.SetValue(player, view.transform);
            rigField.SetValue(player, null);
            target.transform.position = origin + new Vector3(.22f, 0, .36f);
            target.transform.localScale = new Vector3(.10f, .24f, .10f);
            wall.SetActive(false);
            Physics.SyncTransforms();

            var prior = FindObjectsByType<Arrow>(FindObjectsSortMode.None);
            player.AddArrows(1);
            player.DebugHoldDraw(1f);
            release.Invoke(player, null);
            player.AddArrows(player.maxArrows);
            var close = FindObjectsByType<Arrow>(FindObjectsSortMode.None).FirstOrDefault(a => !prior.Contains(a));
            if (close != null) shot = close.gameObject;
            yield return Wait(.13f);
            Check(shot != null && shot.GetComponent<Pickup>() != null,
                "a bow release hits the point-blank target under an offset reticle");
            if (shot != null) Destroy(shot);

            // From the lens the target is clear, but the initial shaft crosses cover nearer the muzzle.
            wall.transform.position = origin + new Vector3(.055f, 0, .10f);
            wall.transform.localScale = new Vector3(.14f, .4f, .045f);
            wall.SetActive(true);
            Physics.SyncTransforms();
            Vector3 direction = (target.transform.position - origin).normalized;
            var covered = Arrow.FireBow(origin, direction, 1f, player.transform);
            shot = covered.gameObject;
            yield return Wait(.13f);
            float lodgedTipDepth = shot != null
                ? (shot.transform.position + shot.transform.forward * .72f - origin).z : float.PositiveInfinity;
            Check(shot != null && shot.GetComponent<Pickup>() != null && lodgedTipDepth < .15f,
                "an arrow stops at cover in front of the muzzle before reaching the target");
            wall.SetActive(false);
            target.SetActive(false);
            yield return ReviewArrowSilhouette(player, origin + Vector3.forward * 5f);
        }
        finally
        {
            if (shot != null) Destroy(shot);
            Destroy(view);
            Destroy(target);
            Destroy(wall);
            cameraField.SetValue(player, oldCamera);
            rigField.SetValue(player, oldRig);
            player.DebugHoldDraw(0);
            player.AddArrows(oldArrows - player.Arrows);
            body.position = player.transform.position = position;
            body.rotation = player.transform.rotation = rotation;
            body.isKinematic = wasKinematic;
            if (!wasKinematic) body.linearVelocity = velocity;
            Physics.SyncTransforms();
        }
    }

    IEnumerator ReviewArrowSilhouette(PlayerControlSpeed player, Vector3 location)
    {
        var actor = Instantiate(BattleLoading.ZombiePrefab, location, Quaternion.identity);
        var enemy = actor.GetComponent<EnemyController>();
        enemy.Init(ZombieType.Walker, false, false);
        actor.GetComponent<NavMeshAgent>().enabled = false;
        var animator = actor.GetComponent<Animator>();
        animator.Play("Locomotion", 0, 0);
        animator.Update(0);
        animator.enabled = false;
        var capsule = actor.GetComponent<CapsuleCollider>();
        Physics.SyncTransforms();
        float before = enemy.Health;
        Ray miss = default;
        bool found = false;
        for (float y = .48f; y <= 1.2f && !found; y += .12f)
            for (int side = -1; side <= 1 && !found; side += 2)
            {
                var origin = actor.transform.TransformPoint(new Vector3(capsule.radius * .87f * side, y, -1.5f));
                var candidate = new Ray(origin, actor.transform.forward);
                float distance = 3f;
                found = capsule.Raycast(candidate, out _, 3f) &&
                    !ArrowSurface.Trace(enemy, candidate, false, ref distance, out _, out _);
                if (found) miss = candidate;
            }
        Check(found, "the testing ray passes through the Walker movement capsule outside the actual silhouette");
        if (found)
        {
            Vector3 target = Arrow.AimTarget(miss, miss.origin, player.transform);
            var arrow = Arrow.FireBow(miss.origin, miss.direction, .5f, player.transform);
            yield return Wait(.17f);
            Check((target - miss.origin).magnitude > 100f && Mathf.Approximately(enemy.Health, before),
                "neither crosshair convergence nor a flying arrow awards a Walker hit outside its actual silhouette");
            if (arrow != null) Destroy(arrow.gameObject);
        }

        var chest = enemy.ArrowBody.bones.First(b => b.name == "Base HumanRibcage").position;
        var bodyRay = new Ray(chest - actor.transform.forward * .8f, actor.transform.forward);
        Vector3 bodyTarget = Arrow.AimTarget(bodyRay, bodyRay.origin, player.transform);
        var bodyShot = Arrow.Fire(bodyRay.origin, bodyRay.direction, 55f, .23f, 0, player.transform);
        yield return Wait(.12f);
        Check((bodyTarget - bodyRay.origin).magnitude < 1f &&
            Mathf.Abs(before - enemy.Health - .23f) < .001f,
            "a point-blank arrow contacts the visible torso and deals normal body damage");
        if (bodyShot != null) Destroy(bodyShot.gameObject);
        Destroy(actor);
    }
}
#endif
