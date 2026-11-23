using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using UnityEngine;

namespace JuicyChickenGames.Menu
{
    public partial class TargetDialog
    {
        private OverworldScene targetingWorld;
        private List<CampaignRoute> gateTargets = new();
        private CampaignRoute selectedGate;
        private string gateActionName;
        private Action<CampaignRoute> useGate;
        private int gateSubmitFrame = -1;
        internal string TargetRouteId => selectedGate?.Id;

        internal void Setup(OverworldScene world, string actionName, List<CampaignRoute> targets,
            string preferredRoute, Action<CampaignRoute> confirm)
        {
            targetingWorld = world;
            gateActionName = actionName;
            gateTargets = targets;
            useGate = confirm;
            gateSubmitFrame = -1;
            // The reusable view references the same indicator prefab used by dungeon items.
            if (!TargetIndicator.scene.IsValid()) TargetIndicator = Instantiate(TargetIndicator, world.transform);
            TargetIndicator.transform.localScale = Vector3.one * world.Map.Template.cellSize / 2.5f;
            enabled = true;
            SelectTargetPrompt.SetActive(true);
            promptText = SelectTargetPrompt.GetComponentInChildren<TMPro.TMP_Text>(true);
            originalPrompt = promptText.text;
            RefreshPrompt();
            audioReady = false;
            SelectGate(targets.FirstOrDefault(route => route.Id == preferredRoute) ?? targets[0]);
            pointerReady = false;
            TerminalPointerFocus = null;
            lastPointer = MenuUIInputModule.Active?.UI.Point.ReadValue<Vector2>() ?? Vector2.zero;
            audioReady = true;
        }

        private IEnumerable<Vector3Int> GateCells(CampaignRoute route)
        {
            if (route == null || targetingWorld == null) return Array.Empty<Vector3Int>();
            var world = targetingWorld;
            return (route.IsWarp ? new[] { world.Position } : world.Map.CurrentGrid.Locks
                .Where(gate => gate.RouteId == route.Id).SelectMany(gate => gate.Cells)
                .Where(cell => !world.Map.CurrentGrid.RequiresBoat(cell)))
                .Select(cell => new Vector3Int(cell.X, cell.Y, 0));
        }

        private Vector3Int GateCell(CampaignRoute route) => GateCells(route)
            .OrderBy(cell => Mathf.Abs(cell.x - targetingWorld.Position.X) + Mathf.Abs(cell.y - targetingWorld.Position.Y)).First();

        private string GateLabel(CampaignRoute route)
        {
            var world = targetingWorld;
            var from = world.Map.CurrentGrid.Locations[route.From];
            var to = world.Map.CurrentGrid.Locations[route.To];
            int Distance(GridPoint point) => Math.Abs(point.X - world.Position.X) + Math.Abs(point.Y - world.Position.Y);
            string destination = Distance(from) <= Distance(to) ? route.To : route.From;
            return "Gate to " + CampaignGuidance.LocationName(world.Context, destination);
        }

        private void SelectGate(CampaignRoute route)
        {
            if (audioReady && selectedGate != route) AudioManager.Instance?.PlayCue(SoundCue.Navigate);
            selectedGate = route;
            var cell = GateCell(route);
            TargetIndicator.SetActive(!TerminalMode.Effective);
            TargetIndicator.transform.position = targetingWorld.CellCenterToWorld(new GridPoint(cell.x, cell.y)) + Vector3.back * .05f;
            HudRefresh.Request(HudChange.Travel);
        }

        internal bool TrySelectGateCell(Vector3Int cell)
        {
            var route = gateTargets.FirstOrDefault(candidate => GateCells(candidate).Contains(cell));
            if (route == null) return false;
            var focus = TerminalPointerFocus ?? TerminalSelectedCell;
            SelectGate(route);
            TerminalPointerFocus = focus;
            return true;
        }

        private bool TryPointGate(Vector2 position)
        {
            var camera = targetingWorld.ViewCamera;
            if (camera == null || !camera.pixelRect.Contains(position) || PointerHitsControl(position)) return false;
            var ray = camera.ScreenPointToRay(position);
            CampaignRoute closest = null;
            float distance = float.PositiveInfinity;
            foreach (var route in gateTargets)
                foreach (var renderer in targetingWorld.GateRenderers(route.Id))
                    if (renderer.enabled && renderer.bounds.IntersectRay(ray, out float hit) && hit < distance)
                    { closest = route; distance = hit; }
            if (closest != null) { SelectGate(closest); return true; }
            var ground = new Plane(Vector3.forward, targetingWorld.Map.transform.position);
            if (!ground.Raycast(ray, out distance)) return false;
            var point = (ray.GetPoint(distance) - targetingWorld.Map.transform.position) / targetingWorld.Map.Template.cellSize;
            return TrySelectGateCell(new Vector3Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y), 0));
        }

        private void UpdateGateTargeting()
        {
            if (Owner?.Current != this || Common.Instance.GlobalSettings.IsOpen ||
                MenuUIInputModule.Active?.InputConsumed == true || AutoplayRunner.BlocksPlayerInput) return;
            RefreshPrompt();
            TargetIndicator.SetActive(!TerminalMode.Effective);
            if (ProcessPointer()) return;
            var direction = MenuUIInputModule.Active?.NavigationStep ?? Vector2.zero;
            if (direction == Vector2.zero) return;
            TerminalPointerFocus = null;
            var current = GateCell(selectedGate);
            var choices = gateTargets.Where(route => route != selectedGate).ToList();
            var next = choices.Where(route => Vector2.Dot(((Vector2)(Vector3)(GateCell(route) - current)).normalized, direction) > .7f)
                .OrderBy(route => (GateCell(route) - current).sqrMagnitude).FirstOrDefault();
            next ??= choices.OrderBy(route => Vector2.Dot((Vector2)(Vector3)GateCell(route), direction)).FirstOrDefault();
            if (next != null) SelectGate(next);
        }

        private void ConfirmGateTarget()
        {
            if (Owner?.Current != this || selectedGate == null || useGate == null || gateSubmitFrame == Time.frameCount ||
                Common.Instance.GlobalSettings.IsOpen || Common.Instance.Travel.IsTransitioning) return;
            gateSubmitFrame = Time.frameCount;
            useGate(selectedGate);
            Common.Instance.MenuInputHandler.ClearInputThisFrame();
        }
    }
}
