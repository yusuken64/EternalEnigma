using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EternalEnigma.Core.Progression;
using UnityEngine;

/// <summary>Town conversations read live party and route state whenever the player asks a question.</summary>
internal sealed class TownGuide
{
    // Retain only the few slot rolls per visited town. A new or restored campaign gets its own cache.
    private static readonly ConditionalWeakTable<CampaignContext, Dictionary<string, int[]>> campaignRolls = new();
    private readonly Dictionary<string, int[]> townRolls;
    private readonly Town town;
    private readonly CampaignRoute firstRoadGate;
    private TownAlly gateRecruit;
    private CampaignContext Context => Common.Instance.CampaignContext;
    private GameSaveData Save => Common.Instance.GameSaveData;
    private TownPlayer Party => town.TownPlayer;
    internal TownNpc Mochi { get; }

    internal TownGuide(Town town)
    {
        this.town = town;
        Mochi = town.Townsfolk.FirstOrDefault(npc => npc.Definition.Id == "Cat");
        if (Context == null || Context.IsSandbox) return;
        if (town.Configuration.Id == Context.Campaign.StartLocationId)
            firstRoadGate = Context.Campaign.Routes.Single(r => r.Id == "starter-exit");
        townRolls = campaignRolls.GetOrCreateValue(Context);
        townRolls[town.Configuration.Id] = town.Plan.AllySlots.Select(slot => slot.Roll).ToArray();
    }

    internal bool ShowMochiPrompt()
    {
        if (Mochi == null || Party.ControllingTownAlly == null || Context?.IsPreparingForOpening != true || Save.MetMochi ||
            town.Configuration.Id != Context.Campaign.StartLocationId || town.HomeLesson?.IsLocked == true) return false;
        var cell = Party.ControllingTownAlly.TilemapPosition;
        return (Mochi.Cell - cell).sqrMagnitude <= 36 &&
            !town.Plan.Layers[EternalEnigma.Core.World.TownLayers.ShopFloor].At(cell.ToGridPoint());
    }

    internal void TalkToMochi(TownNpc speaker)
    {
        Save.MetMochi = true;
        void Say(string line) => Speak(speaker.Definition.DisplayName, line);
        TownMenu.ShowTopics("What do you want to know?", new List<(string, Action)>
        {
            ("Where do I go next?", () => Say(Directions(speaker.Cell))),
            ("Where can I recruit someone?", () =>
            {
                var classes = ClassCatalog.Load().Classes.OrderBy(c => c.DisplayName, StringComparer.Ordinal);
                TownMenu.ShowTopics("Which class are you looking for?",
                    classes.Select(c => (c.DisplayName, (Action)(() => Say(RecruitHint(c))))).ToList(), "Back to my questions");
            }),
            ("What should I do before I leave?", () => Say(PreparationHint()))
        });
    }

    internal string Directions(Vector3Int from)
    {
        var context = Context;
        if (context == null) return "Try the dungeon entrance here in town when you're ready for an adventure.";
        if (town.Configuration.Id == context.Campaign.StartLocationId)
        {
            if (context.IsPreparingForOpening)
            {
                if (town.HomeLesson?.IsLocked == true) return context.OpeningObjective;
                if (!context.State.BoughtOpeningBread)
                    return BuildingHint("bakery", from, "The baker has a free loaf set aside for you.");
                if (!context.State.LearnedOpeningSkill)
                    return BuildingHint("trainer", from, "The trainer can teach you your first active skill.");
                return BuildingHint("home", from, "Sleep in your own bed. The town dungeon opens in the morning.");
            }
            if (!context.Completed.Contains("story-0"))
                return BuildingHint("entrance", from, "The way out of town opens when you clear the town dungeon.");
            if (FirstTripAhead)
            {
                string place = CampaignGuidance.LocationName(context, firstRoadGate.KeyLocationId);
                string key = context.Campaign.KeyLabel(firstRoadGate.KeyId);
                return $"Go through the town gate and take the east road to {place}. Clear that dungeon to earn the {key} for the outer gate.";
            }
        }
        string hint = CampaignGuidance.TownHint(context, town.Configuration.Id,
            id => CampaignParty.Resolve(id, town.Configuration)?.Name ?? id);
        return string.IsNullOrEmpty(hint) ? "The roads are open. Is there somewhere you'd like to explore again?" : hint;
    }

    private string BuildingHint(string id, Vector3Int from, string purpose)
    {
        var building = town.TownBuildings.FirstOrDefault(b => b.Definition.Id == id || b.Definition.DialogId == id);
        return building == null ? purpose : $"{purpose} Look {Bearing(from, building.TilemapPosition)} for {Landmark(building)}.";
    }

    internal string RecruitHint(ClassDefinition wanted)
    {
        bool Matches(TownAlly ally) => ally.PrimaryClass == wanted || ally.SecondaryClass == wanted;
        var local = town.TownAllies.Where(Matches).OrderBy(a => a.RecruitCost).FirstOrDefault();
        if (local != null) return LocalOffer(local);
        if (townRolls != null)
        {
            foreach (var id in Context.Towns.Where(id => id != town.Configuration.Id).OrderBy(id => id, StringComparer.Ordinal))
            {
                // A resumed game can reconstruct the exact offer rolls from its versioned town plan.
                if (!townRolls.TryGetValue(id, out var rolls))
                    townRolls[id] = rolls = Context.Town(id).AllySlots.Select(s => s.Roll).ToArray();
                var offer = town.TownAllyManager.SelectOffers(rolls, Context.Roster.Append(Save.ProtagonistId))
                    .FirstOrDefault(o => Matches(o.Ally));
                if (offer != null) return $"{offer.Ally.Name} is taking work in {Context.GetTownDisplayName(id)} for {offer.Cost} gold.";
            }
        }
        var member = Party.RecruitedAllies.FirstOrDefault(Matches);
        if (member != null) return $"{member.Name} already travels with you as a {wanted.DisplayName}.";
        return $"I haven't met a {wanted.DisplayName} looking for work in the towns we've visited. Ask me again after your next stop.";
    }

    internal string PreparationHint()
    {
        foreach (var ally in Party.RecruitedAllies)
        {
            if (ally.Hp < 0 && ally.Mana < 0 && !ally.HasHunger) continue;
            var stats = TownUtilityService.StatsFor(ally);
            if (ally.Hp >= 0 && ally.Hp < stats.HPMax || ally.Mana >= 0 && ally.Mana < stats.ManaMax ||
                ally.HasHunger && ally.Hunger < stats.HungerMax)
                return $"Take {ally.Name} to the inn before you set out. The innkeeper will restore the whole party for free.";
        }
        if (Party.RecruitedAllies.Count == 1)
        {
            var hire = town.TownAllies.Where(a => a.RecruitCost <= Party.Gold).OrderBy(a => a.RecruitCost).FirstOrDefault();
            if (hire != null) return $"You can afford company on this trip. {LocalOffer(hire)}";
        }
        if (Context?.IsPreparingForOpening == true) return Directions(Party.ControllingTownAlly.TilemapPosition);
        if (!Party.Inventory.Any(i => i.ItemName == "Bread")) return "Pack some bread from the bakery for the next descent.";
        return "Save at the inn before you leave. If your party falls, that's where you can start again.";
    }

    private string LocalOffer(TownAlly ally)
    {
        string landmark;
        if (NearGate(ally.TilemapPosition, 2)) landmark = "beside the town gate";
        else
        {
            var nearest = town.TownBuildings.OrderBy(b => (b.TilemapPosition - ally.TilemapPosition).sqrMagnitude).First();
            landmark = $"{Bearing(nearest.TilemapPosition, ally.TilemapPosition)} of {Landmark(nearest)}";
        }
        return $"{ally.Name} is {landmark}. Their fee is {ally.RecruitCost} gold.";
    }

    private static string Landmark(TownBuilding building) => building.Definition.Id == "home" ? "your house" : "the " + building.Name;
    private static string Bearing(Vector3Int from, Vector3Int to) => CampaignGuidance.Bearing(from.ToGridPoint(), to.ToGridPoint());
    private static void Speak(string name, string line) => TownMenu.ShowMessage(name + "\n\n" + line);

    private bool FirstTripAhead => firstRoadGate != null && !Context.State.Finished && Context.Completed.Contains("story-0") &&
        !Context.Keys.Contains(firstRoadGate.KeyId) && !Context.Opened.Contains(firstRoadGate.Id);

    private bool NearGate(Vector3Int position, int distance)
    {
        var offset = position - town.Plan.Exit.ToCell();
        return Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) <= distance;
    }

    internal void PlaceGateRecruit()
    {
        gateRecruit = null;
        if (!FirstTripAhead || Party.RecruitedAllies.Count != 1) return;
        var recruit = town.TownAllies.FirstOrDefault();
        if (recruit == null) return;
        var exit = town.Plan.Exit.ToCell();
        foreach (var side in new[] { Vector3Int.right, Vector3Int.left })
        foreach (var cell in new[] { exit + side + Vector3Int.up, exit + side })
        {
            if (!town.CanEnter(cell) || Party.RecruitedAllies.Any(a => a.TilemapPosition == cell)) continue;
            recruit.TilemapPosition = cell;
            recruit.transform.position = town.WalkableMap.CellToWorld(cell);
            gateRecruit = recruit;
            return;
        }
    }

    private TownAlly GateSpeaker()
    {
        if (!FirstTripAhead || !NearGate(Party.ControllingTownAlly.TilemapPosition, 3)) return null;
        return Party.RecruitedAllies.FirstOrDefault(a => a.Id != Save.ProtagonistId) ?? gateRecruit;
    }

    internal bool IntroduceGateSpeaker()
    {
        if (Save.HeardOpeningGateInvitation || AutoplayRunner.BlocksPlayerInput) return false;
        var speaker = GateSpeaker();
        if (speaker == null) return false;
        Save.HeardOpeningGateInvitation = true;
        string invitation = town.TownAllies.Contains(speaker) ? "Want company? Come talk to me before you go." :
            "Let's see what's down there together.";
        Speak(speaker.Name, Directions(speaker.TilemapPosition) + " " + invitation);
        return true;
    }

    internal bool TalkToGateSpeaker(TownAlly ally)
    {
        if (ally != GateSpeaker()) return false;
        Save.HeardOpeningGateInvitation = true;
        bool hiring = town.TownAllies.Contains(ally);
        PartyMenuPicker topics = null;
        topics = TownMenu.ShowTopics("What do you want to know?", new List<(string, Action)>
        {
            ("Where are we headed?", () => Speak(ally.Name, Directions(ally.TilemapPosition))),
            (hiring ? "Want to come with me?" : "Manage party", () =>
            {
                topics.CloseDialog();
                Party.OpenAllyDialog(ally, hiring ? AllyRecruitDialogMode.Recruit : AllyRecruitDialogMode.Talk);
            })
        });
        return true;
    }
}
