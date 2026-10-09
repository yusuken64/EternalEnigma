using UnityEngine;

// Session-only memory. Only a personal sight check may supply a leader position.
internal sealed class AllyAwareness
{
    private readonly Ally ally;
    private object floor;
    private AllyStrategy strategy;
    internal Ally Leader { get; private set; }
    internal Vector3Int? LastSeenLeader { get; private set; }
    internal bool LeaderVisible { get; private set; }
    internal bool SearchedLastSeen { get; set; }
    internal Vector3Int? PreviousSearchTile { get; set; }
    internal bool SearchFirst => ally.AllyStrategy == AllyStrategy.Follow && Leader != ally && !LeaderVisible;

    internal AllyAwareness(Ally ally) { this.ally = ally; }

    internal void Refresh(Game game)
    {
        var leader = game.PlayerController.PartyLeader;
        var currentFloor = game.CurrentDungeon.Floor;
        if (!ReferenceEquals(floor, currentFloor) || Leader != leader)
        {
            floor = currentFloor;
            Leader = leader;
            LastSeenLeader = null;
            SearchedLastSeen = false;
            PreviousSearchTile = null;
            ClearPursuit();
        }
        if (strategy != ally.AllyStrategy)
        {
            strategy = ally.AllyStrategy;
            ClearPursuit();
        }
        LeaderVisible = leader != null && (leader == ally || game.CurrentDungeon.CanSee(ally, leader));
        if (LeaderVisible)
        {
            LastSeenLeader = leader.TilemapPosition;
            SearchedLastSeen = false;
            PreviousSearchTile = null;
        }
        if (!AllyCombat.IsVisibleHostile(game, ally, ally.PursuitTarget)) ClearPursuit();
    }

    private void ClearPursuit()
    {
        ally.PursuitTarget = null;
        ally.PursuitPosition = null;
    }
}
