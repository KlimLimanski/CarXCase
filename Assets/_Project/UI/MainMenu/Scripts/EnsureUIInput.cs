using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace NoirMenu
{
    /// <summary>Safety net: makes sure the UI input module has actions (mouse, keyboard, gamepad).</summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class EnsureUIInput : MonoBehaviour
    {
        void Awake()
        {
            var module = GetComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null || module.point == null || module.point.action == null)
                module.AssignDefaultActions();
        }
    }
}
