using UnityEngine;

namespace CarXCase.Apartment
{
    /// Spins the platter + record of the record player around its own vertical axis.
    public sealed class RecordSpin : MonoBehaviour
    {
        [Tooltip("Обороты в минуту (33⅓ или 45)")] public float rpm = 33.333f;
        public bool playing = true;
        void Update()
        {
            if (playing) transform.Rotate(0f, rpm * 6f * Time.deltaTime, 0f, Space.Self);
        }
    }
}
