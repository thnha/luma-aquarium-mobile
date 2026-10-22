using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Bounded visual simulation: one surface material, four impulses, and one bubble emitter.
public sealed class AquariumWaterEffects : MonoBehaviour
{
    public Renderer surface;
    public Camera viewCamera;
    public AquariumDecoration decoration;
    public ParticleSystem bubbles;
    public Transform fish;
    public float surfaceY = 2.42f;
    public float strength = 1;
    public int RippleCount { get; private set; }
    private readonly Vector4[] ripples = new Vector4[4];
    private MaterialPropertyBlock properties;
    private int nextRipple;
    private Vector2 pressPosition;
    private float pressTime;
    private float nextFishRipple;

    private void Awake()
    {
        properties = new MaterialPropertyBlock();
        for (int i = 0; i < ripples.Length; i++) ripples[i].w = -100;
        // The underside of the surface mirrors the tank from the opaque scene colour.
        if (viewCamera != null) viewCamera.GetUniversalAdditionalCameraData().requiresColorOption = CameraOverrideOption.On;
    }

    public void Ripple(Vector3 point)
    {
        float edge = decoration != null ? decoration.max.x + .05f : .9f;
        ripples[nextRipple] = new Vector4(Mathf.Clamp(point.x, -edge, edge), point.z, strength, Time.time);
        nextRipple = (nextRipple + 1) % ripples.Length;
        RippleCount++;
    }

    private void Update()
    {
        properties.SetVectorArray("_Ripples", ripples);
        properties.SetFloat("_Strength", strength);
        surface.SetPropertyBlock(properties);

        if (fish != null && fish.position.y > surfaceY - .22f && Time.time > nextFishRipple)
        {
            Ripple(fish.position);
            nextFishRipple = Time.time + .65f;
        }
        if (decoration != null && decoration.Editing) return;
        var pointer = Pointer.current;
        if (pointer == null) return;
        if (pointer.press.wasPressedThisFrame)
        {
            pressPosition = pointer.position.ReadValue();
            pressTime = Time.unscaledTime;
        }
        if (!pointer.press.wasReleasedThisFrame || Time.unscaledTime - pressTime > .35f ||
            Vector2.Distance(pressPosition, pointer.position.ReadValue()) > 20) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        var ray = viewCamera.ScreenPointToRay(pressPosition);
        if (new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance))
            Ripple(ray.GetPoint(distance));
    }
}
