using UnityEngine;

namespace CarXCase.Hero
{
    /// Simple analytic two-bone IK applied after the Animator (works with Generic rigs).
    /// Used to put the driver's hand exactly on the steering wheel rim.
    [DefaultExecutionOrder(10000)]
    public sealed class HeroArmIK : MonoBehaviour
    {
        public Transform upper, lower, end;
        public Transform target;
        [Tooltip("Elbow points toward this transform.")]
        public Transform hint;
        [Range(0, 1)] public float weight = 1;
        public bool alignHandRotation = true;

        // A relaxed wrist follows the forearm. A firm grip matches the target orientation.
        public float GripAlignment { get; set; } = 1f;

        void LateUpdate()
        {
            if (!upper || !lower || !end || !target || weight <= 0) return;
            Solve(upper, lower, end, target.position, hint ? hint.position : (Vector3?)null, weight);
            if (alignHandRotation)
                end.rotation = Quaternion.Slerp(end.rotation, target.rotation, weight * GripAlignment);
        }

        public static void Solve(Transform a, Transform b, Transform c, Vector3 targetPos, Vector3? hintPos, float w)
        {
            Vector3 aPos = a.position, bPos = b.position, cPos = c.position;
            Vector3 tPos = Vector3.Lerp(cPos, targetPos, w);
            float ab = (bPos - aPos).magnitude, bc = (cPos - bPos).magnitude;
            float at = Mathf.Clamp((tPos - aPos).magnitude, 1e-4f, ab + bc - 1e-4f);

            // 1. bend the middle joint to obtain the required distance
            float curAngle = Angle(ab, bc, (cPos - aPos).magnitude);
            float newAngle = Angle(ab, bc, at);
            Vector3 axis = Vector3.Cross(cPos - bPos, aPos - bPos);
            if (axis.sqrMagnitude < 1e-8f)
            {
                axis = hintPos.HasValue ? Vector3.Cross(hintPos.Value - bPos, aPos - bPos) : a.up;
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
            }
            axis.Normalize();
            b.rotation = Quaternion.AngleAxis(curAngle - newAngle, axis) * b.rotation;

            // 2. aim the chain at the target
            cPos = c.position;
            a.rotation = Quaternion.FromToRotation(cPos - aPos, tPos - aPos) * a.rotation;

            // 3. swing the elbow toward the hint around the a→target axis
            if (hintPos.HasValue)
            {
                Vector3 at3 = (tPos - aPos).normalized;
                Vector3 elbow = Vector3.ProjectOnPlane(b.position - aPos, at3);
                Vector3 hintDir = Vector3.ProjectOnPlane(hintPos.Value - aPos, at3);
                if (elbow.sqrMagnitude > 1e-8f && hintDir.sqrMagnitude > 1e-8f)
                {
                    float ang = Vector3.SignedAngle(elbow, hintDir, at3);
                    a.rotation = Quaternion.AngleAxis(ang * w, at3) * a.rotation;
                }
            }
        }

        static float Angle(float ab, float bc, float ac)
        {
            float cos = (ab * ab + bc * bc - ac * ac) / (2 * ab * bc);
            return Mathf.Acos(Mathf.Clamp(cos, -1, 1)) * Mathf.Rad2Deg;
        }
    }
}
