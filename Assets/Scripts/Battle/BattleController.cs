using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public enum GameState
{
    IN_MENU, PLAYER_TURN, ENEMY_TURN, GAME_OVER
}

[System.Serializable]
public class SpawnableEnemyLocation
{
    public Vector3 position;
    public bool isTaken;
}

public partial class BattleController : MonoBehaviour
{

    public static BattleController Instance;
    [Header("Prefab Assignments")]
    [SerializeField] private GameObject enemyPrefabObject;
    [Header("Object Assignments")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI drawText;
    [SerializeField] private TextMeshProUGUI discardText;
    [SerializeField] private Transform deckParentTransform;
    [SerializeField] private Button _drawPileButton;
    [SerializeField] private Button _discardPileButton;

    [HideInInspector] public int enemiesStillAlive = 0;
    [SerializeField] private List<SpawnableEnemyLocation> _spawnableEnemyLocations = new List<SpawnableEnemyLocation>();
    public SpawnableEnemyLocation GetNextAvailableEnemyLocation() => _spawnableEnemyLocations.Find((loc) => !loc.isTaken);
    public void TakeUpEnemyLocation(Vector3 pos) => _spawnableEnemyLocations.Find((loc) => loc.position == pos).isTaken = true;
    public void FreeUpEnemyLocation(Vector3 pos) => _spawnableEnemyLocations.Find((loc) => loc.position == pos).isTaken = false;

    private GameState _gameState;
    public void ChangeGameState(GameState newState) => _gameState = newState;
    public GameState GetGameState() => _gameState;
    private UnityEvent OnNextTurnStart = new UnityEvent();
    private UnityEvent OnTurnEnd = new UnityEvent();
    [HideInInspector] public BattleHeroController playerBCC;
    [HideInInspector] public List<BattleEnemyController> enemyBCCs = new List<BattleEnemyController>();
    [HideInInspector] public List<Card> cardsInDiscard = new List<Card>();
    [HideInInspector] public List<Card> cardsInDrawPile = new List<Card>();

    private List<Card> _cardsInHand = new List<Card>();
    private List<GameObject> _cardObjectsInHand = new List<GameObject>();
    private int _turnNumber; // To help with Enemy AI calculations

    private const int MAX_ALLOWED_CARDS_IN_HAND = 8;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
        }
        Instance = this;
        _turnNumber = 0;
        ChangeGameState(GameState.PLAYER_TURN);
        // Make the screen refresh cards if the screen is resized.
        ResizeAspectRatioController.OnScreenResize -= () =>
        {
            if (GetGameState() == GameState.PLAYER_TURN) { UpdateCardsInHand(true); }
        };
        ResizeAspectRatioController.OnScreenResize += () =>
        {
            if (GetGameState() == GameState.PLAYER_TURN) { UpdateCardsInHand(true); }
        };
    }

    // Add logic that pertains to the battle actually functioning properly, outside
    // of player/enemy logic.
    private void SetListenersOnStart()
    {
        // Make energy updates change the displays of cards in the player's hand.
        EnergyController.Instance.OnEnergyChanged.AddListener((energy) =>
        {
            foreach (GameObject cardObject in _cardObjectsInHand)
            {
                CardHandler cardHandler = cardObject.GetComponent<CardHandler>();
                cardHandler.UpdateColorBasedOnPlayability();
            }
        });
    }

    private void Start()
    {
        // Set any listeners to be played in the start function.
        SetListenersOnStart();
        // Initialize the UI.
        GlobalUIController.Instance.InitializeUI();
        // Initialize the hero and all enemies!
        playerBCC = playerObject.GetComponent<BattleHeroController>();
        InitializeHero();
        foreach (Enemy e in GameController.nextBattleEnemies)
        {
            SpawnEnemy(e);
        }
        // Initialize the buttons.
        _drawPileButton.onClick.AddListener(() => TopBarController.Instance.ToggleCardOverlay(cardsInDrawPile, _drawPileButton));
        _discardPileButton.onClick.AddListener(() => TopBarController.Instance.ToggleCardOverlay(cardsInDiscard, _discardPileButton));
#if !UNITY_EDITOR
        // If we haven't played the battle tutorial yet, do that.
        if (!GameController.alreadyPlayedTutorials.Contains("Battle"))
        {
            TutorialController.Instance.StartTutorial();
            GameController.alreadyPlayedTutorials.Add("Battle");
        }
#endif
        // Initialize the background!
        BackgroundController.Instance.InitializeBG();
        // Make the game fade from black to clear.
        FadeTransitionController.Instance.ShowScreen(0.75f);
        // Play game music!
        SoundManager.Instance.PlayOnLoop(MusicType.BATTLE_MUSIC);
        // Evaluate logic that runs at the beginning of the game.
        RunOnBattleStart();
        // Start game loop!
        RunGameLoop();
    }

    // Initializes the hero's deck, sprite, and health.
    private void InitializeHero()
    {
        for (int i = 0; i < GameController.GetHeroCards().Count; i++)
        {
            CardData cardDataCopy = Instantiate(GameController.GetHeroCards()[i].cardData);
            Card cardCopy = new Card(cardDataCopy, GameController.GetHeroCards()[i].level);
            cardsInDiscard.Add(cardCopy);
        }
        // Set the rest of the PlayerBCC values.
        playerBCC.Initialize(GameController.GetHeroData(), GameController.GetHeroHealth(), GameController.GetHeroMaxHealth());
    }

    // Initializes an enemy object.
    public void SpawnEnemy(Enemy enemyData)
    {
        // Don't let the game spawn more than three enemies at a time.
        if (enemiesStillAlive > 3) { return; }
        // Initialize this enemy based on the Enemy scriptable object data.
        GameObject enemyObject = Instantiate(enemyPrefabObject);
        BattleEnemyController bec = enemyObject.GetComponent<BattleEnemyController>();
        enemiesStillAlive++;
        enemyBCCs.Add(bec);
        // Initialize the rest of the enemy's information.
        int generatedHealth = Random.Range(enemyData.enemyHealthMin, enemyData.enemyHealthMax + 1);
        switch (enemyData.characterName)
        {
            case "Treevil":
                bec.AddStatusEffect(Globals.GetStatus(Effect.GROWTH, 1));
                break;
            case "Lone Fox":
                bec.AddStatusEffect(Globals.GetStatus(Effect.BARRIER, 3));
                break;
            case "Turtleist":
                bec.AddStatusEffect(Globals.GetStatus(Effect.VOLATILE, 4));
                break;
        }
        bec.SetRewardAmount(Random.Range(enemyData.enemyRewardMin, enemyData.enemyRewardMax));
        SpawnableEnemyLocation enemyLocationToSpawnAt = GetNextAvailableEnemyLocation();
        TakeUpEnemyLocation(enemyLocationToSpawnAt.position);
        bec.gameObject.transform.position = enemyLocationToSpawnAt.position;
        bec.InitializeHealthData(generatedHealth, generatedHealth);
        bec.Initialize(enemyData);
        bec.SetEnemyType(enemyData);
    }

    // Sometimes there are some initial actions we want to perform at the very
    // start of the battle. Do those here.
    private void RunOnBattleStart()
    {
        // If the character has the SCALE OF JUSTICE relic, they can play their first card twice.
        if (GameController.HasRelic(RelicType.SCALE_OF_JUSTICE))
        {
            playerBCC.AddStatusEffect(Globals.GetStatus(Effect.DOUBLE_TAKE, 1));
            TopBarController.Instance.FlashRelicObject(RelicType.SCALE_OF_JUSTICE);
        }
        // If the character has the PLASMA CORE relic, they gain one additional max energy.
        if (GameController.HasRelic(RelicType.PLASMA_CORE))
        {
            EnergyController.Instance.UpdateMaxEnergy(1);
            TopBarController.Instance.FlashRelicObject(RelicType.PLASMA_CORE);
        }
        // If the character has the DUMBELL relic, they gain one additional Strength.
        if (GameController.HasRelic(RelicType.DUMBELL))
        {
            playerBCC.AddStatusEffect(Globals.GetStatus(Effect.STRENGTH, 1));
            TopBarController.Instance.FlashRelicObject(RelicType.DUMBELL);
        }
        // If the character has the DUMBELL relic, they gain one additional Defense.
        if (GameController.HasRelic(RelicType.KEVLAR_VEST))
        {
            playerBCC.AddStatusEffect(Globals.GetStatus(Effect.DEFENSE, 1));
            TopBarController.Instance.FlashRelicObject(RelicType.KEVLAR_VEST);
        }
        // If the character has the GRAPPLING HOOK relic, they draw one additional card per turn.
        if (GameController.HasRelic(RelicType.GRAPPLING_HOOK))
        {
            playerBCC.AddStatusEffect(Globals.GetStatus(Effect.LUCKY_DRAW, 1));
            TopBarController.Instance.FlashRelicObject(RelicType.GRAPPLING_HOOK);
        }

        // If the player has the Green Scarf relic, remove combo if the card isn't identical.
        if (GameController.HasRelic(RelicType.GREEN_SCARF))
        {
            playerBCC.OnPlayCard.AddListener((c) =>
            {
                StatusEffect combo = playerBCC.GetStatusEffect(Effect.COMBO);
                if (combo != null && c.cardData.GetCardDisplayName() != combo.specialValue)
                {
                    playerBCC.RemoveStatusEffect(Effect.COMBO);
                }
            });
        }
        // If the player has the Green Scarf relic, build up combos for every attack card.
        if (GameController.HasRelic(RelicType.GREEN_SCARF))
        {
            playerBCC.OnPlayedCard.AddListener((c) =>
            {
                StatusEffect combo = playerBCC.GetStatusEffect(Effect.COMBO);
                if (c.cardData.cardType == CardType.ATTACKER || c.cardData.cardType == CardType.SPECIAL_ATTACKER)
                {
                    if (combo == null || c.cardData.GetCardDisplayName() == combo.specialValue)
                    {
                        playerBCC.AddStatusEffect(Globals.GetStatus(Effect.COMBO, 2, c.cardData.GetCardDisplayName()));
                        TopBarController.Instance.FlashRelicObject(RelicType.GREEN_SCARF);
                    }
                }
            });
        }
        // If the player has the The Thinker relic, deal 1 damage to enemies for every card.
        if (GameController.HasRelic(RelicType.THE_THINKER))
        {
            playerBCC.OnPlayCard.AddListener((c) =>
            {
                foreach (BattleCharacterController bcc in enemyBCCs)
                {
                    bcc.ChangeHealth(-1);
                }
                TopBarController.Instance.FlashRelicObject(RelicType.THE_THINKER);
            });
        }
        // If the player has the Vampire Teeth relic, killing an enemy should heal 3 health.
        if (GameController.HasRelic(RelicType.VAMPIRE_FANGS))
        {
            foreach (BattleEnemyController bec in enemyBCCs)
            {
                bec.OnDeath.AddListener(() =>
                {
                    playerBCC.ChangeHealth(3);
                    TopBarController.Instance.FlashRelicObject(RelicType.VAMPIRE_FANGS);
                });
            }
        }
        // If the player has the Airhorn relic, all enemies start with 1 crippled.
        if (GameController.HasRelic(RelicType.AIRHORN))
        {
            foreach (BattleEnemyController bec in enemyBCCs)
            {
                bec.AddStatusEffect(Globals.GetStatus(Effect.CRIPPLED, 1));
                TopBarController.Instance.FlashRelicObject(RelicType.AIRHORN);
            }
        }
        // If the player has the Catastrophe status effect, deal X damage to all enemies
        // when a card is played.
        playerBCC.OnPlayCard.AddListener((card) =>
        {
            StatusEffect catastropheEffect = playerBCC.GetStatusEffect(Effect.CATASTROPHE);
            if (catastropheEffect != null)
            {
                foreach (BattleEnemyController bec in enemyBCCs)
                {
                    bec.ChangeHealth(-catastropheEffect.amplifier);
                }
            }
        });
        playerBCC.OnPlayedCard.AddListener((e) => UpdateAllCardDescriptions());
    }

    private void RunGameLoop()
    {
        StartCoroutine(RunGameLoopCoroutine());
    }

    private IEnumerator RunGameLoopCoroutine()
    {
        yield return new WaitForEndOfFrame();
        _turnNumber++;
        // Sets energy to max energy.
        EnergyController.Instance.RestoreEnergy();
        EnergyController.Instance.EnergyGlow();
        // Run the turn start logic function for player and enemies.
        playerBCC.TurnStartLogic(_turnNumber);
        foreach (BattleEnemyController bec in enemyBCCs)
        {
            if (!bec.IsAlive()) { continue; }
            bec.TurnStartLogic(_turnNumber);
            bec.GenerateNextMove(_turnNumber);
        }
        // Update energy and health text values.
        EnergyController.Instance.UpdateEnergyText();
        // Draw random cards from the draw pile.
        // Potentially draw more from Lucky Draw effect.
        StatusEffect luckyDraw = playerBCC.GetStatusEffect(Effect.LUCKY_DRAW);
        int cardsToDraw = 5 + ((luckyDraw != null) ? luckyDraw.amplifier : 0);
        // If it's the first turn, check if the player has any Cheat Cards.
        // If yes, then decrement the cards to draw and draw one of these at random.
        if (_turnNumber == 1)
        {
            List<Card> cheatCards = GameController.GetHeroCards().FindAll((card) =>
            {
                return card.HasTrait(Trait.CHEAT_CARD);
            });
            // If we have any cheat cards, draw them into the hand one at a time.
            // Don't let the player draw more than five cards.
            while (cardsToDraw > 0 && cheatCards.Count > 0)
            {
                int randomIdx = Random.Range(0, cheatCards.Count);
                Card cheatCard = cheatCards[randomIdx];
                cheatCards.RemoveAt(randomIdx);
                DrawCards(new List<Card> { cheatCard });
                cardsToDraw--;
            }
        }
        yield return DrawCardsCoroutine(cardsToDraw);
        // Run after additional code.
        OnNextTurnStart.Invoke();
        OnNextTurnStart = new UnityEvent();
        // Allow the player to use the end turn button again.
        if (endTurnButton != null)
        {
            endTurnButton.interactable = true;
        }
    }

    // This function runs when the player clicks the
    // END TURN button, or if the turn is forcefully
    // ended.
    public void EndTurn()
    {
        endTurnButton.interactable = false;
        StartCoroutine(EndTurnCoroutine());
    }

    private IEnumerator EndTurnCoroutine()
    {
        // Run turn end function.
        OnTurnEnd.Invoke();
        OnTurnEnd = new UnityEvent();
        // Animate cards in hand and move them ot the discard.
        // Remove all cards in hand, and add them to the discard.
        int numCardsInHand = _cardsInHand.Count;
        for (int i = numCardsInHand - 1; i >= 0; i--)
        {
            cardsInDiscard.Add(_cardsInHand[i]);
            int cardIdx = i; // This is necessary because the coroutine doesn't save the current index
            StartCoroutine(_cardObjectsInHand[cardIdx].GetComponent<CardHandler>().CardDisappearCoroutine(0.25f, CardAnimation.TRANSLATE_DOWN, () =>
            {
                BattlePooler.Instance.ReturnCardObjectToPool(_cardObjectsInHand[cardIdx]);
                _cardsInHand.RemoveAt(cardIdx);
                _cardObjectsInHand.RemoveAt(cardIdx);
            }));
            UpdateDrawDiscardTexts();
            yield return new WaitForSeconds(0.04f);
        }
        yield return new WaitForSeconds(0.5f);
        // Make the enemy make a move based on the selected algorithm.
        // This is handled in the partial class `BattleController_AI`.
        // Loop ONLY through original enemies. Not summoned ones mid-way.
        List<BattleEnemyController> originalEnemyBCCs = new List<BattleEnemyController>(enemyBCCs);
        foreach (BattleEnemyController bec in originalEnemyBCCs)
        {
            if (!bec.IsAlive()) { continue; }
            yield return bec.PlayCard(bec.GetStoredCard(), new List<BattleCharacterController>() { playerBCC });
        }
        // Run the turn end logic for both the player and the enemy.
        yield return new WaitForSeconds(0.25f);
        playerBCC.TurnEndLogic();
        foreach (BattleEnemyController bec in enemyBCCs)
        {
            if (!bec.IsAlive()) { continue; }
            bec.TurnEndLogic();
        }
        yield return new WaitForSeconds(0.25f);
        // Re-run the game loop!
        RunGameLoop();
    }

    // Inflict a random card in the player's hand with an effect.
    public void InflictRandomCardWithEffect(CardEffectType type)
    {
        switch (type)
        {
            case CardEffectType.POISON:
                playerBCC.FlashColor(new Color(0, 1, 0), true);
                playerBCC.DamageShake(1, 1);
                break;
        }
        OnNextTurnStart.AddListener(() =>
        {
            // Find all cards that do not have the specified effect.
            List<GameObject> cardsWithoutEffect = _cardObjectsInHand.FindAll((c) => !c.GetComponent<CardHandler>().HasCardEffect(type));
            // If no cards without the effect exist, stop here.
            if (cardsWithoutEffect.Count == 0) { return; }
            // Or else, get that card's CardHandler and inflict the status.
            CardHandler randomCardHandler = cardsWithoutEffect[Random.Range(0, cardsWithoutEffect.Count)].GetComponent<CardHandler>();
            randomCardHandler.InflictCardEffect(type);
        });
    }

    // Renders all cards in the hand based on the _cardsInHand list.
    // Creates any new card objects for cards which don't have object representations.
    // If shouldBeInteractable is set to true, cards will immediately be useable.
    public void UpdateCardsInHand(bool shouldBeInteractable = false)
    {
        int totalCards = _cardsInHand.Count;
        float rotationDifference = 7;
        float positionDifference = 140;
        float verticalDifference = 15;
        float canvasScale = GameObject.Find("Canvas").transform.localScale.x;
        float squeezeAmount = 6 - (18 * (canvasScale - 1)); // Squeeze factor. Cards are more close together with more cards.
        float startRotation = ((totalCards % 2 == 0) ? -Mathf.Abs(-(Mathf.Floor(totalCards / 2) - 0.5f)) : -Mathf.Abs(-Mathf.Floor(totalCards / 2))) * rotationDifference;
        float startPosition = ((totalCards % 2 == 0) ? -Mathf.Abs(-(Mathf.Floor(totalCards / 2) - 0.5f)) : -Mathf.Abs(-Mathf.Floor(totalCards / 2))) * positionDifference;
        // Initialize every card.
        for (int i = 0; i < totalCards; i++)
        {
            GameObject cardObject = _cardObjectsInHand[i];
            CardHandler cardHandler = cardObject.GetComponent<CardHandler>();
            cardObject.GetComponent<Canvas>().sortingOrder = i;
            cardObject.transform.localScale = new Vector2(0.5f, 0.5f);
            cardObject.transform.rotation = Quaternion.identity;
            cardObject.transform.Rotate(0, 0, -1 * (startRotation + rotationDifference * i));
            float squeezeTogether = (totalCards % 2 == 0) ? (i - (Mathf.Floor(totalCards / 2) - 0.5f)) * (squeezeAmount * totalCards) : (i - Mathf.Floor(totalCards / 2)) * (squeezeAmount * totalCards);
            cardObject.transform.position = deckParentTransform.position;
            cardObject.transform.position += new Vector3(startPosition + i * positionDifference - squeezeTogether, ((totalCards % 2 == 0) ? -Mathf.Abs(-(Mathf.Floor(totalCards / 2) - 0.5f) + i) : -Mathf.Abs(-Mathf.Floor(totalCards / 2) + i)) * verticalDifference, 0);
            if (shouldBeInteractable)
            {
                cardHandler.EnableInteractions();
            }
            else
            {
                cardHandler.DisableInteractions();
            }
            cardHandler.InitializeStartingData();
        }
    }

    private void InitializeNewCardObject(Card card)
    {
        GameObject cardObject = BattlePooler.Instance.GetCardObjectFromPool(deckParentTransform);
        cardObject.GetComponent<CardHandler>().Initialize(card, true, playerBCC.CalculateDamageModifiers(card), playerBCC.CalculateDefenseModifiers());
        cardObject.GetComponent<CardHandler>().ModifyHoverBehavior(true, true, true, true);
        _cardObjectsInHand.Add(cardObject);
    }

    /// <summary>
    /// Shuffle cards from the draw pile into the player's hand, along with
    /// some animations.
    /// </summary>
    public void DrawCards(int count)
    {
        StartCoroutine(DrawCardsCoroutine(count));
    }

    /// <summary>
    /// Takes a list of cards and shuffles those cards into the player's hand,
    /// along with some animations.
    /// </summary>
    public void DrawCards(List<Card> cardsToDraw)
    {
        StartCoroutine(DrawCardsCoroutine(cardsToDraw.Count, cardsToDraw));
    }

    private IEnumerator DrawCardsCoroutine(int count, List<Card> cardsToDraw = null)
    {
        for (int i = 0; i < count; i++)
        {
            // If the player won or lost the game, don't draw cards!
            if (GetGameState() == GameState.GAME_OVER)
            {
                yield break;
            }
            if (cardsToDraw == null)
            {
                // Get a card in the draw pile and add it to the hand.
                ShuffleRandomCardToHand();
            }
            else
            {
                // If there are cards to draw, draw from those instead.
                AddCardToHand(cardsToDraw[i]);
            }
            // Render the cards on the screen, and update the numbers.
            UpdateCardsInHand();
            UpdateDrawDiscardTexts();
            yield return new WaitForSeconds(0.1f);
        }
        // Allow the user to mess with cards.
        EnableInteractionsForCardsInHand();
    }

    // Allows the players to use cards in their hand.
    public void DisableInteractionsForCardsInHand()
    {
        // Allow the user to mess with cards.
        for (int i = 0; i < _cardObjectsInHand.Count; i++)
        {
            CardHandler cardHandler = _cardObjectsInHand[i].GetComponent<CardHandler>();
            cardHandler.DisableInteractions();
        }
    }

    // Allows the players to use cards in their hand.
    public void EnableInteractionsForCardsInHand()
    {
        // Allow the user to mess with cards.
        for (int i = 0; i < _cardObjectsInHand.Count; i++)
        {
            CardHandler cardHandler = _cardObjectsInHand[i].GetComponent<CardHandler>();
            cardHandler.EnableInteractions();
        }
    }

    // Create a new card object and add it to the hand.
    // Does NOT re-render the hand automatically.
    public void AddCardToHand(Card card)
    {
        // If the player's hand is full, skip drawing a card.
        if (_cardsInHand.Count >= MAX_ALLOWED_CARDS_IN_HAND)
        {
            ObjectPooler.Instance.SpawnPopup("Hand is full!", 6, Vector3.zero, new Color(1, 0.1f, 0.1f), 1, 2f, false);
            return;
        }
        _cardsInHand.Add(card);
        InitializeNewCardObject(card);
    }

    // Adds the most recent card in the player's draw pile into
    // the player's hand.
    // Does NOT re-render the hand automatically.
    private void ShuffleRandomCardToHand()
    {
        // If the player's hand is full, skip drawing a card.
        if (_cardsInHand.Count >= MAX_ALLOWED_CARDS_IN_HAND)
        {
            ObjectPooler.Instance.SpawnPopup("Hand is full!", 6, Vector3.zero, new Color(1, 0.1f, 0.1f), 1, 2f, false);
            return;
        }
        // If there are no cards to draw, shuffle discard into draw!
        if (cardsInDrawPile.Count == 0)
        {
            if (cardsInDiscard.Count > 0)
            {
                ShuffleDiscardIntoDraw();
            }
            else
            {
                // If there are no cards to draw OR shuffle, skip.
                return;
            }
        }
        Card cardToAdd = cardsInDrawPile[0];
        AddCardToHand(cardToAdd);
        cardsInDrawPile.RemoveAt(0);
    }

    // Removes all of the cards from the hand and add
    // them to the discard pile.
    public void EmptyHand()
    {
        for (int i = _cardsInHand.Count - 1; i >= 0; i--)
        {
            cardsInDiscard.Add(_cardsInHand[i]);
            GameObject cardObject = _cardObjectsInHand[i];
            _cardsInHand.RemoveAt(i);
            _cardObjectsInHand.RemoveAt(i);
            BattlePooler.Instance.ReturnCardObjectToPool(cardObject);
            UpdateDrawDiscardTexts();
        }
    }

    public void ShuffleDiscardIntoDraw()
    {
        int discardSize = cardsInDiscard.Count;
        for (int j = 0; j < discardSize; j++)
        {
            // Get a random index in the discard pile and add it to the draw pile.
            int randomDiscIdx = Random.Range(0, cardsInDiscard.Count);
            cardsInDrawPile.Add(cardsInDiscard[randomDiscIdx]);
            cardsInDiscard.RemoveAt(randomDiscIdx);
        }
    }

    public void ShuffleCardsIntoDraw(List<Card> cards)
    {
        foreach (Card card in cards)
        {
            cardsInDrawPile.Insert(Random.Range(0, cardsInDrawPile.Count), card);
        }
        UpdateDrawDiscardTexts();
    }

    public void UseCardInHand(Card c, List<BattleCharacterController> collidingBCCs, bool subtractCost = true)
    {
        // Don't let the player use cards if it's not their turn.
        if (GetGameState() != GameState.PLAYER_TURN)
        {
            return;
        }
        // Update energy cost after using card, if applicable.
        if (subtractCost)
        {
            EnergyController.Instance.ChangeEnergy(-c.GetCardStats().cardCost);
        }
        // Play animations and perform actions specified on
        // card. (handled in BattleCharacterController)
        StartCoroutine(playerBCC.PlayCard(c, collidingBCCs));
        // Find card and move it to the discard pile.
        int idx = _cardsInHand.IndexOf(c);
        // If it can't find the object, that means that the
        // card was removed during its effects. Ignore deleting
        // it from the hand, then.
        if (idx == -1)
        {
            return;
        }
        GameObject cardObject = _cardObjectsInHand[idx];
        // If the card shouldn't exhaust, add it to the discard pile.
        if (!c.HasTrait(Trait.EXHAUST))
        {
            cardsInDiscard.Add(_cardsInHand[idx]);
        }
        _cardsInHand.RemoveAt(idx);
        _cardObjectsInHand.RemoveAt(idx);
        UpdateDrawDiscardTexts();
        BattlePooler.Instance.ReturnCardObjectToPool(cardObject);
        UpdateCardsInHand();
        // Allow the user to mess with cards.
        EnableInteractionsForCardsInHand();
    }

    public void UpdateDrawDiscardTexts()
    {
        drawText.text = cardsInDrawPile.Count.ToString();
        discardText.text = cardsInDiscard.Count.ToString();
    }

    // Triggers whenever the hero wins the fight. (All monsters go to zero health.)
    public void HandleBattleWin()
    {
        // If the player isn't alive, don't even consider the win logic.
        if (!playerBCC.IsAlive()) { return; }
        StartCoroutine(HandleBattleWinCoroutine());
    }

    private IEnumerator HandleBattleWinCoroutine()
    {
        if (GameController.HasRelic(RelicType.MEDKIT))
        {
            TopBarController.Instance.FlashRelicObject(RelicType.MEDKIT);
            playerBCC.ChangeHealth(4);
        }
        // Allot some time to animate the coins going to the player's balance.
        yield return new WaitForSeconds(1.4f);
        // Let the player add a new card to their deck (out of 3).
        CardChoiceController.Instance.ShowCardChoices(3, () =>
        {
            FadeTransitionController.Instance.HideScreen("Map", 0.75f);
        });
    }

    // Update the attack and defense values of all cards in your hand.
    public void UpdateAllCardDescriptions()
    {
        foreach (GameObject obj in _cardObjectsInHand)
        {
            CardHandler cardHandler = obj.GetComponent<CardHandler>();
            cardHandler.UpdateCardDescription(playerBCC.CalculateDamageModifiers(cardHandler.card), playerBCC.CalculateDefenseModifiers());
        }
    }

}
