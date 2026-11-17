using UnityEditor;
using UnityEngine;

public static class ActionFeedbackReview
{
    [MenuItem("Medieval Warrior/Combat/Rebuild complete sword strokes and slope fixes")]
    public static void Build()
    {
        BuildingBuilder.SoftenHouseSlopes();
        CombatAnimationBuilder.Build();
        SwordClearanceReview.Run();
        CombatOverhaulReview.VerifyAudio();
        Debug.Log("[Action feedback] PASS: graded village slopes, complete sword arcs, equipment clearance and audible creature bank");
    }
    public static void BuildSwordOnly()
    {
        CombatAnimationBuilder.Build();SwordClearanceReview.Run();CombatOverhaulReview.VerifyAudio();
    }
}
