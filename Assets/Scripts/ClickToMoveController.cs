using UnityEngine;
using UnityEngine.InputSystem;

public class ClickToMoveController : MonoBehaviour
{
    
    [Header("Input")]
    [SerializeField] private int mouseButton = 1; // 1 = right click

    [Header("Raycast")]
    [SerializeField] private float maxRayDistance = 5000f;

    [Header("Optional Marker")]
    [SerializeField] private Transform moveMarker;


    private void Update()
    {
        
        if(GameMaster.Instance == null){
            return;
        }

        if (Mouse.current == null) return ;

        if(Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryIssueMove();
        }
    }

    private void TryIssueMove()
    {
        Camera cam = GameMaster.Instance.MainCamera;
        ShipMotor ship = GameMaster.Instance.DebugControlledShip;

        if (cam == null){
            Debug.LogError("[ClickToMoveController] MainCamera is null (assign it in GameMaster).");
            return;
        }

        if(ship == null)
        {
            Debug.LogError("[ClickToMoveController] DebugControlledShip is null (assign it in GameMaster).");
            return;
        }
        
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, GameMaster.Instance.WaterMask))
        {
            Vector3 dest = hit.point;
            ship.SetDestination(dest);

            if (moveMarker != null)
            {
                moveMarker.gameObject.SetActive(true);
                moveMarker.position = dest;
            }
        } else {
            Debug.Log("[ClickToMoveController] Raycast did not hit Water layer. Check collider + layer + mask.");
        }
    }

}
