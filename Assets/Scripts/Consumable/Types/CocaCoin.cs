using UnityEngine;

/// <summary>
/// CocaCoin pickup — a special collectible that spawns on a separate lane from regular coins.
/// Assign this script to your CocaCoin prefab.
/// </summary>
public class CocaCoin : Consumable
{
    // How many coins the player earns when collecting a CocaCoin
    public int coinValue = 5;

    public override ConsumableType GetConsumableType()
    {
        return ConsumableType.Coca_COIN;
    }

    public override string GetConsumableName()
    {
        return "Coca Coin";
    }

    public override int GetPrice()
    {
        return 0;   // Free — it's a collectible, not a purchasable powerup
    }

    public override int GetPremiumCost()
    {
        return 0;
    }

    // CocaCoin is instant — no duration needed (duration field inherited but unused here)
    public override void Tick(CharacterInputController c)
    {
        // Collected immediately on trigger — nothing to tick
        m_Active = false;
    }
}
