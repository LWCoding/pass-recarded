using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemEffectRenderer : MonoBehaviour
{

    /// <summary>
    /// Given an item, renders the proper effects that the item
    /// should do when used.
    /// </summary>
    public void RenderEffects(Item itemInfo)
    {
        switch (itemInfo.type)
        {
            case ItemType.CAT_FOOD:
                if (BattleController.Instance)
                {
                    BattleController.Instance.GetPlayer().ChangeHealth(itemInfo.variables[0]);
                }
                else
                {
                    GameManager.ChangeHeroHealth(itemInfo.variables[0]);
                }
                break;
            case ItemType.CHUG_JUG:
                BattleController.Instance.GetPlayer().ChangeBlock(itemInfo.variables[0]);
                break;
            case ItemType.ENERGY_DRINK:
                EnergyController.Instance.ChangeEnergy(itemInfo.variables[0]);
                break;
            case ItemType.PROTEIN_SHAKE:
                BattleController.Instance.GetPlayer().AddStatusEffect(Globals.GetStatus(Effect.STRENGTH, itemInfo.variables[0]));
                break;
        }
    }

}
