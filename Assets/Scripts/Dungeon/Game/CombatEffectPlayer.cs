using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Scene-owned pool. No simulation decisions or random numbers are consumed by presentation.
public sealed class CombatEffectPlayer : MonoBehaviour
{
    sealed class Instance
    {
        internal GameObject Object, Prefab;
        internal ParticleSystem[] Particles;
        internal Renderer[] Renderers;
        internal float Expires;
        internal Transform Follow;
        internal Vector3 Offset;
        internal Vector3? VisibilityPosition;
    }
    readonly List<Instance> active = new();
    readonly Dictionary<GameObject, Stack<Instance>> pool = new();
    readonly Dictionary<Character, Dictionary<string, StatusVisualProfile>> statuses = new();
    readonly Dictionary<Character, Dictionary<StatusVisualProfile, GameObject>> auras = new();
    readonly Dictionary<Character, GameObject> casting = new();
    internal void ClearCasting(Character c)
    {
        if (casting.TryGetValue(c, out var effect)) Release(effect);
        casting.Remove(c);
    }
    internal void ShowCasting(Character c, CombatEffectProfile profile, bool persistent)
    {
        if (persistent && casting.ContainsKey(c)) return;
        ClearCasting(c);
        profile ??= CombatVisualCatalog.Instance?.Utility;
        if (c == null || profile == null || DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations) return;
        var stage = profile.GroundCircle.Prefab != null ? profile.GroundCircle : profile.Muzzle;
        if (stage?.Prefab == null) stage = CombatVisualCatalog.Instance?.Utility?.GroundCircle;
        var effect = Rent(stage, Ground(Body(c, c.TilemapPosition)), Quaternion.identity, persistent, follow: c.transform);
        if (persistent && effect != null) casting[c] = effect;
    }
    Object dungeon;
    bool hadDungeon;
    public int ActiveCount => active.Count;
    void Awake() { dungeon = Game.Instance?.CurrentDungeon; hadDungeon = dungeon != null; }
    public static CombatEffectPlayer Get()
    {
        if (Game.Instance == null) return null;
        return Game.Instance.GetComponent<CombatEffectPlayer>() ?? Game.Instance.gameObject.AddComponent<CombatEffectPlayer>();
    }
    internal static Vector3 Ground(Vector3 point) { point.z = DungeonPresentation.GroundPlaneZ - .03f; return point; }
    internal static float StageScale(CombatEffectStage stage, float size, float cellSize) =>
        Mathf.Max(.01f, stage.Scale * size, stage.MinimumDiameterCells * cellSize / Mathf.Max(.01f, stage.ReferenceDiameter));
    internal static Bounds? TargetBounds(Character target, Vector3Int cell)
    {
        if (target == null) return null;
        Bounds? bounds = null;
        var model = target.VisualParent != null ? target.VisualParent.transform : target.transform;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            // Selection sprites, trails and status particles must not inflate the body size.
            if (!renderer.enabled || renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
            if (bounds.HasValue) { var combined = bounds.Value; combined.Encapsulate(renderer.bounds); bounds = combined; }
            else bounds = renderer.bounds;
        }
        var dungeon = Game.Instance.CurrentDungeon;
        float cellSize = Mathf.Abs(dungeon.CellToWorld(Vector3Int.right).x - dungeon.CellToWorld(Vector3Int.zero).x);
        if (!bounds.HasValue)
        {
            float width = cellSize * (target.FootPrint == FootPrint.Size3x3 ? 3 : 1);
            return new Bounds(Body(target, cell), Vector3.one * width);
        }
        var snapshot = bounds.Value;
        snapshot.center += dungeon.CellToWorld(cell) - target.transform.position;
        return snapshot;
    }

    internal static (Vector3 point, float size) FitImpact(Bounds bounds, Vector3 forward, Vector3 right, Vector3 up, float cellSize)
    {
        float Project(Vector3 axis) => Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
        // Move along the viewing ray: the impact stays centered on screen but clears the mesh's front surface.
        var point = bounds.center - forward * (Project(forward) + Mathf.Max(.2f, cellSize * .1f));
        float diameter = 2 * Mathf.Max(Project(right), Project(up));
        return (point, Mathf.Max(1, diameter / Mathf.Max(.01f, cellSize * 1.4f)));
    }
    internal static Vector3 Body(Character character, Vector3Int cell)
    {
        Vector3 ground = Game.Instance.CurrentDungeon.CellToWorld(cell);
        if (character?.VisualParent != null) ground += character.VisualParent.transform.position - character.transform.position;
        else
        {
            float size = Mathf.Abs(Game.Instance.CurrentDungeon.CellToWorld(Vector3Int.right).x - Game.Instance.CurrentDungeon.CellToWorld(Vector3Int.zero).x);
            ground += new Vector3(size / 2, size / 2, 0);
        }
        return ground + new Vector3(0, 0, -.65f);
    }

    GameObject Rent(CombatEffectStage stage, Vector3 position, Quaternion rotation, bool loop = false, float size = 1, Transform follow = null, Vector3? visibilityPosition = null)
    {
        if (stage?.Prefab == null) return null;
        if (!pool.TryGetValue(stage.Prefab, out var available)) pool[stage.Prefab] = available = new();
        Instance item;
        if (available.Count > 0) item = available.Pop();
        else
        {
            var go = Instantiate(stage.Prefab, transform);
            foreach (var source in go.GetComponentsInChildren<AudioSource>(true)) { source.playOnAwake = false; source.Stop(); source.enabled = false; }
            var effectsGroup = AudioManager.Instance?.EffectAudioMixerGroup;
            if (effectsGroup != null)
                foreach (var source in go.GetComponentsInChildren<AudioSource>(true))
                    source.outputAudioMixerGroup = effectsGroup;
            item = new Instance { Object = go, Prefab = stage.Prefab,
                Particles = go.GetComponentsInChildren<ParticleSystem>(true), Renderers = go.GetComponentsInChildren<Renderer>(true) };
        }
        item.Object.transform.SetPositionAndRotation(position + stage.Offset, rotation * Quaternion.Euler(stage.Rotation) * stage.Prefab.transform.localRotation);
        float cellSize = Game.Instance?.CurrentDungeon != null ? Mathf.Abs(Game.Instance.CurrentDungeon.CellToWorld(Vector3Int.right).x - Game.Instance.CurrentDungeon.CellToWorld(Vector3Int.zero).x) : 2.5f;
        item.Object.transform.localScale = stage.Prefab.transform.localScale * StageScale(stage, size, cellSize);
        item.Expires = loop ? float.PositiveInfinity : Time.unscaledTime + Mathf.Max(.1f, stage.Lifetime);
        item.Follow = follow;
        item.VisibilityPosition = visibilityPosition;
        item.Offset = follow != null ? item.Object.transform.position - follow.position : Vector3.zero;
        item.Object.SetActive(true);
        foreach (var trail in item.Object.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
        foreach (var particles in item.Particles)
        {
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = loop; main.useUnscaledTime = true; main.stopAction = ParticleSystemStopAction.None;
            particles.Play(false);
        }
        active.Add(item);
        SetVisibility(item);
        return item.Object;
    }

    internal void Release(GameObject effect)
    {
        var item = active.FirstOrDefault(i => i.Object == effect);
        if (item == null) return;
        active.Remove(item);
        foreach (var particles in item.Particles) if (particles != null) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (item.Object == null) return;
        item.Object.SetActive(false); item.Follow = null;
        pool[item.Prefab].Push(item);
    }

    IEnumerator Burst(CombatEffectStage stage, Vector3 point, float size = 1, Quaternion? orientation = null, Vector3? visibilityPosition = null)
    {
        if (stage?.Prefab == null) yield break;
        if (stage.Delay > 0) yield return new WaitForSecondsRealtime(stage.Delay);
        Rent(stage, point, orientation ?? Quaternion.identity, size: size, visibilityPosition: visibilityPosition);
    }

    internal IEnumerator Cast(CombatVisualSequence sequence)
    {
        var profile = sequence.Profile;
        var actor = sequence.Actor;
        var binding = actor != null ? actor.GetComponent<CharacterCombatEffects>() : null;
        var origin = binding?.CastSocket != null ? binding.CastSocket.position : sequence.Origin;
        if (actor != null) actor.PlayAttackAnimation();
        var facing = sequence.Center - origin;
        StartCoroutine(Burst(profile.Muzzle, origin, orientation: facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing, Vector3.back) : Quaternion.identity));
        StartCoroutine(Burst(profile.GroundCircle, Ground(sequence.Origin)));
        yield return new WaitForSecondsRealtime(Mathf.Max(profile.CastSeconds, Mathf.Max(profile.Muzzle.Delay, profile.GroundCircle.Delay)));
        if (actor != null) actor.PlayIdleAnimation();
    }

    internal IEnumerator Deliver(CombatVisualSequence sequence, Vector3 destination, bool hit, Bounds? targetBounds = null)
    {
        var profile = sequence.Profile;
        var stage = profile.Projectile;
        if (stage?.Prefab != null && (!sequence.SingleFlight || !sequence.FlightPlayed))
        {
            sequence.FlightPlayed = true;
            var flightDestination = sequence.SingleFlight ? sequence.Center : destination;
            if (stage.Delay > 0) yield return new WaitForSecondsRealtime(stage.Delay);
            var delta = flightDestination - sequence.Origin;
            var rotation = delta.sqrMagnitude > .001f ? Quaternion.LookRotation(delta, Vector3.back) : Quaternion.identity;
            var projectile = Rent(stage, sequence.Origin, rotation, true);
            int nextImpact = 0;
            var contacts = sequence.FlightImpacts.OrderBy(i => Vector3.Dot(i.point - sequence.Origin, delta)).ToList();
            float duration = Mathf.Clamp(delta.magnitude / Mathf.Max(.1f, profile.ProjectileSpeed),
                Mathf.Max(.01f, profile.FlightSeconds.x), Mathf.Max(.01f, profile.FlightSeconds.y));
            try
            {
                for (float elapsed = 0; elapsed < duration && projectile != null; elapsed += Time.unscaledDeltaTime)
                {
                    projectile.transform.position = Vector3.Lerp(sequence.Origin, flightDestination, elapsed / duration) + stage.Offset;
                    while (nextImpact < contacts.Count && Vector3.Dot(contacts[nextImpact].point - sequence.Origin, delta) <= delta.sqrMagnitude * elapsed / duration)
                    { var contact = contacts[nextImpact++]; if (contact.hit) StartCoroutine(Burst(profile.Impact, contact.point, visibilityPosition: contact.point)); }
                    yield return null;
                }
            }
            finally { Release(projectile); }
            while (nextImpact < contacts.Count) { var contact = contacts[nextImpact++]; if (contact.hit) StartCoroutine(Burst(profile.Impact, contact.point, visibilityPosition: contact.point)); }
        }
        if (hit && !(sequence.ContinuousFlight && stage?.Prefab != null))
        {
            var point = destination; float size = 1;
            if (profile.Impact.FitToTarget && targetBounds.HasValue)
            {
                var camera = Camera.main;
                float cellSize = Mathf.Abs(Game.Instance.CurrentDungeon.CellToWorld(Vector3Int.right).x - Game.Instance.CurrentDungeon.CellToWorld(Vector3Int.zero).x);
                (point, size) = FitImpact(targetBounds.Value, camera != null ? camera.transform.forward : Vector3.forward,
                    camera != null ? camera.transform.right : Vector3.right, camera != null ? camera.transform.up : Vector3.up, cellSize);
            }
            // Impacts authored with a ground footprint (circles, portals, runes) still grow with
            // the target but stay on the dungeon floor instead of floating in front of its body.
            if (profile.Impact.MinimumDiameterCells > 0) point = Ground(destination);
            StartCoroutine(Burst(profile.Impact, point, size, visibilityPosition: destination));
        }
        if (!sequence.AreaPlayed && profile.Area.Prefab != null)
        {
            sequence.AreaPlayed = true;
            AudioManager.Instance?.PlaySoundEffect(profile.AreaSound);
            StartCoroutine(Burst(profile.Area, Ground(sequence.Center), profile.ScaleAreaToRadius ? Mathf.Max(1, sequence.Radius * 2 + 1) : 1));
        }
        yield return new WaitForSecondsRealtime(Mathf.Max(profile.ImpactSeconds, Mathf.Max(profile.Impact.Delay, profile.Area.Delay)));
    }

    internal void SetStatuses(Character character, Dictionary<string, StatusVisualProfile> snapshot)
    {
        if (character == null) return;
        statuses[character] = snapshot;
        Reconcile(character);
    }

    void Reconcile(Character character)
    {
        if (!auras.TryGetValue(character, out var existing)) auras[character] = existing = new();
        var desired = statuses[character].Values.Where(p => p != null).Distinct()
            .OrderByDescending(p => p.Priority).ThenBy(p => p.name).Take(3).ToList();
        foreach (var pair in existing.ToArray())
            if (!desired.Contains(pair.Key)) { Release(pair.Value); existing.Remove(pair.Key); }
        foreach (var profile in desired)
            if (!existing.ContainsKey(profile))
            {
                var anchor = character.VisualParent != null ? character.VisualParent.transform : character.transform;
                existing[profile] = Rent(profile.Aura, Ground(Body(character, Game.Instance.CurrentDungeon.WorldToCell(character.transform.position))), Quaternion.identity, true, follow: anchor);
            }
    }

    void LateUpdate()
    {
        var current = Game.Instance != null && Game.Instance.CurrentDungeon != null ? Game.Instance.CurrentDungeon : null;
        if (!ReferenceEquals(dungeon, current)) { if (hadDungeon) Clear(); dungeon = current; hadDungeon = current != null; }
        foreach (var c in casting.Keys.ToArray()) if (c == null || c.Vitals.HP <= 0) ClearCasting(c);
        foreach (var character in statuses.Keys.ToArray())
            if (character == null) { if (auras.TryGetValue(character, out var effects)) foreach (var effect in effects.Values) Release(effect); auras.Remove(character); statuses.Remove(character); }
        foreach (var item in active.ToArray())
        {
            if (item.Object == null || Time.unscaledTime >= item.Expires) { Release(item.Object); continue; }
            if (item.Follow != null) item.Object.transform.position = item.Follow.position + item.Offset;
            SetVisibility(item);
        }
    }

    static void SetVisibility(Instance item)
    {
        var fog = FogOverlay.Instance;
        bool hidden = fog != null && !fog.IsCurrentlyVisible(item.VisibilityPosition ?? item.Object.transform.position, FootPrint.Size1x1);
        foreach (var renderer in item.Renderers) if (renderer != null) renderer.forceRenderingOff = hidden;
    }
    public void Clear()
    {
        StopAllCoroutines();
        foreach (var item in active) if (item.Object != null) Destroy(item.Object);
        foreach (var stack in pool.Values) foreach (var item in stack) if (item.Object != null) Destroy(item.Object);
        active.Clear(); pool.Clear(); statuses.Clear(); auras.Clear(); casting.Clear();
    }
    void OnDisable() => Clear();
}
