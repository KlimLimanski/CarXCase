using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DetectiveGame.Core
{
    public enum QteType
    {
        SinglePress,
        RapidMash
    }

    public class QteManager : MonoBehaviour
    {
        public static QteManager Instance { get; private set; }

        private bool _isActive = false;
        private QteType _currentType;
        private Key _targetKey;
        private string _targetKeyName = "";

        private float _timeLimit = 2.5f;
        private float _timeRemaining = 0f;

        private int _requiredMashes = 8;
        private int _currentMashes = 0;

        private Action _onSuccess;
        private Action _onFailure;

        private float _resultTimer = 0f;
        private bool _lastResultSuccess = false;
        private string _resultMessage = "";

        private void Awake()
        {
            if (Instance != null && Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (_resultTimer > 0f)
            {
                _resultTimer -= Time.deltaTime;
            }

            if (!_isActive)
            {
                if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                {
                    StartRandomTestQte();
                }
                return;
            }

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                EndQte(false);
                return;
            }

            if (Keyboard.current == null)
            {
                return;
            }

            var keyControl = Keyboard.current[_targetKey];
            if (keyControl == null)
            {
                return;
            }

            if (_currentType == QteType.SinglePress)
            {
                if (keyControl.wasPressedThisFrame)
                {
                    EndQte(true);
                }
            }
            else if (_currentType == QteType.RapidMash)
            {
                if (keyControl.wasPressedThisFrame)
                {
                    _currentMashes++;
                    if (_currentMashes >= _requiredMashes)
                    {
                        EndQte(true);
                    }
                }
            }
        }

        public void StartSinglePress(Key key, string keyName, float duration, Action successCallback, Action failureCallback)
        {
            _isActive = true;
            _currentType = QteType.SinglePress;
            _targetKey = key;
            _targetKeyName = keyName;
            _timeLimit = duration;
            _timeRemaining = duration;
            _onSuccess = successCallback;
            _onFailure = failureCallback;
        }

        public void StartRapidMash(Key key, string keyName, int requiredPresses, float duration, Action successCallback, Action failureCallback)
        {
            _isActive = true;
            _currentType = QteType.RapidMash;
            _targetKey = key;
            _targetKeyName = keyName;
            _timeLimit = duration;
            _timeRemaining = duration;
            _requiredMashes = requiredPresses;
            _currentMashes = 0;
            _onSuccess = successCallback;
            _onFailure = failureCallback;
        }

        public void StartRandomTestQte()
        {
            int randomRoll = UnityEngine.Random.Range(0, 2);
            if (randomRoll == 0)
            {
                StartSinglePress(Key.Space, "ПРОБЕЛ", 2.2f, 
                    () => ShowResult(true, "УСПЕХ: РЕАКЦИЯ!"), 
                    () => ShowResult(false, "ПРОВАЛ: НЕ УСПЕЛ!"));
            }
            else
            {
                StartRapidMash(Key.E, "E", 7, 3.0f, 
                    () => ShowResult(true, "УСПЕХ: ЗАМОК ВЗЛОМАН!"), 
                    () => ShowResult(false, "ПРОВАЛ: СИЛ НЕ ХВАТИЛО!"));
            }
        }

        private void EndQte(bool success)
        {
            _isActive = false;
            ShowResult(success, success ? "УСПЕХ!" : "ПРОВАЛ!");

            if (success)
            {
                _onSuccess?.Invoke();
            }
            else
            {
                _onFailure?.Invoke();
            }
        }

        private void ShowResult(bool success, string message)
        {
            _lastResultSuccess = success;
            _resultMessage = message;
            _resultTimer = 1.3f;
        }

        public bool IsQteActive() => _isActive;

        private void OnGUI()
        {
            if (_resultTimer > 0f && !_isActive)
            {
                float resWidth = 360f;
                float resHeight = 65f;
                Rect resRect = new Rect((Screen.width - resWidth) / 2f, (Screen.height - resHeight) / 2f - 40f, resWidth, resHeight);
                GUI.Box(resRect, "");

                var resStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold
                };
                resStyle.normal.textColor = _lastResultSuccess ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.25f, 0.25f);
                GUI.Label(resRect, _resultMessage, resStyle);
                return;
            }

            if (!_isActive)
            {
                return;
            }

            float width = 420f;
            float height = 180f;
            Rect centerBox = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Box(centerBox, "");
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            string actionTitle = _currentType == QteType.SinglePress 
                ? "БЫСТРАЯ РЕАКЦИЯ!" 
                : "БЫСТРО НАЖИМАЙТЕ КЛАВИШУ!";

            GUI.Label(new Rect(centerBox.x, centerBox.y + 15f, width, 25f), actionTitle, titleStyle);

            var keyPromptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                fontStyle = FontStyle.Bold
            };
            keyPromptStyle.normal.textColor = Color.white;

            float pulse = 1f + Mathf.PingPong(Time.time * 4f, 0.15f);
            GUI.Label(new Rect(centerBox.x, centerBox.y + 45f, width, 50f), $"[  {_targetKeyName}  ]", keyPromptStyle);

            float timeRatio = Mathf.Clamp01(_timeRemaining / _timeLimit);
            Color barColor = Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(0.2f, 0.9f, 1f), timeRatio);

            float barWidth = width - 60f;
            float barHeight = 16f;
            Rect barBgRect = new Rect(centerBox.x + 30f, centerBox.y + 110f, barWidth, barHeight);
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            GUI.Box(barBgRect, "");

            Rect barFillRect = new Rect(centerBox.x + 30f, centerBox.y + 110f, barWidth * timeRatio, barHeight);
            GUI.color = barColor;
            GUI.Box(barFillRect, "");
            GUI.color = Color.white;

            if (_currentType == QteType.RapidMash)
            {
                float mashRatio = Mathf.Clamp01((float)_currentMashes / _requiredMashes);
                Rect mashFillRect = new Rect(centerBox.x + 30f, centerBox.y + 135f, barWidth * mashRatio, 10f);
                GUI.color = new Color(0.3f, 1f, 0.4f);
                GUI.Box(mashFillRect, "");
                GUI.color = Color.white;

                var mashStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11
                };
                GUI.Label(new Rect(centerBox.x, centerBox.y + 150f, width, 20f), $"{_currentMashes} / {_requiredMashes}", mashStyle);
            }
        }
    }
}
