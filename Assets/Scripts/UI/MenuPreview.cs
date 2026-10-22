using UnityEngine;

// Turntable for the knight shown on the customisation page. Renders into a RenderTexture.
public class MenuPreview : MonoBehaviour
{
    public Camera previewCamera;
    public Transform knight;
    public float spinSpeed = 18f;

    RenderTexture texture;
    KnightAppearance appearance;
    float dragVelocity;

    // created on first use: the menu UI may ask for these before our Awake has run
    public RenderTexture Texture { get { Init(); return texture; } }
    public KnightAppearance Appearance { get { Init(); return appearance; } }

    void Awake() => Init();

    void Init()
    {
        if (texture != null) return;
        texture = new RenderTexture(900, 1200, 24, RenderTextureFormat.ARGB32) { name = "Knight Preview", antiAliasing = 4 };
        if (previewCamera != null)
        {
            previewCamera.targetTexture = texture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0, 0, 0, 0);
            previewCamera.enabled = false;
        }
        if (knight != null && !knight.TryGetComponent(out appearance))
            appearance = knight.gameObject.AddComponent<KnightAppearance>();
    }

    public void SetActive(bool on)
    {
        if (previewCamera != null) previewCamera.enabled = on;
    }

    public void Drag(float dx) => dragVelocity = dx * 0.4f;

    void Update()
    {
        if (knight == null) return;
        dragVelocity = Mathf.Lerp(dragVelocity, 0f, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
        knight.Rotate(Vector3.up, (spinSpeed + dragVelocity * 60f) * Time.unscaledDeltaTime, Space.World);
    }

    void OnDestroy()
    {
        if (texture != null) texture.Release();
    }
}
