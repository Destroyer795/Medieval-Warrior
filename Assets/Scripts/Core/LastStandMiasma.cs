using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
internal sealed class LastStandMiasma : MonoBehaviour
{
    const CameraEvent CompositeEvent = CameraEvent.BeforeForwardAlpha;
    static readonly int SceneBuffer = Shader.PropertyToID("_LastStandOpaqueScene");
    static readonly int FogBuffer = Shader.PropertyToID("_LastStandFog");

    Camera view;
    Material mist;
    CommandBuffer composite;
    Texture3D turbulence;
    Texture2D relief;
    bool originalForceIntoTexture;
    DepthTextureMode originalDepth;
    Vector3 sunrise;

    void Awake()
    {
        view = GetComponent<Camera>();
        var shader = Resources.Load<Shader>("LastStand/Miasma");
        if (shader == null || !shader.isSupported) return;

        mist = new Material(shader) { name = "Encroaching Corruption" };
        turbulence = MakeTurbulence();
        mist.SetTexture("_Turbulence", turbulence);
        originalDepth = view.depthTextureMode;
        originalForceIntoTexture = view.forceIntoRenderTexture;
        view.depthTextureMode |= DepthTextureMode.Depth;
        view.forceIntoRenderTexture = true;

        // Integrate at half resolution. Composite before rain and fire, so the
        // atmospheric pass sees solid geometry and the HDR streaks can bloom.
        composite = new CommandBuffer { name = "Last Stand volumetric mist" };
        var sceneFormat = view.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        composite.GetTemporaryRT(SceneBuffer, -1, -1, 0, FilterMode.Point, sceneFormat);
        composite.GetTemporaryRT(FogBuffer, -2, -2, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        composite.Blit(BuiltinRenderTextureType.CameraTarget, SceneBuffer);
        composite.Blit(SceneBuffer, FogBuffer, mist, 0);
        composite.Blit(SceneBuffer, BuiltinRenderTextureType.CameraTarget, mist, 1);
        composite.ReleaseTemporaryRT(FogBuffer);
        composite.ReleaseTemporaryRT(SceneBuffer);
        view.AddCommandBuffer(CompositeEvent, composite);
    }

    public void SetWeather(Vector3 center, float radius, float time, float storm, float density, float retreat,
        float ceiling, float highGroundExposure, float haze, float sunlight, Vector3 sunDirection)
    {
        if (mist == null) return;
        if (relief == null && Terrain.activeTerrain != null) BindTerrain(Terrain.activeTerrain);
        mist.SetVector("_Zone", new Vector4(center.x, center.y, center.z, radius));
        mist.SetVector("_Weather", new Vector4(time, storm, density, retreat));
        mist.SetVector("_Air", new Vector4(ceiling, highGroundExposure, haze, sunlight));
        sunrise = sunDirection;
    }

    void OnPreRender()
    {
        if (mist == null) return;
        var cameraTransform = view.transform;
        var projection = view.projectionMatrix;
        mist.SetVector("_ViewOrigin", cameraTransform.position);
        mist.SetVector("_ViewForward", cameraTransform.forward);
        mist.SetVector("_ViewRight", cameraTransform.right / projection.m00);
        mist.SetVector("_ViewUp", cameraTransform.up / projection.m11);
        mist.SetVector("_ViewOffset", new Vector4(projection.m02, projection.m12, 0f, 0f));
        mist.SetVector("_SunDirection", sunrise);
        mist.SetVector("_ScreenSun", view.WorldToViewportPoint(cameraTransform.position + sunrise * 100f));
    }

    void BindTerrain(Terrain terrain)
    {
        // Only the broad relief is needed. A small height texture anchors the
        // low mist to the landscape without querying terrain per camera frame.
        const int size = 128;
        var data = terrain.terrainData;
        Vector3 origin = terrain.GetPosition();
        float[] heights = new float[size * size];
        for (int z = 0; z < size; z++)
        for (int x = 0; x < size; x++)
            heights[z * size + x] = origin.y + data.GetInterpolatedHeight(x / (size - 1f), z / (size - 1f));
        relief = new Texture2D(size, size, TextureFormat.RFloat, false, true)
        {
            name = "Last Stand terrain relief",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        relief.SetPixelData(heights, 0);
        relief.Apply(false, true);
        mist.SetTexture("_Relief", relief);
        mist.SetVector("_Terrain", new Vector4(origin.x, origin.z, 1f / data.size.x, 1f / data.size.z));
    }

    static Texture3D MakeTurbulence()
    {
        const int size = 32;
        float[] values = new float[size * size * size];
        var random = new System.Random(1607);
        for (int cells = 4; cells <= 16; cells *= 2)
        {
            float[] corners = new float[cells * cells * cells];
            for (int i = 0; i < corners.Length; i++) corners[i] = (float)random.NextDouble();
            float scale = cells / (float)size;
            float weight = cells == 4 ? .57f : cells == 8 ? .28f : .15f;
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float sx = x * scale, sy = y * scale, sz = z * scale;
                int ix = (int)sx, iy = (int)sy, iz = (int)sz;
                float u = Mathf.SmoothStep(0f, 1f, sx - ix);
                float v = Mathf.SmoothStep(0f, 1f, sy - iy);
                float w = Mathf.SmoothStep(0f, 1f, sz - iz);
                float bottom = Mathf.Lerp(CornerRow(ix, iy, iz, u), CornerRow(ix, iy + 1, iz, u), v);
                float top = Mathf.Lerp(CornerRow(ix, iy, iz + 1, u), CornerRow(ix, iy + 1, iz + 1, u), v);
                values[(z * size + y) * size + x] += Mathf.Lerp(bottom, top, w) * weight;
            }

            float CornerRow(int x, int y, int z, float blend)
            {
                int row = ((z % cells) * cells + y % cells) * cells;
                return Mathf.Lerp(corners[row + x], corners[row + (x + 1) % cells], blend);
            }
        }
        byte[] pixels = new byte[values.Length];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)Mathf.RoundToInt(values[i] * 255f);
        var noise = new Texture3D(size, size, size, TextureFormat.R8, false)
        {
            name = "Last Stand turbulence",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        noise.SetPixelData(pixels, 0);
        noise.Apply(false, true);
        return noise;
    }

    void OnDestroy()
    {
        if (composite != null)
        {
            if (view != null)
            {
                view.RemoveCommandBuffer(CompositeEvent, composite);
                view.forceIntoRenderTexture = originalForceIntoTexture;
                view.depthTextureMode = originalDepth;
            }
            composite.Release();
        }
        if (mist != null) Destroy(mist);
        if (turbulence != null) Destroy(turbulence);
        if (relief != null) Destroy(relief);
    }
}
