using UnityEngine;

public sealed class FireBrazier : MonoBehaviour
{
    static Mesh mesh;
    internal static FireBrazier Place(Transform parent, Vector3 position)
    {
        if (mesh == null)
        {
            var kit = new ProceduralModels.MeshKit(2);
            for (int i = 0; i < 3; i++)
            {
                Vector3 foot = Quaternion.Euler(0, i * 120, 0) * Vector3.forward * .43f;
                kit.Tube(0, foot, foot * .56f + Vector3.up * .89f, .055f, .045f, 7);
            }
            kit.Tube(0, Vector3.up * .78f, Vector3.up * 1.06f, .18f, .51f, 16);
            kit.Tube(0, Vector3.up * 1.05f, Vector3.up * 1.10f, .52f, .52f, 16);
            for (int i = 0; i < 7; i++)
            {
                Vector3 coal = Quaternion.Euler(0, i * 137.5f, 0) * Vector3.forward * (i == 0 ? 0 : .26f);
                kit.Ball(1, coal + Vector3.up * 1.1f, new Vector3(.16f, .07f, .12f), 3, 5);
            }
            for (int i = 0; i < 8; i++)
            {
                Vector3 rim = Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * .49f;
                kit.Tube(0, rim + Vector3.up * 1.07f, rim * .92f + Vector3.up * 1.38f, .025f, .017f, 5);
            }
            mesh = kit.Build("Keep fire basket");
        }
        var go = ProceduralModels.Spawn("Archery brazier", mesh, new[] {
            ProceduralModels.Mat("Blackened brazier iron", new Color(.16f,.12f,.09f), .37f, .63f),
            ProceduralModels.Mat("Brazier embers", new Color(.31f,.065f,.014f), .2f, 0, new Color(.58f,.12f,.013f))
        }, parent);
        go.transform.position = position;
        FX.AttachFlame(go.transform, Vector3.up * 1.11f, 1.65f, true);
        var hull = go.AddComponent<CapsuleCollider>();
        hull.center = Vector3.up * .54f; hull.height = 1.08f; hull.radius = .34f;
        return go.AddComponent<FireBrazier>();
    }

    public bool InReach(Vector3 position) => (position - transform.position).sqrMagnitude <= 9f;
}
