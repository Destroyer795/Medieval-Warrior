using UnityEngine;

// Bone-axis-independent analytic arm solver used by the authoring tool and archer.
public static class PoseMath
{
    // Keep the palm's anatomical wrist-to-knuckle direction forward of the
    // forearm. A soft limit prevents a blade target from folding it backwards.
    public static Quaternion WristRotation(Transform fore,Transform hand,Quaternion desired,Vector3 palmDistal)
    {
        Vector3 arm=(hand.position-fore.position).normalized;
        Vector3 fingers=desired*palmDistal.normalized;
        float angle=Vector3.Angle(arm,fingers);
        if(angle<=35)return desired;
        float limited=35+20*(1-Mathf.Exp(-(angle-35)/20));
        Vector3 target=Vector3.RotateTowards(arm,fingers,limited*Mathf.Deg2Rad,0);
        return Quaternion.FromToRotation(fingers,target)*desired;
    }

    public static void SetWrist(Transform fore,Transform hand,Quaternion desired,Vector3 palmDistal,Quaternion restLocal)
    {
        desired=WristRotation(fore,hand,desired,palmDistal);
        Vector3 axis=(hand.position-fore.position).normalized;
        Vector3 localAxis=fore.InverseTransformDirection(axis);
        // Put pronation on the forearm, keeping the wrist's local twist near
        // its anatomical bind pose. The hand endpoint remains unchanged.
        Quaternion targetForearm=desired*Quaternion.Inverse(restLocal);
        fore.rotation=Quaternion.FromToRotation(targetForearm*localAxis,axis)*targetForearm;
        hand.rotation=desired;
    }

    // A complete bend-plane frame fixes the roll ambiguity of FromToRotation
    // when a forearm passes close to the opposite direction of its rest axis.
    // Used for authored cuts; the bow retains its independently tuned solver.
    public static void ArmStable(Transform upper,Transform fore,Transform hand,Vector3 target,Vector3 hint)
    {
        Vector3 shoulder=upper.position;
        Vector3 originalUpper=fore.position-shoulder,originalFore=hand.position-fore.position;
        float a=originalUpper.magnitude,b=originalFore.magnitude;
        Vector3 originalPlane=Vector3.Cross(originalUpper,originalFore).normalized;
        if(originalPlane.sqrMagnitude<.01f)originalPlane=Vector3.ProjectOnPlane(upper.forward,originalUpper).normalized;
        Quaternion upperOffset=Quaternion.Inverse(Quaternion.LookRotation(originalUpper,originalPlane))*upper.rotation;
        Quaternion foreOffset=Quaternion.Inverse(Quaternion.LookRotation(originalFore,originalPlane))*fore.rotation;
        Vector3 delta=target-shoulder;Vector3 direction=delta.normalized;
        float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,(a+b)*.995f);
        Vector3 bend=Vector3.ProjectOnPlane(hint-shoulder,direction).normalized;
        if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(originalPlane,direction).normalized;
        float cosine=Mathf.Clamp((a*a+distance*distance-b*b)/(2*a*distance),-1,1);
        Vector3 elbow=shoulder+(direction*cosine+bend*Mathf.Sqrt(1-cosine*cosine))*a;
        Vector3 endpoint=shoulder+direction*distance;
        Vector3 plane=Vector3.Cross(elbow-shoulder,endpoint-elbow).normalized;
        upper.rotation=Quaternion.LookRotation(elbow-shoulder,plane)*upperOffset;
        fore.rotation=Quaternion.LookRotation(endpoint-elbow,plane)*foreOffset;
    }

    public static void Arm(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 hint, float weight = 1f)
    {
        Quaternion startUpper = upper.rotation, startFore = fore.rotation;
        float a = Vector3.Distance(upper.position, fore.position), b = Vector3.Distance(fore.position, hand.position);
        Vector3 offset = target-upper.position;
        float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(a-b)+.001f, (a+b)*.995f);
        Vector3 direction = offset.sqrMagnitude > .000001f ? offset.normalized : upper.forward;
        float cosine = Mathf.Clamp((a*a+distance*distance-b*b)/(2*a*distance), -1f, 1f);
        Vector3 bend = Vector3.ProjectOnPlane(hint-upper.position, direction);
        if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(Vector3.down, direction);
        Vector3 elbow = upper.position+(direction*cosine+bend.normalized*Mathf.Sqrt(1-cosine*cosine))*a;
        upper.rotation = Quaternion.FromToRotation(fore.position-upper.position, elbow-upper.position)*upper.rotation;
        fore.rotation = Quaternion.FromToRotation(hand.position-fore.position, upper.position+direction*distance-fore.position)*fore.rotation;
        if (weight < .999f)
        {
            upper.rotation = Quaternion.Slerp(startUpper, upper.rotation, weight);
            fore.rotation = Quaternion.Slerp(startFore, fore.rotation, weight);
        }
    }

    public static Quaternion AlongBlade(Vector3 direction, Vector3 normal)
    {
        Vector3 x = direction.normalized, z = Vector3.ProjectOnPlane(normal, x).normalized;
        if (z.sqrMagnitude < .001f) z = Vector3.ProjectOnPlane(Vector3.right,x).normalized;
        return Quaternion.LookRotation(z, Vector3.Cross(z,x));
    }
}
