using UnityEngine;

public sealed class EnvironmentArtAsset : ScriptableObject
{
    public Mesh supplyCrate;
    public Material crateMaterial;
    static EnvironmentArtAsset cached;
    public static EnvironmentArtAsset Asset=>cached!=null?cached:(cached=Resources.Load<EnvironmentArtAsset>("Buildings/EnvironmentArt"));
}
