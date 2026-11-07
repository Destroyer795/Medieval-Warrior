using UnityEngine;

// Unity-native exports of the editable Blender source. Bone indices and bind poses
// match the existing animation rig; no demo vertices or texture atlas are reused.
public class MarchwardenAsset : ScriptableObject
{
    public Mesh body, tunic, waistArmor, sword, shield, bow, quiver, arrow;
    public ArmorJoint[] armorJoints;
    [System.Serializable] public class ArmorJoint
    {
        public int parent;
        public Matrix4x4 pelvisFrame;
        public Vector3[] probes;
    }
    public Matrix4x4 quiverMount;
    public Vector3 quiverSuspension;
    public Material linenMaterial;
    public Matrix4x4 meshToCharacter;
    public ClothCapsule[] clothCapsules;
    [System.Serializable] public struct ClothCapsule
    {
        public string name;
        public int bone;
        public Vector3 start,end;
        public float radius;
    }
    public Material[] materials;
    public Matrix4x4 swordMount, shieldMount;
    public Matrix4x4 leftPalm, rightPalm, shieldGripMount;
    public Vector3 drawHook;
    public Quaternion leftWristRest=Quaternion.identity,rightWristRest=Quaternion.identity;
    public int swordBone, shieldBone;
    public Quaternion chestFrameRotation;
    public Vector3 headEyeLocal;
    public float nativeWalkSpeed = 2f, nativeRunSpeed = 4.4f;
    public float statureScale=1f;
}
