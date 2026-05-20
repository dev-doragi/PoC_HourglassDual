using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Combat Party Data", fileName = "CombatPartyData")]
public class CombatPartyDataSO : ScriptableObject
{
    public string partyName;
    public CombatActorType teamType;
    public CombatActorDataSO[] actors;
}
