using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using TMPro;
using UnityEngine;

/// <summary>Cardinal visual-road graph. No capability queries, discoveries, or navigation changes.</summary>
public sealed class BiomeRoadGraph
{
    public readonly Dictionary<Vector2Int,HashSet<Vector2Int>> Edges=new();
    public void Add(Vector2Int p){if(!Edges.ContainsKey(p))Edges[p]=new();}
    public void Connect(Vector2Int a,Vector2Int b){if(Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y)!=1)return;Add(a);Add(b);Edges[a].Add(b);Edges[b].Add(a);}
    public List<HashSet<Vector2Int>> Junctions()
    {
        var pending=new HashSet<Vector2Int>(Edges.Where(p=>p.Value.Count>=3).Select(p=>p.Key));var result=new List<HashSet<Vector2Int>>();
        while(pending.Count>0){var first=pending.OrderBy(p=>p.x).ThenBy(p=>p.y).First();var group=new HashSet<Vector2Int>{first};var queue=new Queue<Vector2Int>();queue.Enqueue(first);pending.Remove(first);
            while(queue.Count>0)foreach(var next in Edges[queue.Dequeue()])if(pending.Remove(next)){group.Add(next);queue.Enqueue(next);}result.Add(group);}
        return result;
    }
    public (string town,int distance) Nearest(Vector2Int first,ISet<Vector2Int> blocked,IReadOnlyDictionary<Vector2Int,string> towns)
    {
        var seen=new HashSet<Vector2Int>(blocked);var queue=new Queue<(Vector2Int p,int distance)>();if(!seen.Add(first))return(null,0);queue.Enqueue((first,1));
        string best=null;int distance=int.MaxValue;
        while(queue.Count>0){var current=queue.Dequeue();if(current.distance>distance)break;
            if(towns.TryGetValue(current.p,out var id)){if(best==null||string.CompareOrdinal(id,best)<0){best=id;distance=current.distance;}continue;}
            if(Edges.TryGetValue(current.p,out var edges))foreach(var next in edges.OrderBy(p=>p.x).ThenBy(p=>p.y))if(seen.Add(next))queue.Enqueue((next,current.distance+1));}
        return(best,distance);
    }
    public static BiomeRoadGraph From(OverworldGrid grid)
    {
        var graph=new BiomeRoadGraph();var roads=grid.Layers[OverworldLayers.Roads];
        for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)if(roads[x,y])graph.Add(new Vector2Int(x,y));
        foreach(var p in graph.Edges.Keys.ToArray())foreach(var d in BiomeDecorations.Directions)if(graph.Edges.ContainsKey(p+d))graph.Connect(p,p+d);
        var warps=new HashSet<string>(grid.Warps.Select(w=>w.Id));
        foreach(var route in grid.Routes.Where(r=>!warps.Contains(r.Key)))for(int i=1;i<route.Value.Count;i++) {
            var a=route.Value[i-1];var b=route.Value[i];graph.Connect(new Vector2Int(a.X,a.Y),new Vector2Int(b.X,b.Y));
        }
        return graph;
    }
}

public static class BiomeRoadSigns
{
    public sealed class Branch {public string TownId;public Vector2Int Direction,FirstCell;public int Distance;}
    public sealed class Sign {public Vector2Int Road,Verge;public OverworldBiome Biome;public Branch[] Branches;public int Priority;public HashSet<Vector2Int> JunctionCells;}
    public static List<Sign> Plan(OverworldGrid grid)
    {
        var graph=BiomeRoadGraph.From(grid);var towns=grid.TownFootprints.ToDictionary(t=>new Vector2Int(t.Entrance.X,t.Entrance.Y),t=>t.LocationId);
        var candidates=new List<(Vector2Int p,HashSet<Vector2Int> group,int priority,string approach)>();
        foreach(var town in towns.OrderBy(t=>t.Value,StringComparer.Ordinal)) {
            var seen=new HashSet<Vector2Int>{town.Key};var queue=new Queue<(Vector2Int p,int d)>();queue.Enqueue((town.Key,0));
            while(queue.Count>0){var cur=queue.Dequeue();if(cur.d>=3&&cur.d<=5)candidates.Add((cur.p,new HashSet<Vector2Int>{cur.p},0,town.Value));
                if(cur.d>=5||!graph.Edges.TryGetValue(cur.p,out var edges))continue;foreach(var next in edges.OrderBy(p=>p.x).ThenBy(p=>p.y))if(seen.Add(next))queue.Enqueue((next,cur.d+1));}
        }
        var junctions=graph.Junctions();var junctionCells=new HashSet<Vector2Int>(junctions.SelectMany(g=>g));
        foreach(var group in junctions)candidates.Add((group.OrderBy(p=>p.x).ThenBy(p=>p.y).First(),group,1,null));
        // Traverse each uninterrupted segment once. Stable orientation makes bends and loops repeatable.
        var visitedEdges=new HashSet<(Vector2Int,Vector2Int)>();
        foreach(var start in graph.Edges.Keys.OrderBy(p=>p.x).ThenBy(p=>p.y))foreach(var first in graph.Edges[start].OrderBy(p=>p.x).ThenBy(p=>p.y)) {
            var prev=start;var current=first;int distance=0;
            while(visitedEdges.Add((prev,current))) {
                visitedEdges.Add((current,prev));distance++;
                if(distance%12==0&&!junctionCells.Contains(current))candidates.Add((current,new HashSet<Vector2Int>{current},2,null));
                if(graph.Edges[current].Count!=2)break;
                var next=graph.Edges[current].First(p=>p!=prev);prev=current;current=next;
            }
        }
        var result=new List<Sign>();var chunks=new Dictionary<Vector2Int,int>();var approaches=new HashSet<string>();
        bool At(string layer,Vector2Int p)=>grid.Layers.TryGetValue(layer,out var mask)&&mask.At(new GridPoint(p.x,p.y));
        bool Supported(Vector2Int p) {
            if(p.x<1||p.y<1||p.x>=grid.Width-1||p.y>=grid.Height-1 || graph.Edges.ContainsKey(p)||!At(OverworldLayers.Ground,p))return false;
            foreach(var layer in new[]{OverworldLayers.Water,OverworldLayers.Bridges,OverworldLayers.TownFootprints,OverworldLayers.Mountains,OverworldLayers.Trees,OverworldLayers.Locks})if(At(layer,p))return false;
            if(grid.Locations.Values.Any(q=>Mathf.Max(Math.Abs(q.X-p.x),Math.Abs(q.Y-p.y))<2))return false;
            if(grid.Locks.Any(l=>l.Cells.Any(q=>Mathf.Max(Math.Abs(q.X-p.x),Math.Abs(q.Y-p.y))<=2)))return false;
            return true;
        }
        foreach(var c in candidates.OrderBy(c=>c.priority).ThenBy(c=>BiomeDecorationPlacement.Hash(grid.CampaignSeed,"signs",c.p.x+","+c.p.y)).ThenBy(c=>c.p.x).ThenBy(c=>c.p.y)) {
            if(c.approach!=null&&approaches.Contains(c.approach))continue;
            var exits=c.group.OrderBy(p=>p.x).ThenBy(p=>p.y).SelectMany(p=>graph.Edges[p].Where(n=>!c.group.Contains(n)).Select(n=>(from:p,to:n))).ToArray();
            var branches=new List<Branch>();
            foreach(var exit in exits){var nearest=graph.Nearest(exit.to,c.group,towns);if(nearest.town!=null)branches.Add(new Branch{TownId=nearest.town,Distance=nearest.distance,Direction=exit.to-exit.from,FirstCell=exit.to});}
            var selected=branches.OrderBy(b=>b.Distance).ThenBy(b=>b.TownId,StringComparer.Ordinal).GroupBy(b=>b.TownId).Select(g=>g.First()).Take(3).ToArray();
            if(selected.Length==0)continue;
            foreach(var verge in c.group.OrderBy(p=>p.x).ThenBy(p=>p.y).SelectMany(p=>BiomeDecorations.Directions.Select(d=>p+d)).Distinct()) {
                if(!Supported(verge)||result.Any(s=>(s.Verge-verge).sqrMagnitude<36))continue;
                var chunk=new Vector2Int(verge.x/32,verge.y/32);chunks.TryGetValue(chunk,out int count);if(count>=8)continue;
                var biome=c.approach!=null?OverworldCosmetics.Biome(grid,towns.First(t=>t.Value==c.approach).Key.x,towns.First(t=>t.Value==c.approach).Key.y):OverworldCosmetics.Biome(grid,verge.x,verge.y);
                result.Add(new Sign{Road=c.p,Verge=verge,Biome=biome,Branches=selected,Priority=c.priority,JunctionCells=c.group});chunks[chunk]=count+1;if(c.approach!=null)approaches.Add(c.approach);break;
            }
        }
        return result;
    }
    public static void Build(OverworldGrid grid,Transform parent,BiomeDecorationCatalog catalog,float size)
    {
        var common=UnityEngine.Object.FindFirstObjectByType<Common>();
        var context=common?.CampaignContext;
        if(context==null||context.State.Seed!=grid.CampaignSeed)context=new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Sandbox,grid.CampaignSeed));
        foreach(var sign in Plan(grid)) {
            var root=new GameObject("Road sign").transform;root.SetParent(parent,false);root.localPosition=new Vector3(sign.Verge.x+.5f,sign.Verge.y+.5f,0)*size;
            var batch=new EnvironmentBatch(root);float scale=size*.8f;
            BiomeDecorationPlacement.Add(batch,root,catalog,catalog.Get(sign.Biome,BiomeDecorationKind.SignPost),Vector3.zero,scale,0,false);
            for(int i=0;i<sign.Branches.Length;i++) {
                var branch=sign.Branches[i];float z=-(1.15f-i*.35f)*scale;
                float angle=Mathf.Atan2(branch.Direction.y,branch.Direction.x)*Mathf.Rad2Deg;
                // Tilt the writing face upward: north/south boards otherwise become edge-on
                // to the production camera. The long arrow axis still follows the road branch.
                var rotation=Quaternion.Euler(0,0,angle)*Quaternion.Euler(55,0,0);
                BiomeDecorationPlacement.Add(batch,root,catalog,catalog.Get(sign.Biome,BiomeDecorationKind.SignPanel),new Vector3(0,0,z),scale,rotation,false);
                var label=new GameObject("Destination "+branch.TownId).AddComponent<TextMeshPro>();label.transform.SetParent(root,false);
                label.transform.localPosition=new Vector3(0,-.08f*scale,z-.02f);label.text=context.GetTownDisplayName(branch.TownId);
                label.fontSizeMin=.5f;label.fontSizeMax=1.6f;label.enableAutoSizing=true;label.textWrappingMode=TextWrappingModes.Normal;
                label.overflowMode=TextOverflowModes.Overflow;label.alignment=TextAlignmentOptions.Center;label.color=new Color(1,.96f,.83f);
                label.outlineColor=Color.black;label.outlineWidth=.18f;
                label.rectTransform.sizeDelta=new Vector2(.95f*scale,.20f*scale);
                var facing=label.gameObject.AddComponent<BiomeSignLabel>();facing.DisplayName=label.text;facing.Direction=new Vector3(branch.Direction.x,branch.Direction.y,0);facing.CameraOffset=scale*1.4f;
            }
            batch.Finish();
        }
    }
}
