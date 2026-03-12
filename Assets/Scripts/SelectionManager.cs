using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    // **********************************************************
    // HOLD LIST OF CURRENTLY SELECTED SHIPS
    // THIS IS THE ONLY THING THAT CAN ADD/REMOVE FROM THAT LIST
    // **********************************************************

    // the list
    private readonly List<SelectableUnit> selected = new List<SelectableUnit>();

    // command which return the list
    public IReadOnlyList<SelectableUnit> Selected => selected;

    // go through and deselect everything in list
    public void ClearSelection(){
        for(int i = 0; i < selected.Count; i++){
            selected[i].SetSelected(false);
        }
        selected.Clear();
    }

    // add input to the select list
    public void AddToSelection(SelectableUnit unit){
        if(unit == null) return;                // check input
        if(selected.Contains(unit)) return;     // check already in list
        
        // check if unit is from the player faction
        ShipState shipState = unit.GetComponent<ShipState>();
        if (shipState != null && !FactionManager.Instance.IsPlayerShip(shipState)) {
            return;  // cannot select enemy ships
        }
        
        selected.Add(unit);
        unit.SetSelected(true);
    }

    // remove input from select list
    public void RemoveFromSelection(SelectableUnit unit){
        if (unit == null) return;
        if (!selected.Remove(unit)) return;
        unit.SetSelected(false);
    }

    // when you single click, make only that new click in list
    public void SetSelectionSingle(SelectableUnit unit){
        ClearSelection();
        AddToSelection(unit);
    }
}
