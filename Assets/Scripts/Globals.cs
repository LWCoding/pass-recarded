using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class Globals
{

    public static List<CardData> allCardData = new List<CardData>();
    public static List<CardData> allBehaviors = new List<CardData>(); // Cards specifically used by enemy
    public static List<Enemy> allEnemies = new List<Enemy>();
    public static List<HeroData> allHeroData = new List<HeroData>();
    public static List<Status> allStatuses = new List<Status>();
    public static List<StatusFlavor> allStatusFlavors = new List<StatusFlavor>();
    public static List<Relic> allRelics = new List<Relic>();
    public static List<Item> allItems = new List<Item>();
    public static List<Dialogue> allDialogues = new List<Dialogue>();
    public static List<CardEffect> allCardEffects = new List<CardEffect>();
    public static List<MapInfo> allMapInfo = new List<MapInfo>();
    private static bool globalsInitialized = false;

    private static void Initialize()
    {

        // Find all enemies in the "Enemies" folder (through Resources)
        // and add them to the `allEnemies` variable.
        allEnemies = Resources.LoadAll<Enemy>("ScriptableObjects/Enemies").ToList();

        // Find all heroes in the "Heroes" folder (through Resources)
        // and add them to the `allHeroData` variable.
        allHeroData = Resources.LoadAll<HeroData>("ScriptableObjects/Heroes").ToList();

        // Find all hero cards in their respective folders (from the heroes we found
        // in the allHeroData list) and then add all of the global cards to the `allCardData`
        // variable.
        allCardData = Resources.LoadAll<CardData>("ScriptableObjects/Cards").ToList();
        foreach (HeroData heroData in allHeroData)
        {
            foreach (CardData cardData in Resources.LoadAll<CardData>("ScriptableObjects/Cards/" + heroData.characterName).ToList())
            {
                if (allCardData.FindAll((c) => c.GetCardUniqueName() == cardData.GetCardUniqueName()).Count > 1)
                {
                    Debug.LogError("Conflicting unique card names found in card: " + cardData.GetCardUniqueName());
                }
                allCardData.Add(cardData);
            }
        }

        // Find all cards in the "Behaviors" folder (through Resources)
        // and add them to the `allBehaviors` variable. 
        allBehaviors = Resources.LoadAll<CardData>("ScriptableObjects/Behaviors").ToList();
        // Add these behaviors to the `allCards` list to be referenced.
        foreach (CardData behavior in allBehaviors)
        {
            if (allCardData.FindAll((c) => c.GetCardUniqueName() == behavior.GetCardUniqueName()).Count > 1)
            {
                Debug.LogError("Conflicting unique card names found in card: " + behavior.GetCardUniqueName());
            }
            allCardData.Add(behavior);
        }

        // Find all statuses in the "Statuses" folder (through Resources)
        // and add them to the `allStatuses` variable.
        allStatuses = Resources.LoadAll<Status>("ScriptableObjects/Statuses").ToList();

        // Find all statuses in the "StatusFlavors" folder (through Resources)
        // and add them to the `allStatusFlavors` variable.
        allStatusFlavors = Resources.LoadAll<StatusFlavor>("ScriptableObjects/StatusFlavors").ToList();

        // Find all relics in the "Relics" folder (through Resources)
        // and add them to the `allRelics` variable.
        allRelics = Resources.LoadAll<Relic>("ScriptableObjects/Relics").ToList();

        // Find all items in the "Items" folder (through Resources)
        // and add them to the `allItems` variable.
        allItems = Resources.LoadAll<Item>("ScriptableObjects/Items").ToList();

        // Find all card effects in the "CardEffects" folder (through Resources)
        // and add them to the `allCardEffects` variable.
        allCardEffects = Resources.LoadAll<CardEffect>("ScriptableObjects/CardEffects").ToList();

        // Find all dialogues in the "Dialogues" folder (through Resources)
        // and add them to the `allDialogues` variable.
        allDialogues = Resources.LoadAll<Dialogue>("ScriptableObjects/Dialogue").ToList();

        // Find all map information in the "MapInfo" folder (through Resources)
        // and add them to the `allMapInfo` variable.
        allMapInfo = Resources.LoadAll<MapInfo>("ScriptableObjects/MapInfo").ToList();

        // After everything is initialized, set to true.
        globalsInitialized = true;

    }

    // Gets a hero by name.
    public static Hero GetBaseHero(HeroTag ht)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        HeroData foundHeroData = null;
        allHeroData.ForEach((hero) =>
        {
            if (hero.heroTag == ht)
            {
                foundHeroData = hero;
                return;
            }
        });
        if (!foundHeroData)
        {
            Debug.Log("Could not find hero (" + ht + ") in Globals.cs!");
        }
        Hero hero = new Hero();
        hero.Initialize(foundHeroData);
        return hero;
    }

    // Gets a enemy by name.
    public static Enemy GetEnemy(string name)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        Enemy foundEnemy = null;
        allEnemies.ForEach((enemy) =>
        {
            if (enemy.characterName == name)
            {
                foundEnemy = enemy;
                return;
            }
        });
        if (!foundEnemy)
        {
            Debug.Log("Could not find enemy (" + name + ") in Globals.cs!");
        }
        return foundEnemy;
    }

    // Get an enemy encounter by location.
    // Include an optional encounter list that will filter out those already seen.
    // If there are no enemy choices, this function ignores the blacklist and chooses at random.
    public static Encounter GetEnemyEncounterByScene(MapScene scene, int floorNumber, bool minibossOnly, bool bossOnly, List<Encounter> encounterBlacklist = null)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        // If we can't find the map info, end it here.
        MapInfo foundMapInfo = allMapInfo.Find((mi) => mi.mapType == scene);
        if (foundMapInfo == null)
        {
            Debug.Log("Couldn't find map info with dictionary key (" + scene + ") in Globals.cs!");
            return null;
        }
        // Find all possible encounters that fit the search filter.
        List<Encounter> possibleEncounters = foundMapInfo.possibleEncounters.FindAll((encounter) =>
        {
            return floorNumber >= encounter.minFloorRequired && floorNumber < encounter.maxFloorLimit &&
                    encounter.isBoss == bossOnly && encounter.isMiniboss == minibossOnly;
        });
        if (encounterBlacklist != null)
        {
            // Get all relevant encounters from the list.
            List<Encounter> relevantBlacklist = encounterBlacklist.FindAll((encounter) =>
            {
                return encounter.isBoss == bossOnly && encounter.isMiniboss == minibossOnly;
            });
            // Remove all blacklisted encounters from possible encounter list and store separately.
            // Only set the original list to equal this IF the filtered list is not empty.
            List<Encounter> filteredEncounters = possibleEncounters.FindAll((encounter) => !encounterBlacklist.Contains(encounter));
            if (filteredEncounters.Count != 0)
            {
                possibleEncounters = filteredEncounters;
            }
        }
        return possibleEncounters[Random.Range(0, possibleEncounters.Count)];
    }

    public static Dialogue GetDialogueByMapInfoAndFloor(MapScene scene, int floor)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        // If we can't find the map info, end it here.
        MapInfo foundMapInfo = allMapInfo.Find((mi) => mi.mapType == scene);
        if (foundMapInfo == null)
        {
            Debug.Log("Couldn't find map info with dictionary key (" + scene + ") in Globals.cs!");
            return null;
        }
        // Try to look for the dialogue.
        // If we can't find it, return null. Or else, return the dialogue itself.
        DialogueByFloor dbf = foundMapInfo.mapDialogues.Find((dialogueByFloor) => dialogueByFloor.floorNumber == floor);
        return (dbf == null) ? null : dbf.mapDialogue;
    }

    // Gets a card by name.
    public static Card GetCard(string name, bool exceptionAllowed = false)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        CardData foundCardData = null;
        allCardData.ForEach((card) =>
        {
            if (card.GetCardUniqueName() == name)
            {
                foundCardData = card;
                return;
            }
        });
        if (!foundCardData && !exceptionAllowed)
        {
            Debug.Log("Could not find card (" + name + ") in Globals.cs!");
        }
        return new Card(foundCardData);
    }

    // Gets map info by scene.
    public static MapInfo GetMapInfo(MapScene mapScene)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        MapInfo foundMapInfo = null;
        allMapInfo.ForEach((mapInfo) =>
        {
            if (mapInfo.mapType == mapScene)
            {
                foundMapInfo = mapInfo;
                return;
            }
        });
        if (!foundMapInfo)
        {
            Debug.Log("Could not find map info (" + mapScene + ") in Globals.cs!");
        }
        return foundMapInfo;
    }

    // Gets a status effect by effect type.
    public static StatusEffect GetStatus(Effect type, int turnCount = 0, string specialValue = "na")
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        Status foundStatus = null;
        allStatuses.ForEach((status) =>
        {
            if (status.type == type)
            {
                foundStatus = status;
                return;
            }
        });
        if (!foundStatus)
        {
            Debug.Log("Could not find status (" + type + ") in Globals.cs!");
        }
        return new StatusEffect(foundStatus, turnCount, specialValue);
    }

    // Gets a relic by relic type.
    public static Relic GetRelic(RelicType type)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        Relic foundRelic = null;
        allRelics.ForEach((relic) =>
        {
            if (relic.type == type)
            {
                foundRelic = relic;
                return;
            }
        });
        if (!foundRelic)
        {
            Debug.Log("Could not find relic (" + type + ") in Globals.cs!");
        }
        return foundRelic;
    }

    // Gets a card effect by caard effect type.
    public static CardEffect GetCardEffect(CardEffectType type)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        CardEffect foundCardEffect = null;
        allCardEffects.ForEach((cardEffect) =>
        {
            if (cardEffect.type == type)
            {
                foundCardEffect = cardEffect;
                return;
            }
        });
        if (!foundCardEffect)
        {
            Debug.Log("Could not find relic (" + type + ") in Globals.cs!");
        }
        return foundCardEffect;
    }

    public static Dialogue GetDialogue(DialogueName type)
    {
        if (!globalsInitialized)
        {
            Initialize();
        }
        Dialogue foundDialogue = null;
        allDialogues.ForEach((dialogue) =>
        {
            if (dialogue.dialogueName == type)
            {
                foundDialogue = dialogue;
                return;
            }
        });
        if (!foundDialogue)
        {
            Debug.Log("Could not find dialogue (" + type + ") in Globals.cs!");
        }
        return foundDialogue;
    }

}
