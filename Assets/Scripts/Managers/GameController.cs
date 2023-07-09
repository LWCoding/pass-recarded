using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameController
{

    private static Hero _chosenHero;
    public static void SetChosenHero(Hero h) => _chosenHero = h;
    public static List<Card> GetHeroCards() => _chosenHero.currentDeck;
    public static List<Relic> GetRelics() => _chosenHero.currentRelics;
    public static HeroData GetHeroData() => _chosenHero.heroData;
    public static int GetHeroHealth() => _chosenHero.currentHealth;
    public static int GetHeroMaxHealth() => _chosenHero.maxHealth;
    private static int _money;
    public static int GetMoney() => _money;
    public static void SetMoney(int m) => _money = m;
    public static void AddMoney(int m) => _money += m;
    public static void SpendMoney(int m) => AddMoney(-m);
    private static int _xp;
    public static int GetXP() => _xp;
    public static void SetXP(int x) => _xp = Mathf.Min(100, x);
    public static void AddXP(int x) => _xp = Mathf.Min(100, _xp + x);
    public static void SpendXP(int x) => AddXP(-x);
    public static List<Enemy> nextBattleEnemies = new List<Enemy>();
    public static bool visitedShopBefore = false;
    // Dialogue data:
    public static List<DialogueName> alreadyPlayedMapDialogues = new List<DialogueName>();
    public static List<string> alreadyPlayedTutorials = new List<string>();
    // Map data:
    private static MapScene _mapScene;
    public static MapScene GetMapScene() => _mapScene;
    public static void SetMapScene(MapScene ms) => _mapScene = ms;
    private static SerializableMapObject _mapObject = null;
    public static SerializableMapObject GetMapObject() => _mapObject;
    public static void SetMapObject(SerializableMapObject smo) => _mapObject = smo;
    private static List<Encounter> _alreadySeenEnemies = new List<Encounter>();
    public static List<Encounter> GetSeenEnemies() => _alreadySeenEnemies;
    public static void AddSeenEnemies(Encounter enemyEncounter) => _alreadySeenEnemies.Add(enemyEncounter);
    public static void SetSeenEnemies(List<Encounter> seenEnemies) => _alreadySeenEnemies = seenEnemies;
    // Cards and deck data:
    private const float COMMON_CARD_CHANCE = 0.6f;
    private const float UNCOMMON_CARD_CHANCE = 0.3f;
    private const float RARE_CARD_CHANCE = 0.1f;
    private const float COMMON_RELIC_CHANCE = 0.7f;
    private const float UNCOMMON_RELIC_CHANCE = 0.2f;
    private const float RARE_RELIC_CHANCE = 0.1f;

    // Adds a card to the deck.
    public static void AddCardToDeck(Card card)
    {
        _chosenHero.currentDeck.Add(card);
    }

    // Adds a relic to the player's inventory, if they don't have it already.
    public static void AddRelicToInventory(Relic relic)
    {
        if (HasRelic(relic.type)) { return; }
        _chosenHero.currentRelics.Add(relic);
    }

    public static void SetPlayedDialogues(List<DialogueName> dialogues, List<string> tutorials, bool hasVisitedShop)
    {
        alreadyPlayedMapDialogues = dialogues;
        alreadyPlayedTutorials = tutorials;
        visitedShopBefore = hasVisitedShop;
    }

    public static void SaveGame()
    {
        SaveObject so = new SaveObject();
        so.hero = _chosenHero;
        so.money = GetMoney();
        so.mapObject = GetMapObject();
        so.seenEnemies = GetSeenEnemies();
        so.xp = GetXP();
        so.mapDialoguesPlayed = alreadyPlayedMapDialogues;
        so.tutorialsPlayed = alreadyPlayedTutorials;
        so.visitedShopBefore = visitedShopBefore;
        SaveLoadManager.Save(so);
    }

    /// <summary>
    /// Returns a random card from the pool of all cards, based on the chances of card rarities.
    /// Excludes any cards that are in the provided blacklist. 
    /// If no valid cards are found, returns null.
    /// CANNOT return unobtainable cards.
    /// </summary>
    public static Card GetRandomCard(List<Card> cardBlacklist, float commonChance = -1, float uncommonChance = -1, float rareChance = -1)
    {
        if (commonChance == -1) { commonChance = COMMON_CARD_CHANCE; }
        if (uncommonChance == -1) { uncommonChance = UNCOMMON_CARD_CHANCE; }
        if (rareChance == -1) { rareChance = RARE_CARD_CHANCE; }
        // Calculate if the cards drawn should be common, uncommon, or rare.
        CardRarity rarity;
        float randomCalc = Random.Range(0f, 1f);
        if (randomCalc < commonChance)
        {
            rarity = CardRarity.COMMON;
        }
        else if (randomCalc < commonChance + uncommonChance)
        {
            rarity = CardRarity.UNCOMMON;
        }
        else
        {
            rarity = CardRarity.RARE;
        }
        List<CardData> possibleCards = Globals.allCardData.FindAll((card) => (card.cardRarity == rarity && (card.cardExclusivity == HeroTag.ANY_HERO || card.cardExclusivity == GetHeroData().heroTag)));
        // Remove any duplicates. Let the player choose only unique
        // cards from the possibleCards array.
        for (int i = 0; i < cardBlacklist.Count; i++)
        {
            possibleCards.Remove(cardBlacklist[i].cardData);
        }
        // If there are no possible cards to draw, don't draw any!
        if (possibleCards.Count == 0)
        {
            Debug.Log("No possible card found when trying to draw in GameController.cs!");
            return null;
        }
        int randomIdx = Random.Range(0, possibleCards.Count);
        return new Card(possibleCards[randomIdx]);
    }

    /// <summary>
    /// Returns a random relic from the pool of all relics, based on the chances of relic rarities.
    /// Excludes any relics that are in the provided blacklist. 
    /// If no valid relics are found, returns null.
    /// CANNOT return unobtainable relics.
    /// </summary>
    public static Relic GetRandomUnownedRelic(List<Relic> relicBlacklist, float commonChance = -1, float uncommonChance = -1, float rareChance = -1)
    {
        if (commonChance == -1) { commonChance = COMMON_RELIC_CHANCE; }
        if (uncommonChance == -1) { uncommonChance = UNCOMMON_RELIC_CHANCE; }
        if (rareChance == -1) { rareChance = RARE_RELIC_CHANCE; }
        // Get a full list of non-unobtainable and non-placeholder relics.
        List<Relic> possibleRelicsToAchieve = Globals.allRelics.FindAll((relic) => relic.relicRarity != RelicRarity.UNOBTAINABLE && relic.relicRarity != RelicRarity.PLACEHOLDER);
        // Remove any relics the player already has from the pool of possible relics OR
        // are in the provided blacklist.
        for (int i = 0; i < _chosenHero.currentRelics.Count; i++)
        {
            possibleRelicsToAchieve.Remove(_chosenHero.currentRelics[i]);
        }
        for (int i = 0; i < relicBlacklist.Count; i++)
        {
            possibleRelicsToAchieve.Remove(relicBlacklist[i]);
        }
        // If there are no relics to achieve, give them the question mark relic.
        if (possibleRelicsToAchieve.Count == 0)
        {
            return Globals.allRelics.Find((relic) => (relic.relicRarity == RelicRarity.PLACEHOLDER));
        }
        // Sum up the probabilities of getting each relic.
        float relicProbabilitySum = 0;
        foreach (Relic relic in possibleRelicsToAchieve)
        {
            switch (relic.relicRarity)
            {
                case RelicRarity.COMMON:
                    relicProbabilitySum += commonChance;
                    break;
                case RelicRarity.UNCOMMON:
                    relicProbabilitySum += uncommonChance;
                    break;
                case RelicRarity.RARE:
                    relicProbabilitySum += rareChance;
                    break;
                default:
                    break;
            }
        }
        // Get a random number within the range, and map that to a relic.
        float randomIdx = Random.Range(0, relicProbabilitySum);
        foreach (Relic relic in possibleRelicsToAchieve)
        {
            switch (relic.relicRarity)
            {
                case RelicRarity.COMMON:
                    randomIdx -= commonChance;
                    break;
                case RelicRarity.UNCOMMON:
                    randomIdx -= uncommonChance;
                    break;
                case RelicRarity.RARE:
                    randomIdx -= rareChance;
                    break;
                default:
                    break;
            }
            if (randomIdx <= 0) { return relic; }
        }
        Debug.Assert(false); // This should never be null.
        return null;
    }

    /// <summary>
    /// Returns true if the player has the relic. Or else, returns false.
    /// </summary>
    public static bool HasRelic(RelicType relicType)
    {
        foreach (Relic r in _chosenHero.currentRelics)
        {
            if (r.type == relicType)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Changes the chosen hero's health.
    /// </summary>
    public static void ChangeHeroHealth(int health)
    {
        _chosenHero.currentHealth += health;
        _chosenHero.currentHealth = Mathf.Min(_chosenHero.currentHealth, _chosenHero.maxHealth);
        TopBarController.Instance.UpdateHealthText(_chosenHero.currentHealth, _chosenHero.maxHealth);
    }

    /// <summary>
    /// Sets the chosen hero's health.
    /// </summary>
    public static void SetHeroHealth(int health)
    {
        _chosenHero.currentHealth = health;
        _chosenHero.currentHealth = Mathf.Min(_chosenHero.currentHealth, _chosenHero.maxHealth);
        TopBarController.Instance.UpdateHealthText(_chosenHero.currentHealth, _chosenHero.maxHealth);
    }

}
