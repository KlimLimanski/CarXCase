using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarXCase.Harbor
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class HarborRainImpacts : MonoBehaviour
    {
        public Transform characterVisual;

        readonly List<Collider> surfaces = new();
        readonly List<ParticleSystem.Particle> nearby = new();
        ParticleSystem rain, spray;
        BoxCollider vicinity;

        void Awake()
        {
            if (!characterVisual) { enabled = false; return; }

            rain = GetComponent<ParticleSystem>();
            var bones = new Dictionary<string, Transform>();
            foreach (var bone in characterVisual.GetComponentsInChildren<Transform>(true))
                bones.TryAdd(bone.name, bone);

            // Dimensions are in the rig's unscaled metres, measured in its bind pose.
            AddCapsule(bones["Head"], Vector3.up * .034f, Vector3.up * .117f, .104f);
            foreach (float x in new[] { -.09f, .09f })
            {
                var offset = Vector3.right * x;
                AddCapsule(bones["Hips"], offset, offset + Vector3.up * .22f, .14f);
                AddCapsule(bones["Chest"], offset, offset + Vector3.up * .15f, .15f);
            }
            foreach (string side in new[] { "L_", "R_" })
            {
                void Limb(string start, string end, float radius)
                {
                    var bone = bones[side + start];
                    Vector3 axis = bone.InverseTransformPoint(bones[side + end].position);
                    // Round the ends inside the joint rather than extending a full radius past it.
                    var inset = axis.normalized * Mathf.Min(radius * .55f, axis.magnitude * .25f);
                    AddCapsule(bone, inset, axis - inset, radius);
                }
                Limb("Shoulder", "UpperArm", .09f);
                Limb("UpperArm", "LowerArm", .085f);
                Limb("LowerArm", "Hand", .064f);
                Limb("UpperLeg", "LowerLeg", .105f);
                Limb("LowerLeg", "Foot", .077f);
                Limb("Foot", "Toes", .075f);
                var hand = bones[side + "Hand"];
                Vector3 handAxis = hand.InverseTransformDirection((hand.position - bones[side + "LowerArm"].position).normalized);
                AddCapsule(hand, Vector3.zero, handAxis * .095f, .05f);
            }

            vicinity = NewTrigger<BoxCollider>(characterVisual);
            vicinity.center = Vector3.up;
            vicinity.size = new Vector3(3f, 16f, 3f);
            var trigger = rain.trigger;
            trigger.SetCollider(0, vicinity);
            trigger.inside = ParticleSystemOverlapAction.Callback;
            trigger.enter = ParticleSystemOverlapAction.Ignore;
            trigger.enabled = true;

            var node = new GameObject("Rain impact spray");
            node.transform.SetParent(transform, false);
            spray = node.AddComponent<ParticleSystem>();
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            main.maxParticles = 96;
            var emission = spray.emission;
            emission.enabled = false;
            var renderer = node.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetComponent<ParticleSystemRenderer>().sharedMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1f;
            renderer.velocityScale = .007f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            spray.Play();
        }

        // Only the rain queries these triggers. They must not take part in gameplay contacts.
        static T NewTrigger<T>(Transform parent) where T : Collider
        {
            var node = new GameObject("Rain contact") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            node.transform.SetParent(parent, false);
            var collider = node.AddComponent<T>();
            collider.isTrigger = true;
            collider.excludeLayers = ~0;
            return collider;
        }

        void AddCapsule(Transform bone, Vector3 from, Vector3 to, float radius)
        {
            var cap = NewTrigger<CapsuleCollider>(bone);
            cap.transform.localPosition = (from + to) * .5f;
            if (to != from) cap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            cap.radius = radius;
            cap.height = Vector3.Distance(from, to) + 2f * radius;
            surfaces.Add(cap);
        }

        void OnParticleTrigger()
        {
            if (!vicinity || !vicinity.gameObject.activeInHierarchy) return;
            const ParticleSystemTriggerEventType type = ParticleSystemTriggerEventType.Inside;
            int count = rain.GetTriggerParticles(type, nearby);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                var drop = nearby[i];
                Vector3 step = drop.velocity * Time.deltaTime;
                float distance = step.magnitude;
                if (distance <= 0f || drop.remainingLifetime <= 0f) continue;
                var ray = new Ray(drop.position - step, step / distance);
                RaycastHit first = default;
                foreach (var surface in surfaces)
                {
                    if (!surface.Raycast(ray, out var hit, distance)) continue;
                    first = hit;
                    distance = hit.distance;
                }
                if (!first.collider) continue;
                drop.remainingLifetime = -1f;
                nearby[i] = drop;
                changed = true;
                Splash(first);
            }
            if (changed) rain.SetTriggerParticles(type, nearby);
        }

        void Splash(RaycastHit hit)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 tangent = Vector3.ProjectOnPlane(Random.insideUnitSphere, hit.normal);
                var drop = new ParticleSystem.EmitParams
                {
                    position = hit.point + hit.normal * .003f,
                    velocity = hit.normal * Random.Range(.22f, .52f) + tangent * .65f,
                    startSize = Random.Range(.004f, .009f),
                    startLifetime = Random.Range(.065f, .135f),
                    startColor = new Color(.66f, .75f, .85f, .46f)
                };
                spray.Emit(drop, 1);
            }
        }
    }
}
