using UnityEngine;

// One scene-level binding, with no per-frame work or per-building components.
[ExecuteAlways]
public sealed class EnvironmentSurfaceLighting : MonoBehaviour
{
    public Texture2D groundHeight;
    public Vector3 terrainOrigin,terrainSize;
    void OnEnable()=>Apply();
    void OnValidate()=>Apply();
    void Apply()
    {
        if(groundHeight==null)return;
        Shader.SetGlobalTexture("_EnvironmentGroundHeight",groundHeight);
        Shader.SetGlobalVector("_EnvironmentTerrainOrigin",terrainOrigin);
        Shader.SetGlobalVector("_EnvironmentTerrainSize",terrainSize);
        Shader.SetGlobalFloat("_EnvironmentGroundMode",1);
    }
    void OnDisable()=>Shader.SetGlobalFloat("_EnvironmentGroundMode",0);
}
