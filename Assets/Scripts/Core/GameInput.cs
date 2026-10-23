using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// One place for every gameplay input, so keyboard/mouse and a gamepad both work everywhere.
//
//              Keyboard / mouse        Gamepad
//   Move       WASD                    left stick
//   Look       mouse                   right stick
//   Attack     left mouse              right trigger
//   Block/aim  right mouse or E        left trigger
//   Dodge      Space                   A / cross
//   Sprint     Left Shift              left stick click or left bumper
//   Swap       Q (1 sword, 2 bow)      Y / triangle (d-pad left/right)
//   Pause      Esc or P                Start
public static class GameInput
{
    public static bool UsingGamepad { get; private set; }

#if ENABLE_INPUT_SYSTEM
    static Gamepad Pad => Gamepad.current;

    static void Note(bool padActive)
    {
        if (padActive) UsingGamepad = true;
    }
#endif

    static void NoteMouseKeyboard(bool active)
    {
        if (active) UsingGamepad = false;
    }

    public static Vector2 Move
    {
        get
        {
            var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            NoteMouseKeyboard(v.sqrMagnitude > 0.01f);
#if ENABLE_INPUT_SYSTEM
            if (Pad != null)
            {
                var s = Pad.leftStick.ReadValue();
                if (s.magnitude > 0.18f) { v += s; Note(true); }
            }
#endif
            return Vector2.ClampMagnitude(v, 1f);
        }
    }

    /// <summary>Camera turn this frame, in "mouse axis" units (already frame-rate independent for sticks).</summary>
    public static Vector2 Look
    {
        get
        {
            var v = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            NoteMouseKeyboard(v.sqrMagnitude > 0.5f);
#if ENABLE_INPUT_SYSTEM
            if (Pad != null)
            {
                var s = Pad.rightStick.ReadValue();
                if (s.magnitude > 0.15f) { v += s * 2.4f * Time.unscaledDeltaTime * 60f; Note(true); }
            }
#endif
            return v;
        }
    }

    public static bool AttackDown => Key(Input.GetMouseButtonDown(0)) || PadPressed(p => p.rightTrigger);
    public static bool AttackHeld => Input.GetMouseButton(0) || PadHeld(p => p.rightTrigger);
    public static bool SecondaryHeld => Input.GetMouseButton(1) || Input.GetKey(KeyCode.E) || PadHeld(p => p.leftTrigger);
    public static bool DodgeDown => Key(Input.GetKeyDown(KeyCode.Space)) || PadPressed(p => p.buttonSouth);
    public static bool SprintHeld => Input.GetKey(KeyCode.LeftShift) || PadHeld(p => p.leftStickButton) || PadHeld(p => p.leftShoulder);
    public static bool SwapDown => Key(Input.GetKeyDown(KeyCode.Q)) || PadPressed(p => p.buttonNorth);
    public static bool SwordDown => Key(Input.GetKeyDown(KeyCode.Alpha1)) || PadPressed(p => p.dpad.left);
    public static bool BowDown => Key(Input.GetKeyDown(KeyCode.Alpha2)) || PadPressed(p => p.dpad.right);
    public static bool PauseDown => Key(Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) || PadPressed(p => p.startButton);
    public static float Zoom
    {
        get
        {
            float z = Input.GetAxis("Mouse ScrollWheel");
#if ENABLE_INPUT_SYSTEM
            if (Pad != null)
            {
                if (Pad.dpad.up.isPressed) { z += 0.6f * Time.unscaledDeltaTime; Note(true); }
                if (Pad.dpad.down.isPressed) { z -= 0.6f * Time.unscaledDeltaTime; Note(true); }
            }
#endif
            return z;
        }
    }

    static bool Key(bool pressed)
    {
        NoteMouseKeyboard(pressed);
        return pressed;
    }

#if ENABLE_INPUT_SYSTEM
    static bool PadPressed(System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl> control)
    {
        var p = Pad;
        if (p == null) return false;
        bool v = control(p).wasPressedThisFrame;
        Note(v);
        return v;
    }

    static bool PadHeld(System.Func<Gamepad, UnityEngine.InputSystem.Controls.ButtonControl> control)
    {
        var p = Pad;
        return p != null && control(p).isPressed;
    }
#else
    static bool PadPressed(System.Func<object, object> control) => false;
    static bool PadHeld(System.Func<object, object> control) => false;
#endif

    public static string ControlsHint => UsingGamepad
        ? "LS move  ·  RS look  ·  L-click sprint  ·  A dodge  ·  RT attack  ·  LT block / aim  ·  Y swap weapon  ·  Start pause"
        : "WASD move  ·  Shift sprint  ·  Space dodge  ·  LMB attack  ·  RMB block / aim  ·  Q swap weapon  ·  Esc pause";
}
