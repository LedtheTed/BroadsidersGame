using UnityEngine;

[CreateAssetMenu(menuName = "PirateGame/Ship Definition")]
public class ShipDefinition : ScriptableObject
{

    [Header("Identity")]
    public string displayName = "Sloop";
    public int tier = 1;

    [Header("Hull / Crew")]
    public float maxHull = 100f;
    public float maxCrewHP = 50f;

    [Header("Defense")]
    public float armorFlat = 0f;      // subtract after shields
    [Range(0f, 0.9f)] public float armorPct = 0f; // 0.15 = 15%

    [Header("Shields")]
    public bool hasShields = false;
    public float maxShields = 0f;
    public float shieldRechargePerSec = 0f;
    public float shieldRechargeDelay = 3f;

    [Header("Movement (base values)")]
    public float maxSpeed = 8f;
    public float acceleration = 6f;
    public float turnRateDegPerSec = 90f;

    [Header("Mobility multipliers (applied onto ShipMotor base values)")]
    public float maxSpeedMult = 1f;
    public float accelMult = 1f;
    public float turnRateMult = 1f;
    
}
