using UnityEngine;

public class SelectableUnit : MonoBehaviour
{
    [SerializeField] private ShipMotor motor;
    [SerializeField] private GameObject selectionRing;

    private Renderer shipRenderer;
    private Color originalColor;

    public ShipMotor Motor => motor;

    private void Awake()
    {
        if (motor == null) motor = GetComponent<ShipMotor>();
        
        // find renderer on this ship
        shipRenderer = GetComponentInChildren<Renderer>();
        if (shipRenderer != null) {
            originalColor = shipRenderer.material.color;
        }
        
        // apply faction color
        ApplyFactionColor();
    }

    private void ApplyFactionColor() {
        ShipState shipState = GetComponent<ShipState>();
        if (shipRenderer == null || shipState?.Faction == null) return;
        
        Color factionColor = shipState.Faction.FactionColor;
        shipRenderer.material.color = factionColor;
    }

    public void SetSelected(bool selected)
    {
        if (selectionRing != null){
            selectionRing.SetActive(selected);
        }
    }
}