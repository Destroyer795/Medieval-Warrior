#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class SmokeTestDriver
{
    IEnumerator ReviewRevenantHitMotion(EnemyController enemy, RevenantVisual visual, Animator animator)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var controller = typeof(EnemyController);
        var state = controller.GetField("state", flags);
        var cooldown = controller.GetField("guardBrokenUntil", flags);
        void State(string name) => state.SetValue(enemy, Enum.Parse(state.FieldType, name));
        bool bulwark = enemy.type == ZombieType.Bulwark;
        var bones = visual.Body.bones;
        var watched = new[] { "LArmUpperarm", "LArmForearm", "LArmPalm",
            "RArmUpperarm", "RArmForearm", "RArmPalm", "Neck", "Head" }
            .Select(name => bones.First(b => b.name == "Base Human" + name)).ToArray();
        var previous = new Quaternion[watched.Length];
        float largestStep = 0, outwardDot = 1;
        bool havePrevious = false;
        Vector3 forward = enemy.transform.forward;
        Vector3 contact = enemy.transform.position + Vector3.up * (1.1f * enemy.transform.lossyScale.y);
        float savedCapture = Time.captureDeltaTime;
        float savedScale = Time.timeScale;
        var savedCull = animator.cullingMode;
        IEnumerator Observe(int frames, bool checkShield, bool hit = true)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (hit && frame % 7 == 0) enemy.ReceiveWeaponHit(.025f, forward, 0, contact);
                lateCheck = () =>
                {
                    for (int i = 0; i < watched.Length; i++)
                    {
                        var rotation = watched[i].localRotation;
                        if (havePrevious) largestStep = Mathf.Max(largestStep, Quaternion.Angle(rotation, previous[i]));
                        previous[i] = rotation;
                    }
                    havePrevious = true;
                    if (checkShield && bulwark)
                        outwardDot = Mathf.Min(outwardDot,
                            Vector3.Dot(visual.ShieldHand.TransformDirection(visual.Form.left.normal), forward));
                };
                yield return null;
            }
        }

        try
        {
            Time.captureDeltaTime = 1f / 90;
            State("Hunt");
            cooldown.SetValue(enemy, 0f);
            animator.Play("Locomotion", 0, 0);
            animator.SetFloat("Speed", 0);
            yield return Observe(22, bulwark);

            animator.enabled = false;
            var held = visual.ShieldHand.localRotation;
            var elbow = watched[1].localRotation;
            enemy.ReceiveWeaponHit(.05f, -forward, 0, contact, bash: bulwark);
            Time.timeScale = 0;
            yield return null;
            yield return null;
            Check(Quaternion.Angle(held, visual.ShieldHand.localRotation) < .1f &&
                Quaternion.Angle(elbow, watched[1].localRotation) < .1f,
                enemy.type + " holds its arm pose through hits while the rig is frozen");
            Time.timeScale = savedScale;
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.SetFloat("Speed", visual.Form.walkSpeed);
            yield return Observe(20, bulwark);
            animator.SetFloat("Speed", visual.Form.runSpeed);
            yield return Observe(28, bulwark);
            cooldown.SetValue(enemy, 0f);
            yield return Observe(20, bulwark);

            for (int variant = 0; variant < visual.Form.attacks.Length; variant++)
            {
                State("Attack");
                animator.SetFloat("AttackStyle", variant);
                animator.SetFloat("AttackMul", 1.15f);
                animator.CrossFadeInFixedTime("Attack", .055f, 0, 0);
                int strokeFrames = Mathf.CeilToInt(visual.Form.attacks[variant].length * 90f / 1.15f);
                yield return Observe(strokeFrames, false);
                State("Staggered");
                if (bulwark) cooldown.SetValue(enemy, Time.time + .23f);
                animator.CrossFadeInFixedTime("Locomotion", .07f, 0);
                yield return Observe(24, false);
                State("Hunt");
            }

            // Measure the fall separately so the authored transition into
            // an intentional knockdown is not treated as a strike wrist flip.
            Check(largestStep < 100f, $"{enemy.type} head and arms stay continuous through repeated hits, walk, run, attacks and stagger (max {largestStep:F1}° per frame)");
            if (bulwark) Check(outwardDot > .15f, $"Bulwark shield stays outward when bracing, lowering and recovering (minimum forward dot {outwardDot:F2})");
            State("Dead");
            controller.GetField("<IsDead>k__BackingField", flags).SetValue(enemy, true);
            for (int direction = 0; direction < 2; direction++)
            {
                animator.Play(direction == 0 ? "Die Back" : "Die Forward", 0, 0);
                havePrevious = false;
                largestStep = 0;
                yield return Observe(48, false, hit: false);
                Check(largestStep < 100f, $"{enemy.type} head and arms stay continuous through the {(direction == 0 ? "backward" : "forward")} fall (max {largestStep:F1}° per frame)");
            }
        }
        finally
        {
            lateCheck = null;
            animator.enabled = true;
            animator.cullingMode = savedCull;
            Time.timeScale = savedScale;
            Time.captureDeltaTime = savedCapture;
        }
    }
}
#endif
