#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public partial class SmokeTestDriver
{
    IEnumerator ReviewGraveCombat(GraveDuelist duelist, PlayerHealth knight)
    {
        if (duelist == null) { Check(false, "grave opponent emerged"); yield break; }
        var enemy = duelist.GetComponent<EnemyController>();
        var control = knight.GetComponent<PlayerControlSpeed>();
        var body = knight.GetComponent<Rigidbody>();
        var originalPosition = body.position;
        var originalRotation = body.rotation;
        float originalHp = knight.currentHealth, immunity = knight.InvulnerableUntil;
        Weapon originalWeapon = control.CurrentWeapon;
        var spawnClock = typeof(GameManager).GetField("spawnTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float nextSpawn = (float)spawnClock.GetValue(GameManager.instance);
        spawnClock.SetValue(GameManager.instance, 999f);
        Check(Mathf.Approximately(enemy.MaxHealth, knight.maxHealth * .6f) &&
            duelist.StartingWeapon == originalWeapon && duelist.GetComponentInChildren<MarchwardenVisual>().Body.sharedMesh ==
            knight.GetComponent<MarchwardenVisual>().Body.sharedMesh, "the grave double copies the knight and starts with sixty percent max health");

        Vector3 line = Vector3.ProjectOnPlane(originalPosition - enemy.transform.position, Vector3.up).normalized;
        if (line.sqrMagnitude < .5f) line = Vector3.forward;
        // Find one unobstructed strip of courtyard with room for both weapon ranges.
        for (int i = 0; i < 16; i++)
        {
            var axis = Quaternion.AngleAxis(i * 22.5f, Vector3.up) * line;
            if (NavMesh.Raycast(enemy.transform.position, enemy.transform.position + axis * 8, out _, NavMesh.AllAreas)) continue;
            if (!CombatBoons.ClearLine(enemy.transform.position + Vector3.up, enemy.transform.position + axis * 8 + Vector3.up)) continue;
            line = axis;
            break;
        }
        void AtRange(float meters)
        {
            var point = enemy.transform.position + line * meters;
            if (NavMesh.SamplePosition(point, out var floor, 1.5f, NavMesh.AllAreas)) point = floor.position;
            body.position = knight.transform.position = point;
            body.rotation = knight.transform.rotation = Quaternion.LookRotation(-line);
            Physics.SyncTransforms();
        }
        IEnumerator Observe(Func<bool> condition, float seconds)
        {
            float timeout = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < timeout) yield return null;
        }
        try
        {
            // The encounter may have brought a few ambient zombies into
            // range before the first isolated hit. Measure only the duel.
            foreach (var bystander in EnemyController.All.ToArray())
                if (bystander != null && bystander != enemy)
                {
                    bystander.gameObject.SetActive(false);
                    Destroy(bystander.gameObject);
                }
            var privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GraveDuelist).GetMethod("InterruptAttack", privateInstance).Invoke(duelist, new object[] { .2f });
            typeof(GraveDuelist).GetField("step", privateInstance).SetValue(duelist, 0);
            knight.InvulnerableUntil = 0;
            control.Equip(Weapon.Sword);
            AtRange(1.45f);
            float hp = knight.currentHealth;
            yield return Observe(() => knight.currentHealth < hp, 5f);
            Check(Mathf.Abs(hp - knight.currentHealth - Boons.SwordDamage) < .02f,
                $"an unguarded grave sword cut deals the knight's light sword damage ({hp - knight.currentHealth:F2})");

            control.DebugBlock(true);
            hp = knight.currentHealth;
            yield return Observe(() => knight.currentHealth < hp, 4f);
            Check(hp > knight.currentHealth && hp - knight.currentHealth < Boons.SwordDamage * .4f,
                "the knight's raised shield absorbs the grave sword strike");
            control.DebugBlock(false);
            knight.InvulnerableUntil = float.PositiveInfinity;
            // Normal shield hits apply a brief recoil before a counter is accepted.
            yield return new WaitForSeconds(.5f);
            float enemyHp = enemy.Health;
            control.DebugSwingStep(0);
            yield return Observe(() => enemy.Health < enemyHp, 2f);
            Check(Mathf.Abs(enemyHp - enemy.Health - Boons.SwordDamage) < .02f,
                "the player's sword lands on the grave duelist through normal melee combat");

            AtRange(7.5f);
            knight.InvulnerableUntil = 0;
            hp = knight.currentHealth;
            yield return Observe(() => duelist.DrawAmount > .79f, 6f);
            int arrows = duelist.Arrows;
            Vector3 bowTarget = body.position;
            body.position = knight.transform.position = bowTarget + Vector3.Cross(line, Vector3.up) * 2f;
            Physics.SyncTransforms();
            yield return Observe(() => duelist.Arrows < arrows, 2f);
            yield return new WaitForSeconds(.22f);
            Check(duelist.Arrows < arrows && Mathf.Approximately(knight.currentHealth, hp),
                "moving sideways after the enemy commits its aim evades the shot");
            AtRange(7.5f);
            yield return Observe(() => knight.currentHealth < hp, 7f);
            Check(duelist.CurrentWeapon == Weapon.Bow && Mathf.Abs(hp - knight.currentHealth - 3.4f) < .02f,
                "a full draw from the enemy bow flies into the knight for normal arrow damage");
            knight.InvulnerableUntil = float.PositiveInfinity;
            enemyHp = enemy.Health;
            control.Equip(Weapon.Bow);
            control.DebugFireAt(enemy.transform.position + Vector3.up, 1);
            yield return Observe(() => enemy.Health < enemyHp, 2f);
            Check(Mathf.Abs(enemyHp - enemy.Health - 3.4f) < .02f, "a normal player arrow hits the grave knight's torso");

            enemy.TakeDamage(enemy.Health - .5f, line, 0);
            control.DebugFireAt(enemy.transform.position + Vector3.up, 1);
            yield return Observe(() => duelist.IsDefeated, 2f);
            Check(duelist.IsDefeated && !EnemyController.All.Contains(enemy) && !enemy.GetComponent<CapsuleCollider>().enabled,
                "a lethal player shot finishes the duel and releases its collision and enemy slot");
            var loot = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p =>
                Vector3.Distance(p.transform.position, enemy.transform.position) < 4).ToArray();
            Check(loot.Any(p => p.kind == PickupKind.Health && p.amount >= 50) &&
                loot.Any(p => p.kind == PickupKind.Arrows && p.amount >= 20), "the defeated double drops a large draught and a full arrow bundle");
        }
        finally
        {
            control.DebugBlock(false);
            control.Equip(originalWeapon);
            body.position = knight.transform.position = originalPosition;
            body.rotation = knight.transform.rotation = originalRotation;
            knight.currentHealth = originalHp;
            knight.InvulnerableUntil = immunity;
            spawnClock.SetValue(GameManager.instance, nextSpawn);
        }
    }
}
#endif
