using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ShopRelicHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{

    public TextMeshProUGUI relicCostText;
    public GameObject frameOverlayObject;
    private RelicHandler _parentRelicHandler;
    private Relic _relicInfo;
    private Transform _parentRelicTransform;
    private bool _isInteractable;
    private int _relicCost;

    private void Awake()
    {
        _parentRelicHandler = GetComponent<RelicHandler>();
        _parentRelicTransform = _parentRelicHandler.imageObject.transform;
    }

    private void Start()
    {
        _relicInfo = _parentRelicHandler.relicInfo;
        _relicCost = GetRandomCost(_relicInfo.relicRarity);
        _isInteractable = true;
        frameOverlayObject.GetComponent<Image>().sprite = _relicInfo.relicImage;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable) { return; }
        // Show price on mouse enter!
        relicCostText.text = "$" + _relicCost.ToString();
        frameOverlayObject.SetActive(true);
        // Set the color of the overlay text depending on if
        // the player can afford it or not.
        if (_relicCost < GameController.GetMoney())
        {
            // Can afford the relic!
            relicCostText.color = new Color(0.3f, 1, 0);
        }
        else
        {
            // Cannot afford the relic.
            relicCostText.color = new Color(1, 0.15f, 0.15f);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isInteractable) { return; }
        // Hide price on mouse exit!
        frameOverlayObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isInteractable) { return; }
        // If you can buy the relic, buy the relic.
        if (_relicCost <= GameController.GetMoney())
        {
            // Subtract the money and update the top bar.
            GameController.SpendMoney(_relicCost);
            TopBarController.Instance.UpdateCurrencyText();
            // Make the relic not interactable.
            _isInteractable = false;
            relicCostText.text = "";
            // Make the tooltip not show anymore.
            _parentRelicHandler.showTooltipOnHover = false;
            _parentRelicHandler.tooltipParentObject.SetActive(false);
            // Play the relic chosen SFX.
            SoundManager.Instance.PlaySFX(SoundEffect.SHOP_PURCHASE);
            // Add the relic to the deck.
            GameController.AddRelicToInventory(_relicInfo);
            TopBarController.Instance.RenderRelics();
        }
    }

    private int GetRandomCost(RelicRarity relicRarity)
    {
        switch (relicRarity)
        {
            case RelicRarity.COMMON:
                return 150 + Random.Range(-20, 20); // 130-170 $
            case RelicRarity.UNCOMMON:
                return 200 + Random.Range(-20, 20); // 180-220 $
            case RelicRarity.RARE:
                return 250 + Random.Range(-20, 20); // 230-270 $
        }
        return 99999; // Unobtainable!
    }

}
