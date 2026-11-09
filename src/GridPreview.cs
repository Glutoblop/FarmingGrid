using UnityEngine;
using UnityEngine.Rendering;

namespace FarmingGrid
{
    internal sealed class GridPreview
    {
        internal const int HalfWidth = 2;
        private const int Side = 2 * HalfWidth + 1;
        private const int SegmentsPerEdge = 3;
        private static readonly Vector2[] Corners =
        {
            new Vector2(-1f, -1f), new Vector2(-1f, 1f),
            new Vector2(1f, 1f), new Vector2(1f, -1f)
        };

        private readonly LineRenderer[] _lines = new LineRenderer[Side * Side];
        private readonly Vector3[] _outline = new Vector3[4 * SegmentsPerEdge];
        private GameObject _root;
        private Material _material;
        private Vector3 _lastCenter;
        private float _lastStep;
        private SlotColor _lastColor;
        private float _refreshAt;

        internal void Draw(Vector3 center, float step, SlotColor selectedColor, Plugin plugin, Player player)
        {
            if (!_root && !Create())
                return;

            if (_root.activeSelf && Time.unscaledTime < _refreshAt && center == _lastCenter
                && step == _lastStep && selectedColor == _lastColor)
                return;

            _lastCenter = center;
            _lastStep = step;
            _lastColor = selectedColor;
            _refreshAt = Time.unscaledTime + 0.12f;
            _root.SetActive(true);

            int index = 0;
            for (int z = -HalfWidth; z <= HalfWidth; z++)
            {
                for (int x = -HalfWidth; x <= HalfWidth; x++)
                {
                    LineRenderer line = _lines[index++];
                    bool selected = x == 0 && z == 0;
                    Vector3 slot = center + new Vector3(x * step, 0f, z * step);
                    if (!plugin.TryGetSoil(slot, out RaycastHit hit)
                        || !SampleOutline(plugin, hit.point, Mathf.Min(step * 0.36f, 0.65f)))
                    {
                        line.enabled = false;
                        continue;
                    }

                    SlotColor status = selected ? selectedColor : plugin.GetPreviewColor(player, hit);
                    Color color = status == SlotColor.Blocked ? new Color(1f, 0.19f, 0.12f)
                        : status == SlotColor.Near ? new Color(1f, 0.8f, 0.05f)
                        : new Color(0.22f, 1f, 0.36f);
                    color.a = selected ? 1f : 0.65f;
                    line.startColor = line.endColor = color;
                    line.widthMultiplier = selected ? 0.043f : 0.024f;
                    line.SetPositions(_outline);
                    line.enabled = true;
                }
            }
        }

        private bool SampleOutline(Plugin plugin, Vector3 center, float extent)
        {
            int index = 0;
            for (int edge = 0; edge < Corners.Length; edge++)
            {
                Vector2 start = Corners[edge];
                Vector2 end = Corners[(edge + 1) % Corners.Length];
                for (int segment = 0; segment < SegmentsPerEdge; segment++)
                {
                    Vector2 offset = Vector2.Lerp(start, end, (float)segment / SegmentsPerEdge) * extent;
                    if (!plugin.TryGetSoil(center + new Vector3(offset.x, 0f, offset.y), out RaycastHit hit))
                        return false;
                    _outline[index++] = hit.point + Vector3.up * 0.035f;
                }
            }
            return true;
        }

        private bool Create()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (!shader)
                return false;

            _material = new Material(shader);
            _root = new GameObject("Farming Grid preview");
            Object.DontDestroyOnLoad(_root);
            for (int i = 0; i < _lines.Length; i++)
            {
                GameObject marker = new GameObject("Planting slot");
                marker.transform.SetParent(_root.transform, false);
                LineRenderer line = marker.AddComponent<LineRenderer>();
                line.sharedMaterial = _material;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.loop = true;
                line.positionCount = _outline.Length;
                _lines[i] = line;
            }
            return true;
        }

        internal void Hide()
        {
            if (_root)
                _root.SetActive(false);
        }

        internal void Dispose()
        {
            if (_root)
                Object.Destroy(_root);
            if (_material)
                Object.Destroy(_material);
        }
    }
}
