namespace EternalEnigma.Core.Classes;

public enum SkillKind { Normal, Mastery, Gathering, SingleRank }
public enum ClassSource { Primary, Secondary }
public enum LearnRefusal { None, NotInKit, MaxRankReached, PreviousMasteryMissing,
    TierLocked, NotEnoughLowerTierSkills, LevelTooLow, InvalidCost }
