using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Opportunity
{
    public static class GameSettings
    {
        public static bool LargeText { get => PlayerPrefs.GetInt("ll.large", 0) != 0; set => PlayerPrefs.SetInt("ll.large", value ? 1 : 0); }
        public static bool English { get => PlayerPrefs.GetInt("ll.en", 0) != 0; set => PlayerPrefs.SetInt("ll.en", value ? 1 : 0); }
        public static bool Easy { get => PlayerPrefs.GetInt("ll.easy", 0) != 0; set => PlayerPrefs.SetInt("ll.easy", value ? 1 : 0); }
        public static bool Invert { get => PlayerPrefs.GetInt("ll.invert", 0) != 0; set => PlayerPrefs.SetInt("ll.invert", value ? 1 : 0); }
        public static bool ReducedMotion { get => PlayerPrefs.GetInt("ll.motion", 0) != 0; set => PlayerPrefs.SetInt("ll.motion", value ? 1 : 0); }
        public static bool Subtitles { get => PlayerPrefs.GetInt("ll.subtitles", 1) != 0; set => PlayerPrefs.SetInt("ll.subtitles", value ? 1 : 0); }
        public static bool Voice { get => PlayerPrefs.GetInt("ll.voice", 1) != 0; set => PlayerPrefs.SetInt("ll.voice", value ? 1 : 0); }
        public static float Sensitivity { get => PlayerPrefs.GetFloat("ll.sensitivity", 1); set => PlayerPrefs.SetFloat("ll.sensitivity", value); }
        public static float Master { get => PlayerPrefs.GetFloat("ll.master", .8f); set => PlayerPrefs.SetFloat("ll.master", value); }
        public static float Music { get => PlayerPrefs.GetFloat("ll.music", .65f); set => PlayerPrefs.SetFloat("ll.music", value); }
        public static float Effects { get => PlayerPrefs.GetFloat("ll.effects", .65f); set => PlayerPrefs.SetFloat("ll.effects", value); }
        public static float Interface { get => PlayerPrefs.GetFloat("ll.ui", .6f); set => PlayerPrefs.SetFloat("ll.ui", value); }
    }

    public static class GameInput
    {
        public static bool Simulating;
        public static Vector2 SimulatedMove;
        public static bool SimulatedFast;
        public static bool Press(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        public static bool Hold(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
        public static Vector2 Move
        {
            get
            {
                if (Simulating) return SimulatedMove;
                return new Vector2((Hold(Key.D) || Hold(Key.RightArrow) ? 1 : 0) - (Hold(Key.A) || Hold(Key.LeftArrow) ? 1 : 0), (Hold(Key.W) || Hold(Key.UpArrow) ? 1 : 0) - (Hold(Key.S) || Hold(Key.DownArrow) ? 1 : 0));
            }
        }
        // One action for everything: E or the left mouse button. Observations and the rock tool use the held state.
        public static bool ActionPressed => Press(Key.E) || (!Simulating && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        public static bool ActionHeld => Hold(Key.E) || (!Simulating && Mouse.current != null && Mouse.current.leftButton.isPressed);
        public static Vector2 MouseDelta => Simulating ? Vector2.zero : Mouse.current?.delta.ReadValue() ?? Vector2.zero;
        public static bool Fast
        {
            get
            {
                if (Simulating) return SimulatedFast;
                return Hold(Key.LeftShift) || Hold(Key.RightShift);
            }
        }
    }
}
