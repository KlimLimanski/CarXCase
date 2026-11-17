using UnityEngine;

namespace CarXCase.Precinct
{
    /// Turns hard camera cuts into short eased blends and damps small jitters.
    public sealed class CamSmoother
    {
        public float blendTime = 1.1f, follow = 0.2f, cutDistance = 0.6f, cutAngle = 20f;
        Vector3 pos, fromPos, lastPos; Quaternion rot, fromRot, lastRot; float fov, fromFov; float blend = 1; bool init;

        public void Reset() { init = false; }

        public void Apply(Camera cam, Vector3 tPos, Quaternion tRot, float tFov, float dt)
        {
            if (!cam) return;
            if (!init) { pos = tPos; rot = tRot; fov = tFov; lastPos = tPos; lastRot = tRot; init = true; }
            if ((tPos - lastPos).magnitude > cutDistance || Quaternion.Angle(tRot, lastRot) > cutAngle)
            { fromPos = pos; fromRot = rot; fromFov = fov; blend = 0; }
            lastPos = tPos; lastRot = tRot;
            if (blend < 1)
            {
                blend = Mathf.Min(1, blend + dt / Mathf.Max(blendTime, .01f));
                float k = blend * blend * blend * (blend * (6 * blend - 15) + 10); // smootherstep
                pos = Vector3.Lerp(fromPos, tPos, k); rot = Quaternion.Slerp(fromRot, tRot, k); fov = Mathf.Lerp(fromFov, tFov, k);
            }
            else
            {
                float a = 1 - Mathf.Exp(-dt / Mathf.Max(follow, .001f));
                pos = Vector3.Lerp(pos, tPos, a); rot = Quaternion.Slerp(rot, tRot, a); fov = Mathf.Lerp(fov, tFov, a);
            }
            cam.transform.SetPositionAndRotation(pos, rot); cam.fieldOfView = fov;
        }
    }
}
