using UnityEngine;

// Grow the complete skeleton and its attached kit uniformly. Store the native
// scale so rebuilding a prefab or reopening a scene cannot compound the change.
[DefaultExecutionOrder(-120)]
public sealed class MarchwardenStature : MonoBehaviour
{
    [SerializeField] Vector3 nativeScale=Vector3.one;
    [SerializeField] bool captured;
    public float Multiplier { get; private set; }=1;

    public static MarchwardenStature Ensure(GameObject root,MarchwardenAsset asset)
    {
        var stature=root.GetComponent<MarchwardenStature>();
        if(stature==null)stature=root.AddComponent<MarchwardenStature>();
        stature.Apply(asset.statureScale);
        return stature;
    }

    void Awake()
    {
        var asset=MarchwardenVisual.Asset;
        if(asset!=null)Apply(asset.statureScale);
    }

    public void Apply(float multiplier)
    {
        if(!captured){nativeScale=transform.localScale;captured=true;}
        Multiplier=Mathf.Clamp(multiplier,.8f,1.4f);
        transform.localScale=nativeScale*Multiplier;
    }
}
