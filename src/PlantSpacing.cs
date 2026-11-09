using System.Collections.Generic;
using UnityEngine;

namespace FarmingGrid
{
    internal enum SlotColor
    {
        Clear,
        Near,
        Blocked
    }

    internal sealed class PlantSpacing
    {
        private const float Margin = 0.04f;
        private readonly int _spaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
        private readonly Dictionary<GameObject, float> _footprints = new Dictionary<GameObject, float>();
        private readonly List<(Vector3 Position, float Clearance)> _neighbors = new List<(Vector3, float)>();

        internal float GetStep(Plant plant)
        {
            return Mathf.Max(0.35f, plant.m_growRadius + GetFootprint(plant) + WarningDistance(plant) + Margin);
        }

        internal void CollectNeighbors(Vector3 center, Plant selected, float step)
        {
            _neighbors.Clear();
            float selectedFootprint = GetFootprint(selected);
            float gridRadius = step * (GridPreview.HalfWidth * 1.42f + 0.2f);
            float buffer = gridRadius + WarningDistance(selected) + Margin;
            foreach (SlowUpdate instance in SlowUpdate.GetAllInstaces())
            {
                if (!(instance is Plant plant) || !plant || !plant.m_nview || !plant.m_nview.IsValid())
                    continue;

                Vector3 position = plant.transform.position;
                float distance = HorizontalDistance(center, position);
                float clearance = Mathf.Max(selected.m_growRadius + GetGrownFootprint(plant),
                    plant.m_growRadius + selectedFootprint) + Margin;
                if (distance <= clearance + buffer)
                    _neighbors.Add((position, clearance));
            }
        }

        internal SlotColor GetColor(Vector3 position, Plant plant)
        {
            float warning = WarningDistance(plant);
            bool near = false;
            foreach (var neighbor in _neighbors)
            {
                float distance = HorizontalDistance(neighbor.Position, position);
                if (distance < neighbor.Clearance)
                    return SlotColor.Blocked;
                near |= distance < neighbor.Clearance + warning;
            }

            float radius = plant.m_growRadius + Margin;
            // The placement ghost is on Valheim's separate ghost layer.
            if (Physics.CheckSphere(position, radius, _spaceMask))
                return SlotColor.Blocked;
            return near || Physics.CheckSphere(position, radius + warning, _spaceMask) ? SlotColor.Near : SlotColor.Clear;
        }

        private static float WarningDistance(Plant plant)
        {
            return Mathf.Max(0.12f, plant.m_growRadius * 0.2f);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        private float GetFootprint(Plant plant)
        {
            return Mathf.Max(GetPrefabFootprint(plant.gameObject), GetGrownFootprint(plant));
        }

        private float GetGrownFootprint(Plant plant)
        {
            float footprint = 0f;
            foreach (GameObject grown in plant.m_grownPrefabs)
                if (grown)
                    footprint = Mathf.Max(footprint, GetPrefabFootprint(grown));
            return footprint * Mathf.Max(1f, plant.m_maxScale);
        }

        private float GetPrefabFootprint(GameObject prefab)
        {
            if (_footprints.TryGetValue(prefab, out float footprint))
                return footprint;

            // Prefab collider world bounds can be empty. Project their serialized local bounds instead.
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || (_spaceMask & (1 << collider.gameObject.layer)) == 0)
                    continue;

                Bounds bounds;
                switch (collider)
                {
                    case BoxCollider box:
                        bounds = new Bounds(box.center, box.size);
                        break;
                    case SphereCollider sphere:
                        bounds = new Bounds(sphere.center, Vector3.one * (2f * sphere.radius));
                        break;
                    case CapsuleCollider capsule:
                        Vector3 size = Vector3.one * (2f * capsule.radius);
                        size[capsule.direction] = Mathf.Max(size[capsule.direction], capsule.height);
                        bounds = new Bounds(capsule.center, size);
                        break;
                    case MeshCollider mesh when mesh.sharedMesh:
                        bounds = mesh.sharedMesh.bounds;
                        break;
                    default:
                        continue;
                }

                Vector3 root = prefab.transform.position;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    footprint = Mathf.Max(footprint, HorizontalDistance(root, collider.transform.TransformPoint(corner)));
                }
            }

            _footprints.Add(prefab, footprint);
            return footprint;
        }
    }
}
