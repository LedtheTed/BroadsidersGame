using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    
    private readonly List<SelectableUnit> selected = new List<SelectableUnit>();

    public IReadOnlyList<SelectableUnit> Selected => selected;

    public void ClearSelection()
    {
        for (int i = 0; i < selected.Count; i++)
            selected[i].SetSelected(false);
        selected.Clear();
    }

    public void AddToSelection(SelectableUnit unit)
    {
        if (unit == null) return;
        if (selected.Contains(unit)) return;
        selected.Add(unit);
        unit.SetSelected(true);
    }

    public void RemoveFromSelection(SelectableUnit unit)
    {
        if (unit == null) return;
        if (!selected.Remove(unit)) return;
        unit.SetSelected(false);
    }

    public void SetSelectionSingle(SelectableUnit unit)
    {
        ClearSelection();
        AddToSelection(unit);
    }
}
