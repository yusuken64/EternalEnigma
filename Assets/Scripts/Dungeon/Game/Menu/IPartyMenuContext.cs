using System;
using System.Collections.Generic;
using UnityEngine;

public enum PartyMenuTab { Inventory, Skills, Capabilities, Equipment, Stats }

public sealed class PartyMenuHero
{
    public string Id, Name;
    public Sprite Portrait;
    public TownAlly TownActor;
    public Ally DungeonActor;
    public Equipment Equipment => TownActor != null ? TownActor.Equipment : DungeonActor.Equipment;
}

public sealed class PartyMenuEntry
{
    public InventoryItem Item;
    public Skill Skill;
    public string Title, Description, Section;
    public Sprite Icon;
    public bool Equipped;
    public EquipmentSlot? Slot;
    public bool SpendAttribute;
    public object Identity => (object)Item ?? Skill ?? (object)Slot ?? (SpendAttribute ? "spend" : Title);
}

public sealed class PartyMenuAction
{
    public string Label, UnavailableReason;
    public Action<PartyMenu> Execute;
    public bool Available => string.IsNullOrEmpty(UnavailableReason);
}

public interface IPartyMenuContext : IDisposable
{
    IReadOnlyList<PartyMenuHero> Heroes { get; }
    List<PartyMenuEntry> Entries(PartyMenuHero hero, PartyMenuTab tab);
    List<PartyMenuAction> Actions(PartyMenuHero hero, PartyMenuEntry entry);
    string HeroDetails(PartyMenuHero hero);
    string Restriction(PartyMenuHero hero, PartyMenuEntry entry);
}

// Contexts opt into their existing item workflow without adding an extra picker.
public interface IPartyMenuEntryHandler
{
    bool OpenEntry(PartyMenu menu, PartyMenuHero hero, PartyMenuEntry entry);
}
