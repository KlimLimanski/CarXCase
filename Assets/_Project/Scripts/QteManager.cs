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
        private Transform _anchor;
        private string _label;

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
            ShowGlyph();

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
                    CarXCase.Interaction.InteractionGlyphs.Press(this);
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
            CarXCase.Interaction.InteractionGlyphs.Finish(this, success);
            _anchor = null;
            _label = null;

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

        /// Where the key hangs and what it says; QteTrigger sets this before starting.
        public void Present(Transform anchor, string label)
        {
            _anchor = anchor;
            _label = label;
        }

        /// Detroit-style prompt: the key floats in the shot (at the object, or to the side of frame),
        /// trembles while the window is open, and a bar under it shows the time left.
        void ShowGlyph()
        {
            var cam = Camera.main;
            if (!cam && !_anchor) return;
            Vector3 point = _anchor ? CarXCase.Interaction.InteractionGlyphs.PointOf(_anchor)
                : cam.ViewportToWorldPoint(new Vector3(.64f, .46f, 1.6f));
            string label = _currentType == QteType.RapidMash
                ? (string.IsNullOrEmpty(_label) ? "Жмите быстрее" : _label) + $"   {_currentMashes}/{_requiredMashes}"
                : _label;
            CarXCase.Interaction.InteractionGlyphs.Show(this, point, _targetKeyName, label, _timeRemaining / _timeLimit, true);
        }
    }
}
