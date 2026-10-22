using UnityEngine;

// Slow cinematic orbit around the castle for the menu backdrop.
public class MenuCameraOrbit : MonoBehaviour
{
    public Vector3 center;
    public float radius = 120f;
    public float height = 40f;
    public float speed = 2.5f;      // degrees per second
    float angle = 200f;

    void LateUpdate()
    {
        angle += speed * Time.unscaledDeltaTime;
        float bob = Mathf.Sin(Time.unscaledTime * 0.15f) * 6f;
        Vector3 pos = center + Quaternion.Euler(0, angle, 0) * Vector3.back * radius + Vector3.up * (height + bob);
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(center + Vector3.up * 10f - pos);
    }
}
