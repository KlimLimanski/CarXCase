using UnityEngine;
using UnityEngine.AI;

namespace ReferenceBar {
    // Attach to a character supplied by the game, not to the static environment.
    public class BarAgentZones : MonoBehaviour {
        public int agentTypeId;
        public int allowedAreaMask = NavMesh.AllAreas;
        public float radius = .22f;
        public float height = 1.72f;
        void Awake() {
            var a = GetComponent<NavMeshAgent>();
            if (a == null) a = gameObject.AddComponent<NavMeshAgent>();
            a.agentTypeID = agentTypeId; a.radius = radius; a.height = height;
            a.areaMask = allowedAreaMask;
            a.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            a.avoidancePriority = 60;
        }
    }
}
