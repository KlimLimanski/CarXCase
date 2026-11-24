using UnityEngine;

namespace CarXCase.Apartment
{
    /// Spins the platter + record of the record player around its own vertical axis.
    public sealed class RecordSpin : MonoBehaviour
    {
        [Tooltip("Обороты в минуту (33⅓ или 45)")] public float rpm = 33.333f;
        public bool playing = true;
        public Vector3 spinAxis = Vector3.up;
        void Update()
        {
            if (playing) transform.Rotate(spinAxis, rpm * 6f * Time.deltaTime, Space.Self);
        }
    }
}
