namespace EternalEnigma.Core.Generation;

public sealed class BossProfile
{
    public string Family { get; }
    public int MinimumTier { get; }
    public string Ability { get; }
    public int Footprint => 3;
    public float HealthMultiplier => 5;
    public float StrengthMultiplier => 1.25f;
    public float ExperienceMultiplier => 5;
    public int WarningTurns => 1;
    public int CooldownTurns => 3;
    public int EffectTurns => 2;
    internal BossProfile(string family, int tier, string ability) { Family = family; MinimumTier = tier; Ability = ability; }
}

public static class BossCatalog
{
    public static IReadOnlyList<BossProfile> All { get; } = Array.AsReadOnly(new[] {
        new BossProfile("Slime",0,"Leaping splash with knockback"), new BossProfile("Bat",0,"Silencing sonic pulse"),
        new BossProfile("Cactus",0,"Radial needle volley"), new BossProfile("Fishman",0,"Tidal shove"),
        new BossProfile("Crab Monster",0,"Immobilizing pincer strike"), new BossProfile("Evil Mage",0,"Delayed sleep sigil"),
        new BossProfile("Rat Assassin",1,"Poisoned bolt volley"), new BossProfile("Angry Mushroom",1,"Lingering poison cloud"),
        new BossProfile("Smiling Mushroom",0,"Regenerative spore field"), new BossProfile("Spider",1,"Rooting web patches"),
        new BossProfile("Specter",1,"Life-draining beam"), new BossProfile("Monster Plant",1,"Vine pull and bind"),
        new BossProfile("Naga Wizard",1,"Marked arcane displacement"), new BossProfile("Stingray",1,"Lightning fan"),
        new BossProfile("Bishop Knight",2,"Cross-shaped lance strike"), new BossProfile("Salamander",2,"Rush leaving a fire trail"),
        new BossProfile("Werewolf",2,"Bleeding pounce"), new BossProfile("Metal Slime",2,"Armored ricochet charge followed by an exposed turn"),
        new BossProfile("Black Knight",2,"Guard stance and counterstrike"), new BossProfile("Orc",2,"War cry and empowered attacks"),
        new BossProfile("Turtle Shell",2,"Spinning knockback attack"), new BossProfile("Golem",3,"Ground-slam shockwave"),
        new BossProfile("Worm Monster",3,"Marked burrow-and-erupt attack"), new BossProfile("Skeleton",3,"Temporary destructible bone cage"),
        new BossProfile("Battle Bee",3,"Poisonous charging sting"), new BossProfile("Chest Monster",3,"Pull and delayed bite"),
        new BossProfile("Cyclops",3,"Marked boulder throw"), new BossProfile("Beholder",4,"Confusion gaze"),
        new BossProfile("Flying Demon",4,"Swooping fire bombardment"), new BossProfile("Lizard Warrior",4,"Sequenced three-strike combo"),
        new BossProfile("Demon King",4,"Delayed eruptions on marked tiles"), new BossProfile("Dragon",4,"Fire breath cone") });
    public static BossProfile Select(int seed, int tier)
    {
        var eligible = All.Where(p => p.MinimumTier <= tier && p.MinimumTier >= Math.Max(0, tier - 1)).ToArray();
        return eligible[new SeedStream(seed, 1703).Range(eligible.Length)];
    }
}
