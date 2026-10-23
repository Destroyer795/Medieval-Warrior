using UnityEngine;

// Slowly turns the skybox so the clouds drift. Runs on an instance of the sky material
// so the asset on disk is never modified.
public class SkyDrift : MonoBehaviour
{
    public float degreesPerSecond = 0.6f;
    Material sky;
    float rotation;

    void Start()
    {
        if (RenderSettings.skybox == null || !RenderSettings.skybox.HasProperty("_Rotation")) { enabled = false; return; }
        sky = new Material(RenderSettings.skybox);
        rotation = sky.GetFloat("_Rotation");
        RenderSettings.skybox = sky;
    }

    void Update()
    {
        rotation = (rotation + degreesPerSecond * Time.unscaledDeltaTime) % 360f;
        sky.SetFloat("_Rotation", rotation);
    }
}
