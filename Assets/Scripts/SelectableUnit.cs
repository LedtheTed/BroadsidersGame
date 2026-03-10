using UnityEngine;

public class SelectableUnit : MonoBehaviour
{
    [SerializeField] private ShipMotor motor;
    [SerializeField] private GameObject selectionRing;

    public ShipMotor Motor => motor;

    private void Awake()
    {
        if (motor == null) motor = GetComponent<ShipMotor>();
    }

    public void SetSelected(bool selected)
    {
        if (selectionRing != null){
            selectionRing.SetActive(selected);
        }
    }
}