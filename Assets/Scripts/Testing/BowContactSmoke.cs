#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

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
}
#endif
