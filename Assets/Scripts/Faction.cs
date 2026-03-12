using UnityEngine;

[CreateAssetMenu(menuName = "PirateGame/Faction")]
public class Faction : ScriptableObject
{
    [SerializeField] private string factionName;
    [SerializeField] private Color factionColor = Color.white;
    [SerializeField] private int factionID;

    public string FactionName => factionName;
    public Color FactionColor => factionColor;
    public int FactionID => factionID;
}
