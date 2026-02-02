using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using UnityEngine;

public class GameMaster : MonoBehaviour
{
    
    public static GameMaster Instance { get; private set; }

    [Header("Scene Management")]
    [SerializeField] private Camera mainCamera;

    [Header("Raycast Layers")]
    [SerializeField] private LayerMask waterMask;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Prototype Control")]
    [SerializeField] private ShipMotor debugControlledShip;

    public Camera MainCamera => mainCamera;
    public LayerMask WaterMask => waterMask;
    public LayerMask ObstacleMask => obstacleMask;
    public ShipMotor DebugControlledShip => debugControlledShip;

    private void Awake()
    {
        if (Instance != null && Instance != this){
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        if(mainCamera == null){
            mainCamera = Camera.main;
        }
        
        if(mainCamera == null){
            UnityEngine.Debug.Log("GameMaster: No main camera found in the scene.");
        }
    
    }


}
