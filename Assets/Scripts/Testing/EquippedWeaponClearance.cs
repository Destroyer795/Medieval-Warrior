#if UNITY_EDITOR
using UnityEngine;

// Shared by the authoring review and live gameplay regression. Capsules cover
// blade, crossguard, grip and pommel; the shield box includes its rim and straps.
public static class EquippedWeaponClearance
{
    static readonly Vector3[] Starts={new Vector3(-.25f,0,0),new Vector3(-.25f,-.126f,0),new Vector3(-.443f,0,0)};
    static readonly Vector3[] Ends={new Vector3(.457f,0,0),new Vector3(-.25f,.126f,0),new Vector3(-.29f,0,0)};
    static readonly float[] Radii={.029f,.018f,.034f};
    public static float Measure(Matrix4x4 sword,Matrix4x4 shield,Bounds bounds)
    {
        var relative=shield.inverse*sword;float best=float.MaxValue;
        for(int segment=0;segment<Starts.Length;segment++)
        {
            Vector3 a=relative.MultiplyPoint3x4(Starts[segment]),b=relative.MultiplyPoint3x4(Ends[segment]);
            // Distance to a convex box along a segment is convex. Minimize it
            // at sub-mm precision, then subtract the conservative weapon radius.
            float lo=0,hi=1;
            for(int k=0;k<24;k++)
            {
                float p=(lo*2+hi)/3,q=(lo+hi*2)/3;
                if(bounds.SqrDistance(Vector3.Lerp(a,b,p))<bounds.SqrDistance(Vector3.Lerp(a,b,q)))hi=q;else lo=p;
            }
            best=Mathf.Min(best,Mathf.Sqrt(bounds.SqrDistance(Vector3.Lerp(a,b,(lo+hi)*.5f)))-Radii[segment]);
        }
        return best*((Vector3)shield.GetColumn(0)).magnitude;
    }
}
#endif
