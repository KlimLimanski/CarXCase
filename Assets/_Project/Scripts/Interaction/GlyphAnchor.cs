using UnityEngine;

namespace CarXCase.Interaction
{
    /// Place on an empty child to pin the prompt to an exact point of the object.
    public sealed class GlyphAnchor : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 1f, .8f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * .06f);
        }
    }
}
