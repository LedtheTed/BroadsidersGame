using UnityEngine;
using System.Collections.Generic;
using System;

public class FactionManager : MonoBehaviour
{
    public static FactionManager Instance { get; private set; }

    [SerializeField] private Faction playerFaction;

    private Dictionary<Faction, List<ShipState>> factionShips = new();
    private HashSet<ShipState> allShips = new();

    public Faction PlayerFaction => playerFaction;

    public event Action<Faction, ShipState> OnShipRegistered;
    public event Action<Faction, ShipState> OnShipUnregistered;

    private void Awake(){
        if (Instance != null && Instance != this){
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    /// register a ship with its faction
    public void RegisterShip(ShipState ship){
        if (ship == null || allShips.Contains(ship)) return;

        Faction faction = ship.Faction;
        if (faction == null){
            Debug.LogWarning($"Ship {ship.name} has no faction assigned!");
            return;
        }

        allShips.Add(ship);

        if (!factionShips.ContainsKey(faction)){
            factionShips[faction] = new List<ShipState>();
        }

        factionShips[faction].Add(ship);
        OnShipRegistered?.Invoke(faction, ship);
    }

    // unregister a ship from its faction
    public void UnregisterShip(ShipState ship){
        if (ship == null || !allShips.Contains(ship)) return;

        Faction faction = ship.Faction;
        if (faction != null && factionShips.ContainsKey(faction)){
            factionShips[faction].Remove(ship);
            if (factionShips[faction].Count == 0){
                factionShips.Remove(faction);
            }
        }

        allShips.Remove(ship);
        OnShipUnregistered?.Invoke(faction, ship);
    }

    // get all ships belonging to a faction
    public List<ShipState> GetFactionShips(Faction faction){
        if (faction == null) return new List<ShipState>();
        return factionShips.ContainsKey(faction) ? new List<ShipState>(factionShips[faction]) : new List<ShipState>();
    }

    // get ship count for a faction
    public int GetFactionShipCount(Faction faction){
        if (faction == null) return 0;
        return factionShips.ContainsKey(faction) ? factionShips[faction].Count : 0;
    }

    // get all factions that have ships
    public IReadOnlyList<Faction> GetActiveFactions(){
        return new List<Faction>(factionShips.Keys);
    }

    // get all ships in the game
    public IReadOnlyCollection<ShipState> GetAllShips(){
        return allShips;
    }

    // check if two ships are from the same faction
    public bool AreAllies(ShipState ship1, ShipState ship2){
        if (ship1 == null || ship2 == null) return false;
        if (ship1.Faction == null || ship2.Faction == null) return false;
        return ship1.Faction == ship2.Faction;
    }

    // check if a ship belongs to the player faction
    public bool IsPlayerShip(ShipState ship){
        if (ship == null || playerFaction == null) return false;
        return ship.Faction == playerFaction;
    }
}
