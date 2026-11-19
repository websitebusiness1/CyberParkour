using UnityEngine;

/// <summary>Anything that hurts on contact (use a trigger collider). Solid walls are NOT hazards.</summary>
public class Hazard : MonoBehaviour
{
    public int damage = 1;
    public string hazardName = "Malware";
    [Tooltip("Costs a life AND respawns the player at the last checkpoint (chasing walls, fake gateways, overloads).")]
    public bool respawnOnHit = false;
    [Tooltip("Surveillance beams: cannot detect a player who is inside a CoverZone.")]
    public bool ignoredWhenCovered = false;
    [Tooltip("Data packets: a hit also raises the network overload meter.")]
    public bool addsOverload = false;
    [TextArea] public string hitMessage;
}
