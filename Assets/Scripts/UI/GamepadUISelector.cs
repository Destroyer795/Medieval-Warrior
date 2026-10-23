using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Keeps something selected on the frontmost open screen while a gamepad is in use,
// so menus, the pause screen, boon cards and the end screen are all controller-friendly.
public class GamepadUISelector : MonoBehaviour
{
    void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;
        var current = es.currentSelectedGameObject;
        if (!GameInput.UsingGamepad)
        {
            // mouse users: don't leave clicked buttons stuck in their "selected" look (text fields keep focus)
            if (current != null && current.GetComponent<Button>() != null && current.transform.IsChildOf(transform)) es.SetSelectedGameObject(null);
            return;
        }
        if (current != null && current.activeInHierarchy && current.transform.IsChildOf(transform))
        {
            var sel = current.GetComponent<Selectable>();
            if (sel != null && sel.IsInteractable()) return;
        }
        // another canvas owns the focus (e.g. the pause menu over the HUD)
        if (current != null && current.activeInHierarchy && !current.transform.IsChildOf(transform)) return;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;
            foreach (var s in child.GetComponentsInChildren<Selectable>())
            {
                if (!s.IsInteractable() || !s.isActiveAndEnabled) continue;
                es.SetSelectedGameObject(s.gameObject);
                return;
            }
        }
    }
}
