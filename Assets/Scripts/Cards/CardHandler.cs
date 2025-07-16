using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum CardAnimation
{
    SHRINK, TRANSLATE_DOWN
}

public class CardHandler : MonoBehaviour
{

    [HideInInspector] public Card card;
    [HideInInspector] public int cardIdx; // Index in deck, if necessary
    [Header("Object Assignments")]
    public GameObject CardObject;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private Image previewImage;
    [SerializeField] private Image frameImage;
    public void ResetCardColor() => SetPreviewColor(new Color(1, 1, 1));
    // Sets the card's color.
    public void SetPreviewColor(Color color)
    {
        previewImage.color = color;
        frameImage.color = color;
    }
    [SerializeField] private GameObject tooltipParentObject;
    [SerializeField] private Transform cardEffectOverlayTransform;
    [SerializeField] private GraphicRaycaster _graphicRaycaster;

    [HideInInspector] public Vector3 initialPosition;
    [HideInInspector] public float initialScale;
    [HideInInspector] public int initialSortingOrder;
    [HideInInspector] public Quaternion initialRotation;

    private List<CardEffectType> _currentCardEffectTypes;
    private CardHoverHandler _cardHoverController;
    private Transform _canvasTransform;
    private CanvasGroup _cardCanvasGroup;
    private IEnumerator _tooltipShowCoroutine = null;
    private Vector3 _tooltipInitialLocalPosition;

    private void Awake()
    {
        _cardHoverController = GetComponent<CardHoverHandler>();
        _cardCanvasGroup = GetComponent<CanvasGroup>();
        _canvasTransform = GameObject.Find("Canvas").transform;
        _tooltipInitialLocalPosition = tooltipParentObject.transform.localPosition;
    }

    // Usually, this card is animated into frame with the CardAppear coroutine.
    // There is a second parameter to make it show instantly instead.
    public void Initialize(Card c, bool shouldShowInstantly = true, int strengthBuff = 0, int defenseBuff = 0, int idx = 0)
    {
        NullifyCardEffects();
        _currentCardEffectTypes = new List<CardEffectType>();
        // Set all of the basic properties
        InitializeStartingData();
        EnableInteractions();
        // Set the card information
        card = c;
        cardIdx = idx;
        UpdateCardVisuals(strengthBuff, defenseBuff);
        HideTooltip();
        // If we're in a battle, update this card's appearance based on its cost
        // if it's one of the cards showing up in battle.
        if (SceneManager.GetActiveScene().name == "Battle" && BattleController.Instance.GetGameState() == GameState.PLAYER_TURN)
        {
            UpdateColorBasedOnPlayability();
        }
        else
        {
            ResetCardColor();
        }
        // Make sure all opacities for the card
        // are automatically shown IF it is a wanted
        // behavior.
        if (shouldShowInstantly)
        {
            ShowCardInstantly();
        }
    }

    // Updates the card's color depending on if it's playable or not.
    // Uses a currentEnergy parameter to compare the card's cost to.
    public void UpdateColorBasedOnPlayability()
    {
        int currentEnergy = EnergyController.Instance.GetCurrentEnergy();
        if (currentEnergy < card.GetCardStats().cardCost)
        {
            SetPreviewColor(new Color(1, 0.6f, 0.6f));
        }
        else
        {
            SetPreviewColor(new Color(1, 1, 1));
        }
    }

    // Updates the card information based on the current stored card.
    // This is because the card may change for this CardHandler.
    public void UpdateCardVisuals(int strengthBuff = 0, int defenseBuff = 0)
    {
        nameText.text = card.cardData.GetCardDisplayName() + new String('+', card.level - 1);
        UpdateCardDescription(strengthBuff, defenseBuff);
        costText.text = card.GetCardStats().cardCost.ToString();
        levelText.text = "LV." + card.level;
        previewImage.sprite = card.cardData.cardImage;
    }

    // Update the values of the card depending on any strength or defense
    // buffs the player might have during battle.
    public void UpdateCardDescription(int strengthBuff = 0, int defenseBuff = 0)
    {
        string cardText = descText.text;
        int calcStrengthBuff = (card.GetTarget() == Target.SELF) ? 0 : strengthBuff;
        string strengthBuffText = (calcStrengthBuff == 0) ? "" : " (" + ((calcStrengthBuff > 0) ? "<color=\"green\">+" : "<color=\"red\">") + calcStrengthBuff + "</color>)";
        string defenseBuffText = (defenseBuff == 0) ? "" : " (" + ((defenseBuff > 0) ? "<color=\"green\">+" : "<color=\"red\">") + defenseBuff + "</color>)";
        cardText = card.GetCardStats().cardDesc;
        // Replace the [ATK] and [DEF] placeholders with the actual values.
        int calcDamageValue = Mathf.Max(0, card.GetCardStats().damageValue + calcStrengthBuff);
        cardText = cardText.Replace("[ATK]", (calcDamageValue + strengthBuffText).ToString());
        cardText = cardText.Replace("[DEF]", (card.GetCardStats().blockValue + defenseBuff).ToString() + defenseBuffText);
        // Replace the [ATKLUCK] placeholders with the actual values
        CardModifier luckAtkModifier = card.GetCardStats().modifiers.Find((m) => m.trait == Trait.ADDITIONAL_LUCK_DAMAGE);
        if (luckAtkModifier != null)
        {
            int calcLuckDamageValue = Mathf.Max(0, luckAtkModifier.amplifier + calcStrengthBuff);
            cardText = cardText.Replace("[ATKLUCK]", (calcLuckDamageValue).ToString());
        }
        // Update the status effect texts with their actual icons.
        cardText = GameController.GetDescriptionWithIcons(cardText);
        descText.text = cardText;
    }

    // This should be called to set info like default positions, etc. when
    // the card is simply edited and not newly instantiated.
    public void InitializeStartingData()
    {
        // Set all of the basic properties
        initialPosition = transform.position;
        CardObject.transform.position = initialPosition;
        CardObject.transform.localScale = new Vector3(1, 1, 1);
        initialScale = transform.localScale.x;
        initialSortingOrder = GetComponent<Canvas>().sortingOrder;
        initialRotation = transform.rotation;
        tooltipParentObject.transform.localPosition = _tooltipInitialLocalPosition;
    }

    // Give this card a card effect.
    public void InflictCardEffect(CardEffectType type)
    {
        CardEffect cardEffect = Globals.GetCardEffect(type);
        GameObject cardEffectObject = ObjectPooler.Instance.GetObjectFromPool(PoolableType.CARD_EFFECT);
        cardEffectObject.GetComponent<Image>().sprite = cardEffect.sprite;
        cardEffectObject.transform.SetParent(cardEffectOverlayTransform, false);
        cardEffectObject.transform.localPosition = Vector3.zero;
        cardEffectObject.transform.localRotation = Quaternion.identity;
        StartCoroutine(AnimateCardEffectIn(cardEffectObject));
        _currentCardEffectTypes.Add(type);
    }

    private IEnumerator AnimateCardEffectIn(GameObject cardEffectObject)
    {
        float currTime = 0;
        float targetTime = 0.4f;
        Color initialColor = new Color(1, 1, 1, 0);
        Color targetColor = new Color(1, 1, 1, 1);
        Vector3 initialScale = cardEffectObject.transform.localScale;
        Vector3 targetScale = initialScale + new Vector3(0.1f, 0.1f, 0);
        Image cardEffectObjectImage = cardEffectObject.GetComponent<Image>();
        while (currTime < targetTime)
        {
            currTime += Time.deltaTime;
            cardEffectObjectImage.color = Color.Lerp(initialColor, targetColor, currTime / targetTime);
            cardEffectObject.transform.localScale = Vector3.Lerp(initialScale, targetScale, currTime / targetTime);
            yield return null;
        }
        initialColor = new Color(1, 1, 1, 1);
        targetColor = new Color(1, 1, 1, 0.9f);
        targetScale = initialScale;
        initialScale = cardEffectObject.transform.localScale;
        currTime = 0;
        targetTime = 0.2f;
        while (currTime < targetTime)
        {
            currTime += Time.deltaTime;
            cardEffectObjectImage.color = Color.Lerp(initialColor, targetColor, currTime / targetTime);
            cardEffectObject.transform.localScale = Vector3.Lerp(initialScale, targetScale, currTime / targetTime);
            yield return null;
        }
        cardEffectObjectImage.color = targetColor;
    }

    // Remove all card effects from the card.
    public void NullifyCardEffects()
    {
        _currentCardEffectTypes = new List<CardEffectType>();
        for (int i = 0; i < cardEffectOverlayTransform.childCount; i++)
        {
            ObjectPooler.Instance.ReturnObjectToPool(PoolableType.CARD_EFFECT, cardEffectOverlayTransform.GetChild(i).gameObject);
        }
    }

    // Returns a boolean if the card has a certain effect.
    public bool HasCardEffect(CardEffectType type)
    {
        return _currentCardEffectTypes.Contains(type);
    }

    // Sets the sorting order and overrides the initial sorting order
    // saved on this GameObject.
    public void SetSortingOrder(int sortingOrder)
    {
        GetComponent<Canvas>().sortingOrder = sortingOrder;
        initialSortingOrder = sortingOrder;
    }

    // Gradually lerps the position, scale, and rotation of the card to the specified values.
    // If targetPosition is equal to Vector3.zero, this function does not Lerp the position.
    public IEnumerator LerpTransformAndChangeOrder(float targetValue, Vector3 targetPosition, Quaternion targetRotation, int newSortOrder)
    {
        GetComponent<Canvas>().sortingOrder = newSortOrder;
        float currTime = 0;
        float timeToWait = 0.22f;
        Vector3 initialPosition = transform.position;
        Quaternion initialRotation = transform.rotation;
        Vector3 initialScale = transform.localScale;
        Vector3 targetScale = new Vector3(targetValue, targetValue, 1);
        while (currTime < timeToWait)
        {
            currTime += Time.deltaTime;
            // If target position is Vector3.zero (0, 0, 0), then we don't Lerp the position.
            if (targetPosition != Vector3.zero)
            {
                transform.position = Vector3.Lerp(initialPosition, targetPosition, Mathf.SmoothStep(0, 1, currTime / timeToWait));
            }
            transform.localScale = Vector3.Lerp(initialScale, targetScale, Mathf.SmoothStep(0, 1, currTime / timeToWait));
            transform.rotation = Quaternion.Lerp(initialRotation, targetRotation, Mathf.SmoothStep(0, 1, currTime / timeToWait));
            yield return null;
        }
    }

    public void CardAppear()
    {
        StartCoroutine(CardAppearCoroutine(0.18f));
    }

    // This function will make the card fully shown.
    public void ShowCardInstantly()
    {
        _cardCanvasGroup.alpha = 1;
    }

    // This function will make the card fully transparent.
    public void HideCardInstantly()
    {
        _cardCanvasGroup.alpha = 0;
    }

    // Render tooltip and information and show the tooltip.
    public void ShowTooltip()
    {
        bool hasTooltipText = RenderTooltipText();
        if (hasTooltipText)
        {
            _tooltipShowCoroutine = AnimateTooltipInCoroutine();
            StartCoroutine(_tooltipShowCoroutine);
        }
    }

    // Populate the tooltip text with info on the card's special effects.
    // Returns a boolean indicating whether the card actually *has* any tooltip text.
    private bool RenderTooltipText()
    {
        string[] cardDescWords = card.GetCardStats().cardDesc.Split(' ');
        string textToRender = ""; // Card to show on the tooltip.
        // Loop through every word in the card's description and look for the ones that
        // correspond to actual status effects.
        foreach (string w in cardDescWords)
        {
            // Sometimes, our cards have puncutation which prevents the function from finding them.
            string word = Regex.Replace(w, @"[^\w\s]", "");
            Status foundStatus = Globals.allStatuses.Find((status) => status.statusName == word);
            if (foundStatus != null)
            {
                // If we've found the status relating to the word, render it here.
                if (textToRender != "") { textToRender += "\n"; }
                textToRender += "<sprite name=\"" + foundStatus.name.ToLower() + "\"> <b>" + foundStatus.name + "</b>:\n" + foundStatus.statusDescription;
            }
            StatusFlavor foundStatusFlavor = Globals.allStatusFlavors.Find((statusFlavor) => statusFlavor.statusName == word);
            if (foundStatusFlavor != null)
            {
                // If we've found the status relating to the word, render it here.
                if (textToRender != "") { textToRender += "\n"; }
                if (foundStatusFlavor.statusIcon != null) { textToRender += "<sprite name=\"" + foundStatusFlavor.name.ToLower() + "\"> "; }
                textToRender += "<b>" + foundStatusFlavor.name + "</b>:\n" + foundStatusFlavor.statusDescription;
            }
        }
        tooltipText.text = textToRender;
        return textToRender != "";
    }

    // Animate the tooltip in.
    private IEnumerator AnimateTooltipInCoroutine()
    {
        tooltipParentObject.SetActive(true);
        float currTime = 0f;
        float timeToWait = 0.2f;
        CanvasGroup tooltipCanvasGroup = tooltipParentObject.GetComponent<CanvasGroup>();
        while (currTime < timeToWait)
        {
            currTime += Time.deltaTime;
            tooltipCanvasGroup.alpha = Mathf.Lerp(0, 1, currTime / timeToWait);
            yield return null;
        }
    }

    // Hide the tooltip object.
    public void HideTooltip()
    {
        if (_tooltipShowCoroutine != null)
        {
            StopCoroutine(_tooltipShowCoroutine);
        }
        tooltipParentObject.SetActive(false);
    }

    public IEnumerator CardAppearCoroutine(float timeInSeconds)
    {
        // Wait until the end of the frame, just in case there are some
        // updates to be done to the card's initial position.
        yield return new WaitForEndOfFrame();
        // Set all of the initial values of the card to be invisible.
        HideCardInstantly();
        // Calculate frames and initial values for linear interpolation.
        float frames = 0;
        float maxFrames = timeInSeconds * 60; // Max # of frames calculated by 60 frames per second!
        Vector3 targetPosition = CardObject.transform.localPosition;
        CardObject.transform.localPosition -= new Vector3(0, 103, 0);
        Vector3 CardObjectInitialPosition = CardObject.transform.localPosition;
        Vector3 initialVel = Vector3.zero;
        // We use Vector3.Distance because we use SmoothDamp to make the translation smoother.
        while (Vector3.Distance(CardObject.transform.position, targetPosition) > 0.01f)
        {
            if (CardObject == null) { yield break; }
            CardObject.transform.localPosition = Vector3.SmoothDamp(CardObject.transform.localPosition, targetPosition, ref initialVel, timeInSeconds);
            _cardCanvasGroup.alpha = Mathf.Lerp(0, 1, frames / maxFrames);
            frames++;
            yield return null;
        }
        // Make sure the card is completely shown.
        ShowCardInstantly();
    }

    public IEnumerator CardDisappearCoroutine(float timeInSeconds, CardAnimation cardAnimationType, Action codeToExecuteAfter)
    {
        // Wait until the end of the frame, just in case there are some
        // updates to be done to the card's initial position.
        yield return new WaitForEndOfFrame();
        // Set all of the initial values of the card to be invisible.
        ShowCardInstantly();
        _cardHoverController.allowDragging = false;
        // Calculate frames and initial values for linear interpolation.
        float frames = 0;
        float maxFrames = timeInSeconds * 60; // Max # of frames calculated by 60 frames per second!
        Vector3 targetScale = new Vector3(0, 0, 0); // CardAnimation.SHRINK
        Vector3 targetPosition = CardObject.transform.localPosition - new Vector3(0, 103, 0); // CardAnimation.TRANSLATE_DOWN
        Vector3 cardObjectInitialPosition = CardObject.transform.localPosition;
        Vector3 cardObjectInitialScale = CardObject.transform.localScale;
        while (frames < maxFrames)
        {
            if (CardObject == null) { yield break; }
            if (cardAnimationType == CardAnimation.SHRINK)
            {
                CardObject.transform.localScale = Vector3.Lerp(cardObjectInitialScale, targetScale, frames / maxFrames);
            }
            else if (cardAnimationType == CardAnimation.TRANSLATE_DOWN)
            {
                CardObject.transform.localPosition = Vector3.Lerp(cardObjectInitialPosition, targetPosition, frames / maxFrames);
            }
            _cardCanvasGroup.alpha = Mathf.Lerp(1, 0, frames / maxFrames);
            frames++;
            yield return null;
        }
        // Make sure the card is completely hidden.
        if (CardObject == null) { yield break; }
        HideCardInstantly();
        codeToExecuteAfter.Invoke();
    }

    // This function enables the functionality (playability) of this card.
    public void EnableInteractions()
    {
        _graphicRaycaster.enabled = true;
    }

    // This function disables the functionality (playability) of this card.
    public void DisableInteractions()
    {
        _graphicRaycaster.enabled = false;
    }

    // Enable the functionality of the card for the shop scene.
    public void EnableShopFunctionality()
    {
        tooltipParentObject.transform.localPosition = new Vector3(275, _tooltipInitialLocalPosition.y, 0);
        GetComponent<ShopCardHandler>().enabled = true;
    }

    public void EnableUpgradeFunctionality()
    {
        tooltipParentObject.transform.localPosition = new Vector3(275, _tooltipInitialLocalPosition.y, 0);
        GetComponent<UpgradeCardHandler>().enabled = true;
    }

    // Modify the behavior of this card based on mouse inputs.
    // By default, all fields are initialized to true.
    public void ModifyHoverBehavior(bool scaleOnHover, bool transformOnHover, bool allowDrag, bool sortTopOnHover)
    {
        _cardHoverController.scaleOnHover = scaleOnHover;
        _cardHoverController.transformOnHover = transformOnHover;
        _cardHoverController.sortTopOnHover = sortTopOnHover;
        _cardHoverController.allowDragging = allowDrag;
    }

}
