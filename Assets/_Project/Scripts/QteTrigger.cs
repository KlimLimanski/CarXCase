using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Core
{
    public class QteTrigger : MonoBehaviour
    {
        public string actionName = "Взломать заклинивший ящик";
        public QteType qteType = QteType.RapidMash;
        public Key targetKey = Key.Space;
        public string keyDisplayName = "ПРОБЕЛ";
        public float duration = 3.0f;
        public int requiredMashes = 8;

        public Renderer targetRenderer;
        public Color successColor = new Color(0.2f, 0.85f, 0.3f);

        private bool _isResolved = false;

        public void TriggerQte(SimpleUIHUD hud)
        {
            if (_isResolved || QteManager.Instance == null || QteManager.Instance.IsQteActive())
            {
                return;
            }

            QteManager.Instance.Present(targetRenderer ? targetRenderer.transform : transform, actionName);
            if (qteType == QteType.SinglePress)
            {
                QteManager.Instance.StartSinglePress(targetKey, keyDisplayName, duration, OnSuccess, OnFailure);
            }
            else
            {
                QteManager.Instance.StartRapidMash(targetKey, keyDisplayName, requiredMashes, duration, OnSuccess, OnFailure);
            }
        }

        private void OnSuccess()
        {
            _isResolved = true;

            if (targetRenderer != null)
            {
                var mat = targetRenderer.material;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", successColor);
                }
                else if (mat.HasProperty("_Color"))
                {
                    mat.color = successColor;
                }
            }
        }

        private void OnFailure()
        {
        }

        public bool IsResolved() => _isResolved;
    }
}
