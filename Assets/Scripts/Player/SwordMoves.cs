using UnityEngine;

// Four three-hit phrases. Durations and contact times are shared by the animation
// authoring tool and combat, so damage occurs during the cutting phase.
public static class SwordMoves
{
    public const float Crossfade=.050f, FadeIn=.035f, RecoveryFade=.075f, InputBuffer=.16f;
    public readonly struct Move
    {
        public readonly string name;
        public readonly float duration, contact, windup, followThrough, chain;
        public readonly bool thrust;
        public Move(string name, float duration, float contact, bool thrust = false)
        {
            this.name=name;this.duration=duration;this.contact=contact;this.thrust=thrust;
            windup=contact-(thrust?.055f:.065f);
            followThrough=contact+(thrust?.065f:.075f);
            chain=duration-.085f;
        }
    }
    public static readonly Move[] All = {
        new Move("Descending forehand", .37f, .150f), new Move("Returning backhand", .36f, .145f),
        new Move("High descending cut", .44f, .180f), new Move("Rising forehand", .36f, .145f),
        new Move("Horizontal cut", .36f, .145f), new Move("Straight thrust", .39f, .145f, true),
        new Move("Descending backhand", .38f, .155f), new Move("Low rising return", .36f, .145f),
        new Move("High-line thrust", .41f, .160f, true), new Move("Short outside cut", .34f, .135f),
        new Move("Inside returning cut", .37f, .150f), new Move("High backhand finish", .45f, .185f)
    };
    public static int Count => All.Length;
    public static AnimationClip Clip(int variant) => Resources.Load<AnimationClip>("Animations/Sword/Strike_" + variant.ToString("00"));
}
