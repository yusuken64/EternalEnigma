using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
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
    // The first road step gives the posted direction. Stable compass order breaks ties in loops.
    public Dictionary<Vector2Int, (Vector2Int direction, int distance)> RoutesFrom(Vector2Int road)
    {
        var paths = new Dictionary<Vector2Int, (Vector2Int direction, int distance)> { [road] = (Vector2Int.zero, 0) };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(road);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!Edges.TryGetValue(current, out var edges)) continue;
            var path = paths[current];
            foreach (var direction in BiomeDecorations.Directions)
            {
                var next = current + direction;
                if (!edges.Contains(next) || paths.ContainsKey(next)) continue;
                paths.Add(next, (current == road ? direction : path.direction, path.distance + 1));
                queue.Enqueue(next);
            }
        }
        return paths;
    }

    public static BiomeRoadGraph From(OverworldGrid grid)
    {
        var graph=new BiomeRoadGraph();var roads=grid.Layers[OverworldLayers.Roads];
        for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)if(roads[x,y])graph.Add(new Vector2Int(x,y));
        foreach(var p in graph.Edges.Keys.ToArray())foreach(var d in BiomeDecorations.Directions)if(graph.Edges.ContainsKey(p+d))graph.Connect(p,p+d);
        return graph;
    }
}

public static class BiomeRoadSigns
{
    public sealed class Branch {public string TownId;public Vector2Int Direction;public int Distance;}
    public sealed class Sign {public Vector2Int Road,Verge;public OverworldBiome Biome;public Branch[] Branches;public int Priority;public Dictionary<string, (Vector2Int direction, int distance)> Routes;}

    private static string SignName(CampaignContext context, CampaignLocation location)
    {
        if (location.Kind == LocationKind.Town) return context.GetTownDisplayName(location.Id);
        if (location.Encounter != null)
            return location.Encounter.Kind switch
            {
                EncounterKind.Herding => "Herding grounds",
                EncounterKind.Lure => "Trapping grounds",
                EncounterKind.Sokoban => "Boulder chamber",
                EncounterKind.LightPattern => "Light chamber",
                EncounterKind.Ambush => "Ambush site",
                _ => location.Encounter.DisplayName
            };
        if (location.Variant == DungeonVariant.Town) return "Hostile town district";
        string name = CampaignGuidance.LocationName(context, location.Id);
        if (name.StartsWith("the ", System.StringComparison.Ordinal)) name = name.Substring(4);
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    internal static string Compass(Vector2Int d) => d.x > 0 ? "East" : d.x < 0 ? "West" : d.y > 0 ? "North" : "South";

    public static string Directions(CampaignContext context, Sign sign)
    {
        var entries = new List<(bool Town, string Id, int Distance, string Text)>();
        foreach (var location in context.Campaign.Locations.Where(l => l.ParentTownId == null))
        {
            if (!sign.Routes.TryGetValue(location.Id, out var route)) continue;
            bool town = location.Kind == LocationKind.Town;
            string name = SignName(context, location);
            entries.Add((town, location.Id, route.distance, $"{Compass(route.direction)} — {name}"));
        }
        var sorted = entries.OrderBy(e => e.Distance).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
        var selected = sorted.Where(e => e.Town).Take(2).Concat(sorted.Where(e => !e.Town).Take(3)).OrderBy(e => e.Distance);
        return "Road sign\n\n" + string.Join("\n", selected.Select(e => e.Text));
    }
    public static List<Sign> Plan(OverworldGrid grid)
    {
        var graph=BiomeRoadGraph.From(grid);var towns=grid.TownFootprints.Where(t=>!t.IsHostile).ToDictionary(t=>new Vector2Int(t.Entrance.X,t.Entrance.Y),t=>t.LocationId);
        var candidates=new List<(Vector2Int p,HashSet<Vector2Int> roadCells,int priority,string approach)>();
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
            var verges = from road in c.roadCells.OrderBy(p => p.x).ThenBy(p => p.y)
                         from direction in BiomeDecorations.Directions
                         select (road, verge: road + direction);
            foreach (var (road, verge) in verges)
            {
                if (!Supported(verge) || result.Any(sign => (sign.Verge - verge).sqrMagnitude < 36)) continue;
                if (BiomeDecorations.Directions.Any(d => verge + d != road && graph.Edges.ContainsKey(verge + d))) continue;
                var chunk = new Vector2Int(verge.x / 32, verge.y / 32);
                chunks.TryGetValue(chunk, out int count);
                if (count >= 8) continue;
                var paths = graph.RoutesFrom(road);
                var routes = new Dictionary<string, (Vector2Int direction, int distance)>();
                foreach (var location in grid.Locations)
                {
                    var cell = new Vector2Int(location.Value.X, location.Value.Y);
                    if (paths.TryGetValue(cell, out var path) && path.distance > 0) routes.Add(location.Key, path);
                }
                var selected = towns.Values.Where(routes.ContainsKey)
                    .OrderBy(id => routes[id].distance).ThenBy(id => id, StringComparer.Ordinal).Take(2)
                    .Select(id => new Branch { TownId = id, Direction = routes[id].direction, Distance = routes[id].distance }).ToArray();
                if (selected.Length == 0) continue;
                var biomeCell = c.approach != null ? towns.First(t => t.Value == c.approach).Key : verge;
                result.Add(new Sign { Road = road, Verge = verge, Biome = OverworldCosmetics.Biome(grid, biomeCell.x, biomeCell.y),
                    Branches = selected, Priority = c.priority, Routes = routes });
                chunks[chunk] = count + 1;
                if (c.approach != null) approaches.Add(c.approach);
                break;
            }
        }
        return result;
    }
    internal const string ObjectName = "Road sign";

    public static void Build(OverworldGrid grid, Transform parent, BiomeDecorationCatalog decorations, float size)
    {
        var diorama = DioramaCatalog.Load();
        foreach (var sign in Plan(grid))
        {
            var root = new GameObject(ObjectName).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(sign.Verge.x + .5f, sign.Verge.y + .5f, 0) * size;
            var batch = new EnvironmentBatch(root);
            if (diorama != null)
                diorama.Add(batch, "Signboard", sign.Biome, Vector3.zero);
            else
            {
                var post = decorations.Get(sign.Biome, BiomeDecorationKind.SignPost);
                float scale = size * post.OverworldScale;
                BiomeDecorationPlacement.Add(batch, root, decorations, post, Vector3.zero, scale, 0, false);
                BiomeDecorationPlacement.Add(batch, root, decorations, decorations.Get(sign.Biome, BiomeDecorationKind.SignPanel),
                    Vector3.back * scale, scale, Quaternion.Euler(55, 0, 0), false);
            }
            batch.Finish();
        }
    }
}
