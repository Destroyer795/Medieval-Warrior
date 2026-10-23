using UnityEngine;

// Gives repeated props (the houses) a slightly different colour each, without extra materials.
// The colour is chosen by the environment builder and applied with a MaterialPropertyBlock.
[ExecuteAlways]
public class TintVariation : MonoBehaviour
{
    public Color tint = Color.white;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    public void Apply()
    {
        var mpb = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", tint);
            r.SetPropertyBlock(mpb);
        }
    }
}
