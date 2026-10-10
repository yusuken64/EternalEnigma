using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TWC.Actions;
using UnityEngine;

public enum OverworldLayerRole { CoreMask, DerivedTerrain, Cosmetic, PlannedCosmetic }

[Serializable]
public sealed class OverworldStyleBinding
{
    public string BlueprintGuid;
    public string CoreLayer;
    public OverworldLayerRole Role;
    public bool AllowEmpty = true;
    [Tooltip("Optional stable presentation projection from SmartEnvironmentMasks; independent of the blueprint display name.")]
    public string Projection;
}

/// <summary>Presentation only. Inheritance overrides bindings by stable TWC GUID, never by display name.</summary>
[CreateAssetMenu(menuName = "Game/Art/Overworld Style Profile")]
public sealed class OverworldStyleProfile : ScriptableObject
{
    public OverworldStyleProfile BaseProfile;
    public TileWorldCreatorAsset Template;
    public EnvironmentKit CosmeticKit;
    public PaintedGroundStyle GroundStyle;
    public List<OverworldStyleBinding> Layers = new();
    [Tooltip("Additional artist assets read by custom derivations; included in player cache identity.")]
    public UnityEngine.Object[] Dependencies = Array.Empty<UnityEngine.Object>();

    public IEnumerable<OverworldStyleProfile> Composition()
    {
        var seen = new HashSet<OverworldStyleProfile>();
        for(var profile=this;profile!=null;profile=profile.BaseProfile)
        {
            if(!seen.Add(profile))throw new InvalidOperationException("Overworld style inheritance contains a cycle.");
            yield return profile;
        }
    }
    public TileWorldCreatorAsset ResolveTemplate() => Composition().Select(p=>p.Template).FirstOrDefault(a=>a!=null);
    public EnvironmentKit ResolveKit() => Composition().Select(p=>p.CosmeticKit).FirstOrDefault(a=>a!=null);
    public PaintedGroundStyle ResolveGround() => Composition().Select(p=>p.GroundStyle).FirstOrDefault(a=>a!=null);
    public IReadOnlyList<OverworldStyleBinding> ResolveLayers()
    {
        var result=new Dictionary<Guid,OverworldStyleBinding>();
        foreach(var profile in Composition().Reverse())
        {
            var local=new HashSet<Guid>();
            foreach(var binding in profile.Layers)
            {
                if(binding==null || !Guid.TryParse(binding.BlueprintGuid,out var guid) || guid==Guid.Empty || !local.Add(guid))
                    throw new ArgumentException("Style bindings require unique, nonempty blueprint GUIDs in each profile.");
                result[guid]=binding;
            }
        }
        return result.Values.ToArray();
    }
    public List<CampaignLayerBinding> Bindings(OverworldGrid grid,TileWorldCreatorAsset fallback)
    {
        var template=ResolveTemplate()??fallback;
        Validate(grid,template);
        return ResolveLayers().Where(b=>b.Role==OverworldLayerRole.CoreMask).Select(b=>
            new CampaignLayerBinding(b.CoreLayer,template.mapBlueprintLayers.Single(l=>l.guid==Guid.Parse(b.BlueprintGuid)).layerName)).ToList();
    }
    public void Validate(OverworldGrid grid,TileWorldCreatorAsset template)
    {
        if(grid==null || template==null)throw new ArgumentException("An overworld style needs a grid and TWC template.");
        if(grid.Width<=0 || grid.Height<=0 || grid.Layers.Values.Any(l=>l.Width!=grid.Width || l.Height!=grid.Height))
            throw new ArgumentException("Core mask dimensions must match the overworld grid.");
        var layers=template.mapBlueprintLayers;
        if(layers.Any(l=>l==null || l.guid==Guid.Empty) || layers.Select(l=>l.guid).Distinct().Count()!=layers.Count)
            throw new ArgumentException("TWC blueprint GUIDs must be unique and nonempty.");
        var specs=ResolveLayers().ToDictionary(b=>Guid.Parse(b.BlueprintGuid));
        if(layers.Any(l=>l.active && !specs.ContainsKey(l.guid)))
            throw new ArgumentException("Every active blueprint requires an explicit style role.");
        foreach(var pair in specs)
        {
            var layer=layers.FirstOrDefault(l=>l.guid==pair.Key);
            if(layer==null || !layer.active)throw new ArgumentException("Style binding references a missing or disabled blueprint: "+pair.Key);
            if(pair.Value.Role==OverworldLayerRole.CoreMask && !grid.Layers.ContainsKey(pair.Value.CoreLayer??""))
                throw new ArgumentException("Unknown Core mask: "+pair.Value.CoreLayer);
            if(pair.Value.CoreLayer==OverworldLayers.Walkable && pair.Value.Role!=OverworldLayerRole.CoreMask)
                throw new ArgumentException("Movement authority cannot be assigned to an artist derivation.");
            if(!string.IsNullOrEmpty(pair.Value.Projection) &&
                !new[] {SmartEnvironmentMasks.Mountains,SmartEnvironmentMasks.Roads,SmartEnvironmentMasks.Walls,SmartEnvironmentMasks.Houses,SmartEnvironmentMasks.Coast}.Contains(pair.Value.Projection))
                throw new ArgumentException("Unknown presentation projection: "+pair.Value.Projection);
            if(pair.Value.CoreLayer==OverworldLayers.Walkable && !string.IsNullOrEmpty(pair.Value.Projection))
                throw new ArgumentException("Movement authority cannot use a presentation projection.");
        }
        for(int index=0;index<layers.Count;index++)
        {
            var layer=layers[index];
            if(!layer.active || !specs.TryGetValue(layer.guid,out var spec) || spec.Role==OverworldLayerRole.CoreMask)continue;
            foreach(var step in layer.stack)
            {
                if(step?.action==null)throw new ArgumentException("An artist derivation contains a missing action.");
                foreach(var field in step.action.GetType().GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic))
                {
                    if(field.FieldType!=typeof(Guid) || field.Name==nameof(TWCBlueprintAction.guid))continue;
                    var source=(Guid)field.GetValue(step.action);
                    if(!layers.Take(index).Any(l=>l.guid==source && l.active))
                        throw new ArgumentException("Artist derivations must reference an earlier active blueprint: "+layer.layerName+" / "+field.Name);
                }
            }
        }
        var ground=template.mapBuildLayers.OfType<OverworldGroundLayer>().Where(l=>l.active).ToArray();
        if(ground.Length!=1)throw new ArgumentException("A composed overworld style requires exactly one broad-ground owner.");
        if((ResolveGround()??ground[0].Style)==null)throw new ArgumentException("The broad-ground owner requires a ground style.");
        if(ground[0].Inputs.Any(i=>i==null || !specs.Values.Any(s=>s.Role==OverworldLayerRole.CoreMask && s.CoreLayer==i.CoreLayer)))
            throw new ArgumentException("Every ground input requires a bound Core mask.");
        foreach(var output in template.mapBuildLayers.Where(l=>l.active))
            if(output.assignedGenerationLayerGuid!=Guid.Empty && !layers.Any(l=>l.guid==output.assignedGenerationLayerGuid && l.active))
                throw new ArgumentException("Build layer references a missing or disabled blueprint: "+output.layerName);
    }
}

/// <summary>Applied after every artist cosmetic transform so approaches stay clear throughout the stack.</summary>
[Serializable]
public sealed class ProtectOverworldApproaches : TWCBlueprintAction, ITWCAction
{
    [TWC.OdinSerializer.OdinSerialize] private bool[,] protectedCells;
    public ProtectOverworldApproaches(bool[,] cells) { protectedCells=(bool[,])cells.Clone(); }
    public override bool ShowFoldout=>false;
    public float GetGUIHeight()=>0;
    public ITWCAction Clone()=>new ProtectOverworldApproaches(protectedCells);
    public bool[,] Execute(bool[,] map,TileWorldCreator creator)
    {
        if(map==null || map.GetLength(0)!=protectedCells.GetLength(0) || map.GetLength(1)!=protectedCells.GetLength(1))
            throw new InvalidOperationException("Cosmetic derivation changed the map dimensions.");
        for(int y=0;y<map.GetLength(1);y++)for(int x=0;x<map.GetLength(0);x++)if(protectedCells[x,y])map[x,y]=false;
        return map;
    }
}
