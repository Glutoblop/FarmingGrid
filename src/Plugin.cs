using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace FarmingGrid
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jordan.valheim.farminggrid";
        public const string Name = "Farming Grid";
        public const string Version = "0.1.0";

        private static Plugin _instance;
        private ConfigEntry<bool> _enabled;
        private Harmony _harmony;
        private PlantSpacing _spacing;
        private GridPreview _grid;
        private Plant _plant;
        private Piece _piece;
        private GameObject _ghost;
        private bool _hitSoil;
        private bool _snapped;
        private Vector3 _slot;
        private Heightmap _soil;
        private float _step;
        private int _terrainMask;
        private int _waterMask;
        private int _previewFrame = -10;

        private void Awake()
        {
            if (Application.isBatchMode)
                return;

            _enabled = Config.Bind("General", "Enabled", true, "Show the planting grid and enforce plant spacing.");
            _spacing = new PlantSpacing();
            _grid = new GridPreview();
            _terrainMask = LayerMask.GetMask("terrain");
            _waterMask = LayerMask.GetMask("Water");
            _instance = this;
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void LateUpdate()
        {
            Player player = Player.m_localPlayer;
            if (_grid != null && (!_enabled.Value || !player || !_ghost || player.m_placementGhost != _ghost
                || Time.frameCount - _previewFrame > 1 || !Hud.instance || !Hud.instance.IsVisible()
                || Hud.IsPieceSelectionVisible() || Menu.IsVisible() || Minimap.IsOpen()))
                _grid.Hide();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _grid?.Dispose();
            _instance = null;
        }

        [HarmonyPatch(typeof(Player), "PieceRayTest")]
        private static class SnapToSlot
        {
            private static void Postfix(Player __instance, bool __result, ref Vector3 point, ref Vector3 normal,
                ref Heightmap heightmap, ref Collider waterSurface)
            {
                Plugin plugin = _instance;
                if (!plugin || __instance != Player.m_localPlayer || !plugin._enabled.Value)
                    return;

                if (!__result || !plugin._plant)
                    return;

                plugin._hitSoil = heightmap;
                Vector3 aim = point;
                float step = plugin._step;
                aim.x = Mathf.Round(aim.x / step) * step;
                aim.z = Mathf.Round(aim.z / step) * step;
                if (!plugin.TryGetSoil(aim, out RaycastHit hit))
                    return;

                plugin._slot = hit.point;
                plugin._soil = hit.collider.GetComponent<Heightmap>();
                plugin._snapped = true;
                // Keep vanilla's ray hit for obstacles, while still showing the blocked slot below them.
                if (!plugin._hitSoil)
                    return;

                point = hit.point;
                normal = hit.normal;
                heightmap = plugin._soil;
                if (plugin._piece.m_noInWater && plugin.TryGetWaterAbove(point, out RaycastHit water))
                    waterSurface = water.collider;
            }
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static class CheckSpacing
        {
            private static void Prefix(Player __instance)
            {
                Plugin plugin = _instance;
                if (!plugin || __instance != Player.m_localPlayer)
                    return;

                plugin._hitSoil = false;
                plugin._snapped = false;
                plugin._plant = null;
                plugin._ghost = __instance.m_placementGhost;
                if (!plugin._enabled.Value || !plugin._ghost)
                    return;

                GameObject prefab = __instance.m_buildPieces?.GetSelectedPrefab();
                if (!prefab || !prefab.TryGetComponent(out Plant plant) || plant.m_attachDistance > 0f
                    || plant.m_growRadius <= 0f || !prefab.TryGetComponent(out Piece piece) || piece.m_waterPiece)
                    return;

                plugin._plant = plant;
                plugin._piece = piece;
                plugin._step = plugin._spacing.GetStep(plant);
            }

            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Player __instance, bool flashGuardStone)
            {
                Plugin plugin = _instance;
                if (!plugin || __instance != Player.m_localPlayer)
                    return;

                if (!plugin._plant || !plugin._ghost)
                {
                    plugin._grid.Hide();
                    return;
                }

                Vector3 position = plugin._ghost.transform.position;
                if (!plugin._hitSoil || !plugin._snapped || !plugin.WithinReach(__instance, position)
                    || Mathf.Abs(position.x - plugin._slot.x) > 0.01f
                    || Mathf.Abs(position.z - plugin._slot.z) > 0.01f)
                    Block(__instance, Player.PlacementStatus.Invalid);

                if (!plugin._snapped)
                {
                    plugin._grid.Hide();
                    return;
                }

                if (__instance.m_placementStatus == Player.PlacementStatus.Valid
                    && plugin._plant.m_needCultivatedGround
                    && !plugin._soil.IsCultivated(position))
                    Block(__instance, Player.PlacementStatus.NeedCultivated);
                if (__instance.m_placementStatus == Player.PlacementStatus.Valid
                    && (plugin._soil.GetBiome(position) & plugin._plant.m_biome) == 0)
                    Block(__instance, Player.PlacementStatus.WrongBiome);

                bool draw = !flashGuardStone && Hud.instance && Hud.instance.IsVisible()
                    && !Hud.IsPieceSelectionVisible() && !Menu.IsVisible() && !Minimap.IsOpen();
                SlotColor color = SlotColor.Blocked;
                if (draw || __instance.m_placementStatus == Player.PlacementStatus.Valid)
                    plugin._spacing.CollectNeighbors(plugin._slot, plugin._plant, plugin._step);
                if (__instance.m_placementStatus == Player.PlacementStatus.Valid)
                {
                    color = plugin._spacing.GetColor(position, plugin._plant);
                    if (color == SlotColor.Blocked)
                        Block(__instance, Player.PlacementStatus.MoreSpace);
                }
                if (draw)
                {
                    plugin._grid.Draw(plugin._slot, plugin._step, color, plugin, __instance);
                    plugin._previewFrame = Time.frameCount;
                }
            }

            private static void Block(Player player, Player.PlacementStatus status)
            {
                if (player.m_placementStatus != Player.PlacementStatus.Valid)
                    return;
                player.m_placementStatus = status;
                player.SetPlacementGhostValid(false);
            }
        }

        internal bool TryGetSoil(Vector3 reference, out RaycastHit hit)
        {
            // Use the actual terrain collider. Heightmap.GetHeight samples the nearest vertex.
            return Physics.Raycast(reference + Vector3.up * 12f, Vector3.down, out hit, 24f,
                _terrainMask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponent<Heightmap>();
        }

        private bool WithinReach(Player player, Vector3 position)
        {
            return Vector3.Distance(player.m_eye.position, position)
                < player.m_maxPlaceDistance + (float)_piece.m_extraPlacementDistance;
        }

        private bool TryGetWaterAbove(Vector3 position, out RaycastHit hit)
        {
            return Physics.Raycast(position + Vector3.up * 12f, Vector3.down, out hit, 12f, _waterMask);
        }

        internal SlotColor GetPreviewColor(Player player, RaycastHit hit)
        {
            Vector3 position = hit.point;
            Heightmap heightmap = hit.collider.GetComponent<Heightmap>();
            if (!WithinReach(player, position)
                || ((_plant.m_needCultivatedGround || _piece.m_cultivatedGroundOnly) && !heightmap.IsCultivated(position))
                || (heightmap.GetBiome(position) & _plant.m_biome) == 0
                || (_piece.m_noInWater && TryGetWaterAbove(position, out _)))
                return SlotColor.Blocked;

            return _spacing.GetColor(position, _plant);
        }
    }
}
