using UnityEngine;
using System;


public enum DamageType { Cannon, Fire, Explosive, Boarding, Ram }

public class ShipState : MonoBehaviour
{
    
    [Header("Definition")]
    [SerializeField] private ShipDefinition def;        // pull info from ship def defined in ship prefabs
    
    [Header("Faction")]
    [SerializeField] private Faction faction;

    public ShipDefinition Definition => def;
    public Faction Faction => faction;
    public void SetFaction(Faction newFaction) => faction = newFaction;

    public float Hull { get; private set; }
    public float CrewHP { get; private set; }
    public float Shields { get; private set; }
    public bool IsSunk { get; private set; }

    private float shieldDelayTimer;

    public event Action<float, float> OnHullChanged;     // (cur, max)
    public event Action<float, float> OnShieldsChanged;  // (cur, max)
    public event Action OnSunk;

    private void Awake(){
        if (def == null){
            Debug.LogError($"{name}: ShipState has no ShipDefinition assigned.");
            enabled = false;
            return;
        }

        Hull = def.maxHull;
        CrewHP = def.maxCrewHP;
        Shields = def.hasShields ? def.maxShields : 0f;

        OnHullChanged?.Invoke(Hull, def.maxHull);
        if (def.hasShields) OnShieldsChanged?.Invoke(Shields, def.maxShields);
        
        // Register with FactionManager
        FactionManager.Instance.RegisterShip(this);
    }

    // only really needed for rechaging shiled (if we want)
    private void Update(){
        if (IsSunk) return;

        if (!def.hasShields) return;
        if (def.maxShields <= 0f || def.shieldRechargePerSec <= 0f) return;

        if (shieldDelayTimer > 0f){
            shieldDelayTimer -= Time.deltaTime;
            return;
        }

        if (Shields < def.maxShields){
            Shields = Mathf.Min(def.maxShields, Shields + def.shieldRechargePerSec * Time.deltaTime);
            OnShieldsChanged?.Invoke(Shields, def.maxShields);
        }
    }

    public void ApplyDamage(float amount, DamageType type){
        if (IsSunk || amount <= 0f) return;

        if (def.hasShields) shieldDelayTimer = def.shieldRechargeDelay;

        // shields first
        if (def.hasShields && Shields > 0f){
            float absorbed = Mathf.Min(Shields, amount);
            Shields -= absorbed;
            amount -= absorbed;
            OnShieldsChanged?.Invoke(Shields, def.maxShields);
            if (amount <= 0f) return;
        }

        // armor
        float afterFlat = Mathf.Max(0f, amount - def.armorFlat);
        float afterPct = afterFlat * (1f - Mathf.Clamp01(def.armorPct));

        Hull = Mathf.Max(0f, Hull - afterPct);
        OnHullChanged?.Invoke(Hull, def.maxHull);

        if (Hull <= 0f) Sink();
    }

    public void HealHull(float amount){
        if (IsSunk || amount <= 0f) return;
        Hull = Mathf.Min(def.maxHull, Hull + amount);
        OnHullChanged?.Invoke(Hull, def.maxHull);
    }

    public void DamageCrew(float amount){
        if (IsSunk || amount <= 0f) return;
        CrewHP = Mathf.Max(0f, CrewHP - amount);
    }

    private void Sink(){
        if (IsSunk) return;
        IsSunk = true;

        // Unregister from FactionManager
        FactionManager.Instance.UnregisterShip(this);

        // stop movement
        var motor = GetComponent<ShipMotor>();
        if (motor != null) motor.enabled = false;

        OnSunk?.Invoke();
    }

}
