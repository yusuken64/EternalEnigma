using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Progression;

namespace EternalEnigma.Core.Generation;

/// <summary>One authored presentation of a lock. <c>{place}</c> is replaced with a generated place name.</summary>
public sealed class LockSkin
{
    public string Id { get; }
    public Capability Capability { get; }
    public LockForm Form { get; }
    /// <summary>Region themes this skin fits; empty means any theme.</summary>
    public IReadOnlyList<string> Themes { get; }
    public string Text { get; }
    public string Resolution { get; }
    public LockSkin(string id, Capability capability, LockForm form, IReadOnlyList<string> themes, string text, string resolution)
    { Id = id; Capability = capability; Form = form; Themes = themes; Text = text; Resolution = resolution; }
    public bool Fits(string theme) => Themes.Count == 0 || Themes.Contains(theme);
}

/// <summary>
/// The lock table (spec §19 item 6): fiction for every capability gate, keyed by capability × lock form × theme.
/// Append-only within a capability and form. Skin ids are stored on routes and hashed into the campaign fingerprint,
/// so reordering or editing an entry changes generated campaigns. Skin text never names the capability that opens the lock.
/// A capability can gate four or five locks in one campaign, so each natural form carries several skins that fit any
/// theme, and the generator prefers skins it has not yet used in that campaign.
/// </summary>
public static class LockSkinCatalog
{
    public const string TownGateKeySkin = "key.town-gate";
    public const string TownAreaKeySkin = "key.town-area";
    public const string WaystoneKeySkin = "key.waystone";

    public const string Highlands = "Highlands", Marsh = "Marsh", Coast = "Coast", Forest = "Forest",
        Desert = "Desert", Tundra = "Tundra", Ruins = "Ruins", Volcanic = "Volcanic";

    public static IReadOnlyList<LockSkin> Skins { get; } = BuildSkins();

    public static IReadOnlyList<LockSkin> For(Capability capability, LockForm form, string theme) =>
        Skins.Where(s => s.Capability == capability && s.Form == form && s.Fits(theme)).ToArray();
    public static IReadOnlyList<LockSkin> For(Capability capability, string theme) =>
        Skins.Where(s => s.Capability == capability && s.Fits(theme)).ToArray();
    public static bool IsKnown(string skinId) =>
        skinId == TownGateKeySkin || skinId == TownAreaKeySkin || skinId == WaystoneKeySkin || Skins.Any(s => s.Id == skinId);

    // Key text. {key} is the key's display name, {place} a generated place, {where} the theme's countryside.
    public const string TownGateKeyText = "The town gate is barred. The {key} lies at the bottom of the dungeon beneath the town.";
    public const string TownAreaKeyText = "A toll-gate shuts the road out of {place}. The keeper wants the {key}, taken from the dungeon.";
    public const string WaystoneKeyText = "A waystone stands dark at {place}. It wakes only for the {key}, kept somewhere in {where}.";

    public static IReadOnlyList<string> TownGateKeyNames { get; } = new[]
    {
        "Warden's Iron Key", "Gatekeeper's Brass Key", "Sexton's Black Key", "Bellringer's Key",
        "Reeve's Copper Key", "Porter's Heavy Key", "Steward's Old Key", "Night Watch Key",
    };
    public static IReadOnlyList<string> TownAreaKeyNames { get; } = new[]
    {
        "Hunter's Tally Token", "Watchman's Signet", "Surveyor's Seal", "Courier's Writ",
        "Drover's Pass-stamp", "Warden's Tin Badge", "Tollkeeper's Chit", "Cartographer's Token",
    };

    /// <summary>The relic a waystone in a later biome is keyed to, by that biome's theme.</summary>
    public static string WaystoneKeyName(string theme) => theme switch
    {
        Highlands => "Cairn Stone", Marsh => "Bog-iron Charm", Coast => "Tide Pearl", Forest => "Heartwood Seal",
        Desert => "Sun Sigil", Tundra => "Frostglass Shard", Ruins => "Obelisk Fragment", Volcanic => "Emberglass Shard",
        _ => theme + " Relic",
    };
    /// <summary>Names for whole biomes, by theme. Each campaign has unique themes, so region names are unique too.</summary>
    public static IReadOnlyList<string> RegionNames(string theme) => RegionNamePools.TryGetValue(theme, out var pool) ? pool : new[] { "the " + theme + " Lands" };
    private static readonly Dictionary<string, string[]> RegionNamePools = new()
    {
        [Highlands] = new[] { "the Greyfell Highlands", "the Harrowmark", "the Stonecrown", "the Windward Heights" },
        [Marsh] = new[] { "the Sallowmere", "the Drowned Fens", "the Reedlands", "the Mirelow" },
        [Coast] = new[] { "the Saltmarch Coast", "the Gull Shore", "the Tidelands", "the Pearl Littoral" },
        [Forest] = new[] { "the Hollowwood", "the Elderveil", "the Thornwild", "the Greywillow Weald" },
        [Desert] = new[] { "the Ashen Sands", "the Sunscar", "the Glasswaste", "the Bleached Reach" },
        [Tundra] = new[] { "the Hoarfrost Waste", "the White Steppe", "the Long Cold Reaches", "the Frozen March" },
        [Ruins] = new[] { "the Sundered Lands", "the Old Kingdom", "the Fallen Marches", "the Ghostholds" },
        [Volcanic] = new[] { "the Cinderlands", "the Burning Reach", "the Emberwaste", "the Slagfields" },
    };

    /// <summary>The first capability whose name (as an identifier or spaced words) appears in the text, or null.</summary>
    public static Capability? LeakedCapability(string text)
    {
        foreach (var capability in CapabilityCatalog.All)
        {
            if (text.Contains(capability.ToString()) || text.Contains(capability.DisplayName())) return capability;
        }
        return null;
    }

    public static IReadOnlyList<string> Places(string theme) => PlacePools.TryGetValue(theme, out var pool) ? pool : FallbackPlaces;

    private static readonly string[] FallbackPlaces = { "the Far Road", "the Lonely March", "Nowhere Hollow" };
    private static readonly Dictionary<string, string[]> PlacePools = new()
    {
        [Highlands] = new[] { "the Gravel Pass", "Hollow Ridge", "the Windcut Stair", "Cairn Saddle", "the Grey Shoulder", "Thrall's Rise", "the Long Scarp", "Stonefold", "the Harrow Heights", "Tern Notch" },
        [Marsh] = new[] { "the Sallow Fen", "Drowned Mere", "the Reedmarch", "Gallow Carr", "the Mire of Oss", "Wick Bog", "the Black Sedge", "Lantern Slough", "Hagmoor", "the Leech Flats" },
        [Coast] = new[] { "Saltgate", "the Wrack Shore", "Gullrock", "the Tidemouth", "Brine Haven", "Cutter's Bay", "the Pearl Steps", "Driftmark", "the Kelp Sound", "Barnacle Quay" },
        [Forest] = new[] { "the Pale Valley", "Thornwood", "the Hush", "Old Elm Hollow", "the Lichen Walk", "Greywillow", "the Tangle", "Mossgate", "the Antler Path", "Wren's Drop" },
        [Desert] = new[] { "the Ashen Waste", "the Glass Flats", "Saltmarrow", "the Dune Sea", "Scorch Hollow", "the Bone Wells", "Cinder Reach", "Sunken Dune", "the Mirage Road", "Dry Cistern" },
        [Tundra] = new[] { "Rime Shallows", "the White Reach", "Frostmere", "Hoar Pass", "the Silent Drift", "Icewind Steppe", "Blue Crevasse", "the Long Cold", "Wolfsbane Moor", "Snowfast" },
        [Ruins] = new[] { "the Fallen Court", "Hollow Aqueduct", "the Broken Nave", "Candle Stair", "the Sundered Gate", "Orrery Hall", "the Toppled Mile", "Ghost Forum", "the Mourning Arch", "Cinder Vault" },
        [Volcanic] = new[] { "the Ember Stair", "Slag Fields", "the Scald", "Brimstone Ridge", "the Cinder Throat", "Ashfall Run", "Magma Weir", "the Glassfall", "Kiln Valley", "Smoke Hollow" },
    };

    private static List<LockSkin> BuildSkins()
    {
        var skins = new List<LockSkin>();
        void S(Capability capability, LockForm form, string text, string resolution, params string[] themes) =>
            skins.Add(new LockSkin($"{capability.ToString().ToLowerInvariant()}.{form.ToString().ToLowerInvariant()}.{skins.Count(k => k.Capability == capability && k.Form == form) + 1}",
                capability, form, themes, text, resolution));
        const LockForm Area = LockForm.Area, Obstacle = LockForm.Obstacle, Interaction = LockForm.Interaction;

        // Area locks: the terrain itself is the lock. Three skins fit any theme; the rest are themed.
        S(Capability.Climb, Area, "The road through {place} ends at a cliff too steep to walk.", "scale the cliff");
        S(Capability.Climb, Area, "A wall of bare rock rises across {place}, and the road resumes somewhere above it.", "climb the rock wall");
        S(Capability.Climb, Area, "Every path through {place} ends at the same ledge, three times a man's height above the ground.", "reach the high ledge");
        S(Capability.Climb, Area, "A sheer face of rock shuts the way through {place}. No path goes over it.", "scale the rock face", Highlands, Tundra);
        S(Capability.Climb, Area, "Old walls and root-wrapped stone block {place}, and the only way past is up and over.", "climb over the old walls", Ruins, Forest);
        S(Capability.Climb, Area, "A hard black scarp cuts off {place}, and the trail resumes on its far lip.", "scale the scarp", Volcanic, Desert);

        S(Capability.Icewalk, Area, "A frozen river lies across the road at {place}, too slick to cross on foot.", "cross the frozen river");
        S(Capability.Icewalk, Area, "A long slope of glare ice fills the way at {place}, and nothing holds on it.", "traverse the icy slope");
        S(Capability.Icewalk, Area, "The ground at {place} has frozen smooth as a mirror, and the far side is a long way off.", "cross the frozen ground");
        S(Capability.Icewalk, Area, "A glare of black ice covers {place}, too slick to stand on.", "cross the black ice", Tundra);
        S(Capability.Icewalk, Area, "Sea-spray has glazed the stones of {place} into a sheet of ice.", "cross the ice-glazed stones", Highlands, Coast);
        S(Capability.Icewalk, Area, "The water of {place} froze in a single night, and the ice is slick as oil.", "cross the frozen water", Marsh, Forest);

        S(Capability.DeepDive, Area, "A flooded passage drops away below {place}. What it guards lies under the surface.", "dive through the flooded passage");
        S(Capability.DeepDive, Area, "The way on at {place} is a pool with no bottom that anyone has seen.", "swim through the deep pool");
        S(Capability.DeepDive, Area, "A drowned stair at {place} descends into water too deep to wade.", "descend the drowned stair");
        S(Capability.DeepDive, Area, "The road at {place} runs under black water, and no light reaches the bottom.", "follow the submerged road", Coast, Marsh);
        S(Capability.DeepDive, Area, "A drowned gallery lies under {place}, its far door beneath the waterline.", "reach the gallery's underwater door", Ruins, Forest);
        S(Capability.DeepDive, Area, "A cistern sunk beneath {place} holds the only way on, and it is full to the brim.", "swim through the flooded cistern", Volcanic, Desert);

        S(Capability.PhaseShift, Area, "The air across {place} holds still and thick as glass, and the road runs on behind it.", "step through the barrier");
        S(Capability.PhaseShift, Area, "A shimmering seam hangs in the road at {place}. Nothing solid goes through it.", "pass through the shimmering seam");
        S(Capability.PhaseShift, Area, "A ward of pale light fills {place}, and the old road lies on its far side.", "cross the ward of pale light");
        S(Capability.PhaseShift, Area, "A wall of pale light closes {place}, and nothing solid passes it.", "pass through the wall of light", Ruins, Desert);
        S(Capability.PhaseShift, Area, "A curtain of flickering light hangs across the road at {place}.", "pass through the flickering curtain", Volcanic, Tundra);
        S(Capability.PhaseShift, Area, "A mist stands unmoving at {place}, and the land beyond it does not stir.", "cross the wall of mist", Forest, Marsh, Highlands);

        S(Capability.HazardWard, Area, "The ground of {place} is poisoned, and the road crosses it for a long way.", "cross the poisoned ground");
        S(Capability.HazardWard, Area, "A bitter haze hangs over {place}, and the birds that fly into it do not come out.", "pass through the bitter haze");
        S(Capability.HazardWard, Area, "Nothing grows in {place}, and nothing that crosses it is unharmed.", "cross the blighted ground");
        S(Capability.HazardWard, Area, "Hot vents and fume fill {place}. Nothing breathes there long.", "pass the steaming vents", Volcanic);
        S(Capability.HazardWard, Area, "A yellow fog lies on {place}, and everything that wades into it comes out sick.", "cross the yellow fog", Marsh);
        S(Capability.HazardWard, Area, "Bad air pools in the hollows of {place}, and the old wells there are fouled.", "pass through the poisoned hollows", Desert, Ruins);

        S(Capability.Boat, Area, "A wide river cuts {place} in two, and the ferry has been gone for years.", "cross the river");
        S(Capability.Boat, Area, "The road ends at a shore in {place}. Whatever lies beyond is across the water.", "sail to the far shore");
        S(Capability.Boat, Area, "The bridge at {place} washed out long ago, and the current is too strong to ford.", "ferry the party past the washed-out bridge");
        S(Capability.Boat, Area, "The road meets open water at {place}, with no ford and no bridge.", "cross the open water", Coast, Marsh);
        S(Capability.Boat, Area, "A black lake fills the valley at {place}, and the far shore is out of sight.", "cross the lake", Forest, Highlands);
        S(Capability.Boat, Area, "A slow grey channel runs through {place}, too wide to jump and too cold to swim.", "navigate the channel", Tundra, Ruins);

        S(Capability.Icebreaker, Area, "Ice has locked the water at {place} from shore to shore.", "break a channel through the ice");
        S(Capability.Icebreaker, Area, "The channel at {place} is choked with floes, and no plain hull will push through.", "clear a channel through the floes");
        S(Capability.Icebreaker, Area, "A shelf of ice fills the strait at {place}, thick enough to stop a keel.", "break through the ice shelf");
        S(Capability.Icebreaker, Area, "Pack ice has choked the channel at {place}, thick enough to stop any hull.", "break through the pack ice", Tundra, Coast);
        S(Capability.Icebreaker, Area, "A frozen lake lies across {place}, with grey water running underneath.", "cross the icebound lake", Highlands, Marsh);
        S(Capability.Icebreaker, Area, "The old canal at {place} is frozen solid, and the lock gates stand open on ice.", "reopen the frozen canal", Forest, Ruins);

        S(Capability.Airship, Area, "The land beyond {place} is an island of rock, and every other crossing is a long drop away.", "fly to the rock island");
        S(Capability.Airship, Area, "The road beyond {place} breaks off at the edge of a drop that no bridge ever spanned.", "fly across the drop");
        S(Capability.Airship, Area, "Whatever lies past {place} can be reached only from above.", "reach the far side by air");
        S(Capability.Airship, Area, "The peaks around {place} rise too high and too steep to cross on foot.", "fly over the peaks", Highlands, Volcanic);
        S(Capability.Airship, Area, "The only road beyond {place} runs through the sky.", "fly to the far side", Desert, Coast);
        S(Capability.Airship, Area, "A gorge opens at {place} so wide that the far side is only a faint line.", "cross the gorge by air", Tundra, Forest);

        S(Capability.Tunneler, Area, "The road beyond {place} lies under a mountain, and no one has ever found the way through.", "bore a passage beneath the mountain");
        S(Capability.Tunneler, Area, "Old shafts at {place} end in blank rock, and the work was never finished.", "extend the unfinished mine shaft");
        S(Capability.Tunneler, Area, "Solid earth fills the way at {place}, and the road resumes somewhere beneath it.", "dig through the packed earth");
        S(Capability.Tunneler, Area, "Solid rock fills the pass at {place}, and the old miners' tunnels stop short of the far side.", "cut through the rock pass", Highlands, Volcanic, Desert);
        S(Capability.Tunneler, Area, "A buried road runs under {place}, its entrance packed solid with earth.", "uncover the buried road", Ruins, Forest);
        S(Capability.Tunneler, Area, "A sunken causeway runs beneath {place}, silted shut at both ends.", "clear the silted causeway", Marsh, Coast, Tundra);

        // Obstacle locks: a discrete object blocks the way.
        S(Capability.Grapple, Obstacle, "A chasm splits the road at {place}. The far ledge is out of reach of any jump.", "reach the ledge across the chasm");
        S(Capability.Grapple, Obstacle, "The path at {place} breaks off at an edge, and the ledge that continues it is a rope's throw away.", "reach the far ledge");
        S(Capability.Grapple, Obstacle, "A rusted ring is set in the far wall at {place}, across a gap no one can leap.", "anchor a rope to the far wall");
        S(Capability.Grapple, Obstacle, "The bridge at {place} has fallen, and only an anchor ring remains on the far side.", "rig a line across the fallen bridge", Ruins, Highlands);
        S(Capability.Grapple, Obstacle, "The far post of a rope bridge stands past the gap at {place}, too far to leap to.", "reach the far bridge post", Forest, Marsh, Coast);
        S(Capability.Grapple, Obstacle, "A split in the ground at {place} is too wide to jump, and the far lip stands higher than the near.", "cross the gap on a rope", Desert, Volcanic, Tundra);

        S(Capability.Breach, Obstacle, "A wall of stone seals {place}, thick as a house and older than the road.", "break through the stone wall");
        S(Capability.Breach, Obstacle, "A barricade of fallen beams and rubble fills {place}, packed tight.", "clear the barricade of beams and rubble");
        S(Capability.Breach, Obstacle, "The way through {place} is stopped up with stone, and has been for longer than anyone remembers.", "break through the stone blockage");
        S(Capability.Breach, Obstacle, "A rockfall has buried the way through {place}.", "clear the rockfall", Highlands, Volcanic);
        S(Capability.Breach, Obstacle, "Thorn growth chokes the trail through {place}, too dense to push through.", "cut through the thorns", Forest, Marsh);
        S(Capability.Breach, Obstacle, "A collapsed arch buries {place}, and the old stone will not shift by hand.", "clear the collapsed arch", Ruins, Desert, Tundra);

        S(Capability.WaystoneStep, Obstacle, "A waystone stands dark at {place}, its far twin somewhere beyond the walls.", "activate the paired waystones");
        S(Capability.WaystoneStep, Obstacle, "A ring of standing stones at {place} faces a blank wall, and the carvings point through it.", "activate the standing stones");
        S(Capability.WaystoneStep, Obstacle, "The road at {place} stops at a gap with nothing on the other side but a matching stone.", "reach the far waystone");
        S(Capability.WaystoneStep, Obstacle, "A sealed door in {place} opens on bare wall, and the carving above it points beyond.", "reach the other side of the sealed door", Ruins, Desert);
        S(Capability.WaystoneStep, Obstacle, "The path at {place} ends in a gap with no bridge, and a standing stone marks the other side.", "cross the gap between waystones", Highlands, Tundra);
        S(Capability.WaystoneStep, Obstacle, "A mossy marker stands alone at {place}, and its twin shows faintly in the haze far beyond.", "reach the mossy marker's twin", Forest, Coast, Marsh);

        // Interaction locks for utilities: a person or a situation.
        S(Capability.Engineering, Interaction, "A winch and a broken gate-engine block the way at {place}. Someone has to put them right.", "repair the gate winch");
        S(Capability.Engineering, Interaction, "The toll-bridge at {place} is stuck half-raised, its gears seized with rust.", "lower the seized toll bridge");
        S(Capability.Engineering, Interaction, "A pump house at {place} has failed, and the flood it held back has taken the road.", "restart the pump and drain the road");
        S(Capability.Engineering, Interaction, "A great lock at {place} turns on gears and counterweights, and half of them are missing.", "repair the gate's counterweights", Ruins, Highlands);
        S(Capability.Engineering, Interaction, "The sluice gates at {place} will not budge, and the water behind them has risen over the road.", "open the sluice gates and drain the road", Coast, Marsh);
        S(Capability.Translation, Interaction, "A foreign trader at {place} will not move her wagons, and no one can understand a word she says.", "persuade the trader to move her wagons");
        S(Capability.Translation, Interaction, "A notice is nailed to the gate at {place} in a script no one nearby can read.", "decipher the gate notice");
        S(Capability.Translation, Interaction, "A traveler at {place} speaks urgently in a tongue you do not know, and points down the road.", "interpret the traveler's directions");
        S(Capability.Translation, Interaction, "An untranslated inscription covers the door at {place}. The carved words mean nothing to you.", "decipher the door inscription", Ruins, Desert);
        S(Capability.Translation, Interaction, "The markers along the pass at {place} are cut in an old script, and the warning on them goes unread.", "read the warnings on the pass", Highlands, Tundra);
        S(Capability.RoyalAuthority, Interaction, "A guard at {place} refuses passage without the crown's seal.", "win passage from the royal guard");
        S(Capability.RoyalAuthority, Interaction, "A royal edict is posted at {place}: the road is closed to all but the crown's own.", "gain passage under the royal edict");
        S(Capability.RoyalAuthority, Interaction, "The garrison at {place} will open the gate for no one who cannot show a royal warrant.", "have the garrison open the gate");
        S(Capability.RoyalAuthority, Interaction, "The toll-keepers at {place} answer only to the throne's writ.", "pass the royal toll post", Highlands, Coast);
        S(Capability.RoyalAuthority, Interaction, "A crown officer holds {place}, and the road stays shut until someone with the crown's leave speaks to him.", "get the crown officer's leave to pass", Ruins, Forest);
        S(Capability.AncientAttunement, Interaction, "An old ward lies across {place}, and it waits for something that belongs to a time long gone.", "dispel the ancient ward");
        S(Capability.AncientAttunement, Interaction, "A door at {place} has no handle and no keyhole, only a worn hollow where a hand once rested.", "release the door's ancient seal");
        S(Capability.AncientAttunement, Interaction, "The old road at {place} is shut by a sleeping power that does not wake for strangers.", "awaken the power guarding the road");
        S(Capability.AncientAttunement, Interaction, "A stone at {place} hums faintly when approached, and the old doors will not stir for strangers.", "wake the stone and open the old doors", Ruins, Desert);
        S(Capability.AncientAttunement, Interaction, "A ring of dead trees at {place} stands around a gate that has not opened in an age.", "unseal the gate among the dead trees", Forest, Highlands);
        S(Capability.BeastSpeech, Interaction, "A herd has made {place} its own. It will not be driven, and it will not be passed.", "persuade the herd to part");
        S(Capability.BeastSpeech, Interaction, "A pack waits on the road at {place}, watching, and does not give ground.", "persuade the pack to let you pass");
        S(Capability.BeastSpeech, Interaction, "Something large and wary guards the way at {place}, and it flinches from every approach.", "calm the creature guarding the way");
        S(Capability.BeastSpeech, Interaction, "A great animal lies across the trail at {place}, and it will not rise for anyone who cannot reason with it.", "persuade the animal to leave the trail", Forest, Highlands);
        S(Capability.BeastSpeech, Interaction, "The creatures of {place} have closed ranks across the only dry path, and none of them will move.", "ask the creatures to clear the dry path", Marsh, Tundra);
        S(Capability.GuildStanding, Interaction, "The guildhall at {place} bars its doors to anyone without standing in the trade.", "gain entry to the guildhall");
        S(Capability.GuildStanding, Interaction, "A guild clerk at {place} turns away every traveler who cannot name a house that vouches for them.", "secure passage from the guild clerk");
        S(Capability.GuildStanding, Interaction, "The way past {place} belongs to the guild, and the guild lets through only its own.", "gain access to the guild road");
        S(Capability.GuildStanding, Interaction, "The dockmasters at {place} give passage only to members of the guild.", "secure passage from the dockmasters", Coast, Ruins);
        S(Capability.GuildStanding, Interaction, "The caravan wardens at {place} wave on the guild's wagons and turn everyone else back.", "join the guild caravan", Highlands, Desert);
        S(Capability.Remedy, Interaction, "A traveler lies sick on the path at {place}, and no one there knows how to help.", "treat the sick traveler");
        S(Capability.Remedy, Interaction, "A child at {place} burns with fever, and the whole camp stands about her, waiting.", "break the child's fever");
        S(Capability.Remedy, Interaction, "A wounded drover lies at {place}, and his cart blocks the only way through.", "dress the drover's wounds so he could move his cart");
        S(Capability.Remedy, Interaction, "An elder at {place} will speak to no one until the sickness in her household is cured.", "treat the elder's household", Marsh, Forest);
        S(Capability.Remedy, Interaction, "A caravan lies stricken at {place}, its drivers too weak to move the wagons off the road.", "treat the drivers so they could move their wagons", Desert, Tundra);
        S(Capability.Diplomacy, Interaction, "A guard refuses passage at {place}, and no one has yet found the words to change his mind.", "persuade the guard to let you pass");
        S(Capability.Diplomacy, Interaction, "Two camps hold {place}, and neither will let the road be used until someone makes them talk.", "broker a truce between the camps");
        S(Capability.Diplomacy, Interaction, "A quarrel at {place} has closed the road, and both sides want a hearing before it opens.", "settle the dispute blocking the road");
        S(Capability.Diplomacy, Interaction, "The headmen of {place} are at odds, and the road is shut until they are made to agree.", "bring the headmen to an agreement", Coast, Highlands);
        S(Capability.Diplomacy, Interaction, "A watchful band holds the ford at {place} and will treat only with someone who comes to talk.", "negotiate passage across the ford", Forest, Marsh, Desert);
        S(Capability.SacredRite, Interaction, "The priests of {place} keep the gate shut until the old rites are performed in full.", "perform the rite that opens the temple gate");
        S(Capability.SacredRite, Interaction, "A shrine bars the road at {place}, and its keeper turns away anyone who does not know the rite.", "perform the shrine keeper's rite");
        S(Capability.SacredRite, Interaction, "A circle of candles burns at {place}, and the way stays closed until the proper words are said.", "complete the rite at the candle circle");
        S(Capability.SacredRite, Interaction, "A shrine blocks the road at {place}, and the way opens only for a proper rite.", "open the shrine passage", Ruins, Desert);
        S(Capability.SacredRite, Interaction, "A sacred grove at {place} shuts its path to anyone who has not been blessed.", "gain entry to the sacred grove", Forest, Highlands);

        S(Capability.MillwrightTraining, Interaction, "The mill wheel at {place} has stopped, leaving its lifting platform stranded above the road.", "restart the mill lift");
        S(Capability.MillwrightTraining, Interaction, "A mill sluice at {place} feeds the wrong channel. Its wooden gearing must be set before the path drains.", "redirect the mill sluice");
        S(Capability.MillwrightTraining, Interaction, "The water-powered hoist at {place} has thrown its belt, and the crossing hangs out of reach.", "repair the belt on the water-powered hoist");
        S(Capability.MillwrightTraining, Interaction, "The sawmill at {place} holds the only bridge raised. Its drive wheel needs a practiced hand.", "lower the sawmill bridge", Forest, Highlands);
        S(Capability.MillwrightTraining, Interaction, "An old pumping wheel at {place} could empty the flooded causeway, but its linkage is out of time.", "drain the flooded causeway", Marsh, Coast, Ruins);

        S(Capability.ForestersWrit, Interaction, "Rangers bar the trail at {place}. Only someone carrying their woodland permit may pass.", "gain passage on the rangers' trail");
        S(Capability.ForestersWrit, Interaction, "A wardens' lodge closes the path through {place}, reserving the trail for approved travelers.", "pass the wardens' lodge");
        S(Capability.ForestersWrit, Interaction, "The protected hunting road at {place} is chained shut. Its keeper asks for the woodland seal.", "have the keeper unchain the hunting road");
        S(Capability.ForestersWrit, Interaction, "The old-growth reserve at {place} admits only travelers vouched for by the local rangers.", "enter the old-growth reserve", Forest, Marsh);
        S(Capability.ForestersWrit, Interaction, "Wardens at {place} guard a sheltered ranger trail across the high country.", "gain access to the sheltered ranger trail", Highlands, Tundra);

        S(Capability.FerrymansFavor, Interaction, "The private ferry at {place} takes no coin. Its keeper carries only those he owes a crossing.", "secure a private ferry crossing");
        S(Capability.FerrymansFavor, Interaction, "A ferryman waits at {place}, but keeps his boat tied until a trusted passenger calls.", "call the ferryman for a crossing");
        S(Capability.FerrymansFavor, Interaction, "The last boat at {place} belongs to a boatman who will cross for a friend, and for no stranger.", "secure passage on the last boat");
        S(Capability.FerrymansFavor, Interaction, "A reed-hidden landing at {place} serves a private skiff whose owner must first be won over.", "secure passage on the hidden skiff", Marsh, Coast);
        S(Capability.FerrymansFavor, Interaction, "The cable ferry at {place} is shuttered for the season, except for passengers owed a personal debt.", "call the cable ferry", Highlands, Tundra);

        S(Capability.GravediggersCharge, Interaction, "The burial gate at {place} is locked. Only the appointed keeper of the dead may open it.", "unlock the burial gate");
        S(Capability.GravediggersCharge, Interaction, "A sexton guards the crypt stair at {place}, asking who has been entrusted with its care.", "gain access to the crypt stair");
        S(Capability.GravediggersCharge, Interaction, "The cemetery passage at {place} is reserved for someone bearing the burial keeper's seal.", "enter the guarded cemetery passage");
        S(Capability.GravediggersCharge, Interaction, "The old charnel vault at {place} is sealed to all but its commissioned caretaker.", "unseal the charnel vault", Ruins, Desert);
        S(Capability.GravediggersCharge, Interaction, "The walled barrows at {place} admit only a recognized keeper of the burial grounds.", "enter the walled barrows", Highlands, Forest, Tundra);

        S(Capability.SmugglersMark, Interaction, "A lookout at {place} conceals the tunnel entrance and waits for the underworld's sign.", "gain entry to the hidden tunnel");
        S(Capability.SmugglersMark, Interaction, "A cellar door at {place} opens only to someone recognized by the hidden trade.", "gain entry through the cellar door");
        S(Capability.SmugglersMark, Interaction, "The night passage through {place} is watched. Its sentry asks for a sign that cannot be bought.", "pass the sentry on the night passage");
        S(Capability.SmugglersMark, Interaction, "A concealed wharf at {place} admits only passengers known to the contraband runners.", "gain access to the concealed wharf", Coast, Marsh);
        S(Capability.SmugglersMark, Interaction, "An illicit caravan tunnel at {place} stays barred until its keeper recognizes a trusted courier.", "open the caravan tunnel", Desert, Ruins, Highlands);

        S(Capability.BellkeepersOffice, Interaction, "The signal bell at {place} controls the far gate. Only an appointed keeper knows the accepted peal.", "signal the far gate to open");
        S(Capability.BellkeepersOffice, Interaction, "A distant lift serves {place}, but its crew answers only the tower's official signal.", "summon the lift");
        S(Capability.BellkeepersOffice, Interaction, "The watch at {place} waits for the authorized bell-call before lowering the crossing.", "signal the watch to lower the crossing");
        S(Capability.BellkeepersOffice, Interaction, "The harbor boom at {place} opens for the bell tower's appointed signaler.", "signal the harbor boom to open", Coast, Marsh);
        S(Capability.BellkeepersOffice, Interaction, "The mountain lift at {place} hangs across the gorge, waiting for the proper tower peal.", "call the mountain lift", Highlands, Volcanic, Tundra);

        S(Capability.FuneraryRites, Interaction, "Restless mourners crowd the road at {place}. Their dead have never been properly laid to rest.", "lay the dead to rest and clear the mourners' road");
        S(Capability.FuneraryRites, Interaction, "A pale procession crosses {place} without end, waiting for someone to complete its burial service.", "lay the pale procession to rest");
        S(Capability.FuneraryRites, Interaction, "An unburied spirit bars {place}. The prayers offered so far have not brought it peace.", "lay the spirit guarding the road to rest");
        S(Capability.FuneraryRites, Interaction, "Voices rise from the drowned graves at {place}, holding the causeway until the dead are commended.", "quiet the drowned graves and clear the causeway", Marsh, Coast);
        S(Capability.FuneraryRites, Interaction, "The fallen soldiers of {place} still guard the road, awaiting the last words of their burial ceremony.", "lay the fallen soldiers to rest", Ruins, Highlands, Desert);

        S(Capability.RoyalSurveyorsMaps, Interaction, "The road at {place} disappears beneath shifting ground. Only the crown's old survey records its safe course.", "trace the road beneath the shifting ground");
        S(Capability.RoyalSurveyorsMaps, Interaction, "A buried entrance at {place} can be found only by matching the old survey marks.", "locate the buried entrance");
        S(Capability.RoyalSurveyorsMaps, Interaction, "Every trail at {place} ends blindly. The crown's measured route is missing from ordinary charts.", "find the crown's surveyed route");
        S(Capability.RoyalSurveyorsMaps, Interaction, "The causeway through {place} lies hidden under silt, its course preserved only in the royal charts.", "trace the silted causeway", Marsh, Coast, Desert);
        S(Capability.RoyalSurveyorsMaps, Interaction, "The abandoned road at {place} is lost among foundations. Survey bearings could trace it again.", "find the old road through the foundations", Ruins, Forest, Highlands);

        // A sealed door for abilities that are not normally interactions. Return gates are always interactions,
        // whichever capability opens them. Keep multiple variants for optional returns and older layouts.
        S(Capability.Climb, Interaction, "A watchtower door at {place} hangs high on the cliff, and its keeper lowers no rope.", "reach the watchtower door");
        S(Capability.Climb, Interaction, "A hermit's door at {place} is cut into a cliff face high above the ground.", "reach the hermit's cliffside door");
        S(Capability.Grapple, Interaction, "A sealed courtyard at {place} sits beyond a gap, with a hook-ring hanging on its far wall.", "reach the courtyard across the gap");
        S(Capability.Grapple, Interaction, "A gatehouse at {place} stands on a pillar of stone, its drawbridge chained up on the far side.", "reach the stranded gatehouse");
        S(Capability.Icewalk, Interaction, "A hermit's hut at {place} stands on a frozen lake, and the ice has taken the road to it.", "cross the frozen lake to the hermit's hut");
        S(Capability.Icewalk, Interaction, "A lamplit window shows across the ice at {place}, and no one has gone out to it in years.", "reach the house across the ice");
        S(Capability.DeepDive, Interaction, "A sealed vault at {place} lies at the foot of a flooded stair.", "reach the vault below the flooded stair");
        S(Capability.DeepDive, Interaction, "A bell rings under the water at {place}, and the door it guards is out of sight below.", "reach the door beneath the water");
        S(Capability.Breach, Interaction, "A walled yard at {place} is stopped up with fallen stone, and the keeper inside cannot get out.", "clear the fallen stone from the courtyard");
        S(Capability.Breach, Interaction, "A storehouse at {place} has been bricked shut, and a faint knocking comes from inside.", "break open the bricked-up storehouse");
        S(Capability.WaystoneStep, Interaction, "A sealed hall at {place} has no door on this side, only a standing stone and its far twin.", "reach the sealed hall through its paired stones");
        S(Capability.WaystoneStep, Interaction, "A hall at {place} can be seen through a barred window, but the way in lies somewhere else entirely.", "reach the hall beyond the barred window");
        S(Capability.PhaseShift, Interaction, "A sealed chamber at {place} shows its inner room through the wall, but there is no door.", "enter the sealed chamber through its wall");
        S(Capability.PhaseShift, Interaction, "A chapel at {place} glows faintly behind a solid wall, as if the stone were not there.", "enter the chapel through its wall");
        S(Capability.HazardWard, Interaction, "A cloister at {place} lies inside a ring of poisoned ground. Its gate stands open and no one has crossed.", "cross the poisoned ground to the cloister");
        S(Capability.HazardWard, Interaction, "A shrine at {place} stands in a ring of bad air, its offerings still fresh.", "reach the shrine through the bad air");
        S(Capability.Boat, Interaction, "A lone house at {place} stands across the water, with no ferry and no bridge.", "reach the house across the water");
        S(Capability.Boat, Interaction, "A lighthouse at {place} shows a steady lamp on the far shore, with no boat to be had.", "reach the lighthouse on the far shore");
        S(Capability.Icebreaker, Interaction, "A harbor town at {place} is frozen shut, and its watchman rings a bell no one can answer.", "open a channel into the frozen harbor");
        S(Capability.Icebreaker, Interaction, "A beacon burns beyond the ice at {place}, and the channel to it has not been open all winter.", "clear the icy channel to the beacon");
        S(Capability.Airship, Interaction, "A sky-landing at {place} can be reached only by air, and its stairs stopped long ago.", "reach the sky-landing");
        S(Capability.Airship, Interaction, "A hilltop hall at {place} stands on a spire of rock, and the only way up is a mooring mast.", "reach the hilltop mooring mast");
        S(Capability.Tunneler, Interaction, "A buried gate at {place} stands half under rubble, its bar still across the door.", "uncover the buried gate");
        S(Capability.Tunneler, Interaction, "A mine office at {place} is sealed by a cave-in, and a lamp still burns inside.", "clear the cave-in at the mine office");
        return skins;
    }
}
