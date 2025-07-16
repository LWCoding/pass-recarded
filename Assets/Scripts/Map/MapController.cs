using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class MapController : MonoBehaviour
{

    public static MapController Instance;
    [Header("Object Assignments")]
    public GameObject mapOptionPrefab;
    public Transform playerTransform;
    public Transform initialPlayerTransform;
    public SpriteRenderer playerIconSpriteRenderer;
    public Transform bossBattleTransform;
    public Transform mapParentObject;
    public TextMeshPro introBannerText;
    [Header("Image Assignments")]
    public Sprite mapEdgeSprite;
    public Sprite mapBlockSprite;
    [Header("Audio Assignments")]
    public AudioClip footstepsSFX;

    private SerializableMapObject _serializableMapObject;
    private int _currFloor = 0;
    private int _numFloors = 0;
    private MapInfo _currentMapInfo;
    private List<GameObject> _mapOptions = new List<GameObject>();
    private Dictionary<int, List<MapOptionController>> _mapOptionDictionary = new Dictionary<int, List<MapOptionController>>();

    private const float _yPositionDifference = 37.5f; // Distance between boss and player

    private void Awake()
    {
        Instance = GetComponent<MapController>();
        _currentMapInfo = Globals.GetMapInfo(GameController.GetMapScene());
    }

    private void Start()
    {
        // Initialize the UI.
        GlobalUIController.Instance.InitializeUI();
        // Initialize the total number of floors from the map info.
        _numFloors = _currentMapInfo.numFloors;
        // Initialize the map ONLY if they haven't already
        // been created.
        if (GameController.GetMapObject() == null)
        {
            InitializeMap();
            _serializableMapObject.currLocation.position = initialPlayerTransform.position;
            // Allow the player to move to the first row of options at the beginning of the game.
            foreach (MapOptionController moc in _mapOptionDictionary[1])
            {
                SerializableMapPath createdPath = new SerializableMapPath();
                createdPath.targetPosition = moc.transform.position;
                createdPath.initialPosition = initialPlayerTransform.position;
                _serializableMapObject.mapPaths.Add(createdPath);
            }
        }
        else
        {
            GenerateMapFromObject(GameController.GetMapObject());
        }
        // Initialize the boss battle info.
        GameObject bossBattleObj = _mapOptions.Find((obj) => obj.transform.position == bossBattleTransform.position);
        bossBattleObj.GetComponent<MapOptionController>().SetBossFloor(_numFloors + 1);
        // Initialize the current floor information.
        _currFloor = _serializableMapObject.currLocation.floorNumber;
        // Move the camera up to show the player.
        MapScroll.Instance.SetCameraPosition(new Vector3(0, playerTransform.position.y + 2, -10));
        // Make all map options and the show cards button uninteractable.
        DisableMapOptionColliders();
        // Make the game fade from black to clear.
        FadeTransitionController.Instance.ShowScreen(1.25f);
        // Play game music!
        SoundManager.Instance.PlayOnLoop(MusicType.MAP_MUSIC);
        // Allow the player to select an option.
        UpdateMapIconTransparencies();
        // If there is a dialogue to play, play it!
        // However, if the current floor is the first, play the camera panning animation before showing dialogue.
        Dialogue dialogueToPlay = Globals.GetDialogueByMapInfoAndFloor(_serializableMapObject.currScene, _currFloor);
        if (_currFloor == 0)
        {
            StartCoroutine(FirstTimeLoadCoroutine(dialogueToPlay));
        }
        else if (dialogueToPlay != null)
        {
            StartCoroutine(RenderDialogueCoroutine(dialogueToPlay, 0.8f));
        }
        else
        {
            EnableMapOptionColliders();
        }
        // Save the game.
        GameController.SetMapObject(_serializableMapObject);
        GameController.SaveGame();
    }

    // The first time a map is loaded, run the camera panning animation before
    // loading any actual dialogue.
    private IEnumerator FirstTimeLoadCoroutine(Dialogue dialogue)
    {
        switch (_serializableMapObject.currScene)
        {
            case MapScene.FOREST:
                introBannerText.text = "<color=\"black\"><size=13>The Forest</size></color>\n<color=#282E27><i><size=5>Chapter 1</size></i></color>";
                break;
            case MapScene.AERICHO_CITY:
                introBannerText.text = "<color=\"black\"><size=13>Aericho City</size></color>\n<color=#282E27><i><size=5>Chapter 2</size></i></color>";
                break;
            case MapScene.SECRET:
                introBannerText.text = "<color=\"black\"><size=13>The Secret</size></color>\n<color=#282E27><i><size=5>Hello from Selenium :)</size></i></color>";
                break;
        }
        yield return MapScroll.Instance.PanCameraAcrossMapCoroutine();
        // If we have a valid dialogue to render, render it. Or else
        // just let the player make their decision.
        if (dialogue != null)
        {
            yield return RenderDialogueCoroutine(dialogue, 0);
        }
        else
        {
            EnableMapOptionColliders();
        }
    }

    // This coroutine schedules the dialogue to be rendered into the queue
    // and then displays it on the screen.
    private IEnumerator RenderDialogueCoroutine(Dialogue dialogue, float delayBeforeInSecs)
    {
        yield return new WaitForSeconds(delayBeforeInSecs);
        // Queue the actual dialogue and play it.
        DialogueUIController.Instance.QueueDialogueText(dialogue);
        yield return DialogueUIController.Instance.RenderDialogueCoroutine(() =>
        {
            switch (dialogue.actionToPlayAfterDialogue)
            {
                case DialogueAction.NONE:
                    EnableMapOptionColliders();
                    break;
                case DialogueAction.HEAL_TO_FULL_HP:
                    GameController.SetHeroHealth(GameController.GetHeroMaxHealth());
                    SoundManager.Instance.PlaySFX(SoundEffect.HEAL_HEALTH);
                    EnableMapOptionColliders();
                    break;
                case DialogueAction.SECRET_WIN_SEND_TO_TITLE:
                    PlayerPrefs.SetInt("BeatBoykisser", 1);
                    FadeTransitionController.Instance.HideScreen("Title", 2);
                    break;
                case DialogueAction.WON_GAME_SEND_TO_TITLE:
                    PlayerPrefs.SetInt("BeatGame", 1);
                    FadeTransitionController.Instance.HideScreen("Title", 2);
                    break;
            }
        });
    }

    // This function creates the map background from the 
    // map edge and map block sprites we have.
    private void InitializeMapBG()
    {
        float mapScale = 2.8f;
        float mapGap = 5.00f;
        float edgeYBuffer = 3.03f;
        float mapVerticalShift = -1f;
        int numStacks = 9;
        // Create the edge pieces
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject mapEdge = new GameObject();
            mapEdge.AddComponent<SpriteRenderer>();
            mapEdge.GetComponent<SpriteRenderer>().sprite = mapEdgeSprite;
            mapEdge.transform.localScale = new Vector3(mapScale, i * mapScale, 1);
            mapEdge.transform.position = new Vector3(0, (i == 1) ? (numStacks - 1) * mapGap + edgeYBuffer : -edgeYBuffer, 0);
            mapEdge.transform.position += new Vector3(0, mapVerticalShift, 0);
        }
        // Create everything in between
        for (int i = 0; i < numStacks; i++)
        {
            GameObject mapSection = new GameObject();
            mapSection.AddComponent<SpriteRenderer>();
            mapSection.GetComponent<SpriteRenderer>().sprite = mapBlockSprite;
            mapSection.GetComponent<SpriteRenderer>().sortingOrder = -1;
            mapSection.transform.localScale = new Vector3(mapScale, mapScale, 1);
            mapSection.transform.position = new Vector3(0, i * mapGap, 0);
            mapSection.transform.position += new Vector3(0, mapVerticalShift, 0);
        }
    }

    // This function creates all of the choices on the map.
    // Instantiates them as objects and puts them into the
    // `mapParentObject` transform.
    // This should only be run ONCE if the map isn't already
    // loaded in.
    private void InitializeMap()
    {
        InitializeMapBG();
        // Set the color of the camera background
        Camera.main.backgroundColor = _currentMapInfo.mapBGColor;
        // Set the player's headshot sprite
        playerIconSpriteRenderer.sprite = GameController.GetHeroData().mapHeadshotSprite;
        // Create an empty serializable map object.
        _serializableMapObject = new SerializableMapObject();
        _serializableMapObject.currScene = _currentMapInfo.mapType;

        int numOptions = 3; // Static, unchangeable. Must reformat code to add more.
        float xPositionDifference = 2.5f;
        float yPositionDifference = _yPositionDifference / _numFloors;
        float positionVariation = 0.6f;
        float numSpawns = numOptions + 2; // Add to incorporate obstacles.
        float startPosition = ((numSpawns % 2 == 0) ? -Mathf.Abs(-(Mathf.Floor(numSpawns / 2) - 0.5f)) : -Mathf.Abs(-Mathf.Floor(numSpawns / 2))) * xPositionDifference;
        float outsideObstacleDistance = xPositionDifference * numOptions;
        // Spawn a few more obstacles around Jack.
        for (int i = -1; i <= 1; i += 2)
        {
            if (i == 0) { continue; } // Don't spawn one directly on Jack.
            GameObject outskirtObstacleObject = Instantiate(mapOptionPrefab);
            outskirtObstacleObject.transform.SetParent(mapParentObject);
            outskirtObstacleObject.transform.position = mapParentObject.position;
            outskirtObstacleObject.transform.position += new Vector3(i * xPositionDifference, initialPlayerTransform.position.y, 0);
            outskirtObstacleObject.transform.position += new Vector3(Random.Range(-positionVariation, positionVariation) / 2, Random.Range(-positionVariation, positionVariation) / 2, 0);
            MapLocationType mapLocationType = GetRandomMapLocation(_currentMapInfo.mapObstacles);
            outskirtObstacleObject.GetComponent<MapOptionController>().SetType(mapLocationType, 1);
            // Add the serializable map locations to the map object.
            _serializableMapObject.mapLocations.Add(outskirtObstacleObject.GetComponent<MapOptionController>().serializableMapLocation);
        }
        bossBattleTransform.position = mapParentObject.position + new Vector3(0, (_numFloors + 0.5f) * yPositionDifference, 0);
        for (int floor = 0; floor < _numFloors; floor++)
        {
            // For the forest scene, we hard code some battle options.
            if (_serializableMapObject.currScene == MapScene.FOREST && floor == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    GameObject optionObject = Instantiate(mapOptionPrefab);
                    optionObject.transform.SetParent(mapParentObject);
                    optionObject.transform.position = mapParentObject.position;
                    optionObject.transform.position += new Vector3(startPosition + i * xPositionDifference, 0, 0);
                    optionObject.transform.position += new Vector3(0, floor * yPositionDifference, 0);
                    optionObject.transform.position += new Vector3(Random.Range(-positionVariation, positionVariation), Random.Range(-positionVariation, positionVariation) / 2, 0);
                    if (i != 2)
                    {
                        MapLocationType mapLocationType = GetRandomMapLocation(_currentMapInfo.mapObstacles);
                        optionObject.GetComponent<MapOptionController>().SetType(GetRandomMapLocation(_currentMapInfo.mapObstacles), floor + 1);
                        // Add the serializable map location to the map object.
                        _serializableMapObject.mapLocations.Add(optionObject.GetComponent<MapOptionController>().serializableMapLocation);
                    }
                    else
                    {
                        MapLocationType mapLocationType = _currentMapInfo.mapLocations.Find((m) => m.type == MapChoice.BASIC_ENCOUNTER);
                        optionObject.GetComponent<MapOptionController>().SetType(mapLocationType, floor + 1);
                        if (!_mapOptionDictionary.ContainsKey(floor))
                        {
                            _mapOptionDictionary[floor] = new List<MapOptionController>();
                        }
                        _mapOptionDictionary[floor].Add(optionObject.GetComponent<MapOptionController>());
                        // Add the serializable map location to the map object.
                        _serializableMapObject.mapLocations.Add(optionObject.GetComponent<MapOptionController>().serializableMapLocation);
                    }
                }
                continue;
            }
            if (_serializableMapObject.currScene == MapScene.FOREST && floor == 1)
            {
                for (int i = 0; i < 5; i++)
                {
                    GameObject optionObject = Instantiate(mapOptionPrefab);
                    optionObject.transform.SetParent(mapParentObject);
                    optionObject.transform.position = mapParentObject.position;
                    optionObject.transform.position += new Vector3(startPosition + i * xPositionDifference, 0, 0);
                    optionObject.transform.position += new Vector3(0, floor * yPositionDifference, 0);
                    optionObject.transform.position += new Vector3(Random.Range(-positionVariation, positionVariation), Random.Range(-positionVariation, positionVariation) / 2, 0);
                    if (i != 2)
                    {
                        MapLocationType mapLocationType = GetRandomMapLocation(_currentMapInfo.mapObstacles);
                        optionObject.GetComponent<MapOptionController>().SetType(GetRandomMapLocation(_currentMapInfo.mapObstacles), floor + 1);
                        // Add the serializable map location to the map object.
                        _serializableMapObject.mapLocations.Add(optionObject.GetComponent<MapOptionController>().serializableMapLocation);
                    }
                    else
                    {
                        MapLocationType mapLocationType = _currentMapInfo.mapLocations.Find((m) => m.type == MapChoice.SHOP);
                        optionObject.GetComponent<MapOptionController>().SetType(mapLocationType, floor + 1);
                        if (!_mapOptionDictionary.ContainsKey(floor))
                        {
                            _mapOptionDictionary[floor] = new List<MapOptionController>();
                        }
                        _mapOptionDictionary[floor].Add(optionObject.GetComponent<MapOptionController>());
                        // Add the serializable map location to the map object.
                        _serializableMapObject.mapLocations.Add(optionObject.GetComponent<MapOptionController>().serializableMapLocation);
                    }
                }
                continue;
            }
            if (_serializableMapObject.currScene == MapScene.AERICHO_CITY && floor == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    GameObject optionObject = Instantiate(mapOptionPrefab);
                    optionObject.transform.SetParent(mapParentObject);
                    optionObject.transform.position = mapParentObject.position;
                    optionObject.transform.position += new Vector3(startPosition + i * xPositionDifference, 0, 0);
                    optionObject.transform.position += new Vector3(0, floor * yPositionDifference, 0);
                    optionObject.transform.position += new Vector3(Random.Range(-positionVariation, positionVariation), Random.Range(-positionVariation, positionVariation) / 2, 0);
                    if (i != 2)
                    {
                        MapLocationType mapLocationType = GetRandomMapLocation(_currentMapInfo.mapObstacles);
                        optionObject.GetComponent<MapOptionController>().SetType(GetRandomMapLocation(_currentMapInfo.mapObstacles), floor + 1);
                        // Add the serializable map location to the map object.
                        _serializableMapObject.mapLocations.Add(optionObject.GetComponent<MapOptionController>().serializableMapLocation);
                    }
                    else
                    {
                        MapLocationType mapLocationType = _currentMapInfo.mapLocations.Find((m) => m.type == MapChoice.TREASURE);
                        optionObject.GetComponent<MapOptionController>().SetType(mapLocationType, floor + 1);
                        if (!_mapOptionDictionary.ContainsKey(floor))
                        {
                            _mapOptionDictionary[floor] = new List<MapOptionController>();
                        }
                        _mapOptionDictionary[floor].Add(optionObject.GetComponent<MapOptionController>());
                        // Add the serializable map location to the map object.
                        _serializableMapObject.mapLocations.Add(optionObject.GetComponent<MapOptionController>().serializableMapLocation);
                    }
                }
                continue;
            }
            // Randomizer determines if there will be TWO or THREE options to choose from.
            float randomizer = Random.Range(0f, 1f);
            bool obstacleRecentlySpawned = false;
            for (int i = 0; i < 5; i++)
            {
                // < 0.Xf = Two obstacles. > 0.Xf = Three obstacles.
                // Randomize if the obstacle even appears in the first place. (70%)
                if ((randomizer < 0.3f && i % 2 == 0) || (randomizer >= 0.3f && i % 2 == 1))
                {
                    if (obstacleRecentlySpawned) { obstacleRecentlySpawned = false; continue; }
                    obstacleRecentlySpawned = true;
                    GameObject optionObject = Instantiate(mapOptionPrefab);
                    optionObject.transform.SetParent(mapParentObject);
                    optionObject.transform.position = mapParentObject.position;
                    optionObject.transform.position += new Vector3(startPosition + i * xPositionDifference, 0, 0);
                    optionObject.transform.position += new Vector3(0, floor * yPositionDifference, 0);
                    optionObject.transform.position += new Vector3(Random.Range(-positionVariation, positionVariation), Random.Range(-positionVariation, positionVariation) / 2, 0);
                    MapLocationType mapLocationType = GetRandomMapLocation(_currentMapInfo.mapObstacles);
                    optionObject.GetComponent<MapOptionController>().SetType(GetRandomMapLocation(_currentMapInfo.mapObstacles), floor + 1);
                    // Add the serializable map location to the map object.
                    _serializableMapObject.mapLocations.Add(optionObject.GetComponent<MapOptionController>().serializableMapLocation);
                }
                else
                {
                    // Set what type of map event it should be. An encounter? A shop? Etc.
                    MapLocationType mapLocationType = GetRandomMapLocation(_currentMapInfo.mapLocations);
                    // Spawn a treasure room depending on increments.
                    GameObject optionObject = Instantiate(mapOptionPrefab);
                    optionObject.transform.SetParent(mapParentObject);
                    optionObject.transform.position = mapParentObject.position;
                    optionObject.transform.position += new Vector3(startPosition + i * xPositionDifference, 0, 0);
                    optionObject.transform.position += new Vector3(0, floor * yPositionDifference, 0);
                    optionObject.transform.position += new Vector3(Random.Range(-positionVariation, positionVariation), Random.Range(-positionVariation, positionVariation) / 2, 0);
                    if (floor > 0 && floor % 5 == 0) { mapLocationType = _currentMapInfo.mapLocations.Find((ml) => ml.type == MapChoice.TREASURE); }
                    optionObject.GetComponent<MapOptionController>().SetType(mapLocationType, floor + 1);
                    if (!_mapOptionDictionary.ContainsKey(floor))
                    {
                        _mapOptionDictionary[floor] = new List<MapOptionController>();
                    }
                    _mapOptionDictionary[floor].Add(optionObject.GetComponent<MapOptionController>());
                    // Add the serializable map location to the map object.
                    _serializableMapObject.mapLocations.Add(optionObject.GetComponent<MapOptionController>().serializableMapLocation);
                }
            }
            // Spawn a few more obstacles around the outskirts of the map.
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject outskirtObstacleObject = Instantiate(mapOptionPrefab);
                outskirtObstacleObject.transform.SetParent(mapParentObject);
                outskirtObstacleObject.transform.position = mapParentObject.position;
                outskirtObstacleObject.transform.position += new Vector3(i * outsideObstacleDistance, floor * yPositionDifference, 0);
                MapLocationType mapLocationType = GetRandomMapLocation(_currentMapInfo.mapObstacles);
                outskirtObstacleObject.GetComponent<MapOptionController>().SetType(mapLocationType, 1);
                outskirtObstacleObject.transform.position += new Vector3(Random.Range(-positionVariation, positionVariation) / 3, Random.Range(-positionVariation, positionVariation), 0);
                // Add the serializable map location to the map object.
                _serializableMapObject.mapLocations.Add(outskirtObstacleObject.GetComponent<MapOptionController>().serializableMapLocation);
            }
        }
        // Helper function to randomly connect every node on the map.
        RandomlyConnectLocations();
    }

    private void RandomlyConnectLocations()
    {
        // Draw a line to the next available line, except for the last floor of lines.
        for (int floor = 0; floor < _mapOptionDictionary.Keys.Count - 1; floor++)
        {
            List<MapOptionController> mapOptionControllers = _mapOptionDictionary[floor];
            for (int j = 0; j < mapOptionControllers.Count; j++)
            {
                MapOptionController currentFloorNode = mapOptionControllers[j];
                SerializableMapPath createdPath = new SerializableMapPath();
                createdPath.initialPosition = currentFloorNode.transform.position;
                float chance = Random.Range(0f, 1f);
                if (chance < 0.33f)
                {
                    createdPath.targetPosition = _mapOptionDictionary[floor + 1][Mathf.Clamp((j + 1), 0, _mapOptionDictionary[floor + 1].Count - 1)].transform.position;
                    _mapOptionDictionary[floor + 1][Mathf.Clamp((j + 1), 0, _mapOptionDictionary[floor + 1].Count - 1)].isConnected = true;
                }
                else if (chance < 0.66f)
                {
                    createdPath.targetPosition = _mapOptionDictionary[floor + 1][Mathf.Clamp((j - 1), 0, _mapOptionDictionary[floor + 1].Count - 1)].transform.position;
                    _mapOptionDictionary[floor + 1][Mathf.Clamp((j - 1), 0, _mapOptionDictionary[floor + 1].Count - 1)].isConnected = true;
                }
                else
                {
                    createdPath.targetPosition = _mapOptionDictionary[floor + 1][Mathf.Clamp((j), 0, _mapOptionDictionary[floor + 1].Count - 1)].transform.position;
                    _mapOptionDictionary[floor + 1][Mathf.Clamp((j), 0, _mapOptionDictionary[floor + 1].Count - 1)].isConnected = true;
                }
                _serializableMapObject.mapPaths.Add(createdPath);
            }
        }
        // Make sure all node are connected to something on the floor previous. (Corrections!)
        for (int floor = 1; floor < _mapOptionDictionary.Keys.Count; floor++)
        {
            List<MapOptionController> mapOptionControllers = _mapOptionDictionary[floor];
            for (int j = 0; j < mapOptionControllers.Count; j++)
            {
                MapOptionController currentFloorNode = mapOptionControllers[j];
                SerializableMapPath createdPath = new SerializableMapPath();
                createdPath.targetPosition = currentFloorNode.transform.position;
                if (!currentFloorNode.isConnected)
                {
                    float chance = Random.Range(0f, 1f);
                    if (chance > 0.33f)
                    {
                        createdPath.initialPosition = _mapOptionDictionary[floor - 1][Mathf.Clamp((j + 1), 0, _mapOptionDictionary[floor - 1].Count - 1)].transform.position;
                    }
                    else if (chance > 0.66f)
                    {
                        createdPath.initialPosition = _mapOptionDictionary[floor - 1][Mathf.Clamp((j - 1), 0, _mapOptionDictionary[floor - 1].Count - 1)].transform.position;
                    }
                    else
                    {
                        createdPath.initialPosition = _mapOptionDictionary[floor - 1][Mathf.Clamp((j - 1), 0, _mapOptionDictionary[floor - 1].Count - 1)].transform.position;
                    }
                    _serializableMapObject.mapPaths.Add(createdPath);
                    currentFloorNode.isConnected = true;
                }
            }
        }
        // Make the last row of nodes connect to the boss.
        foreach (MapOptionController moc in _mapOptionDictionary[_numFloors - 1])
        {
            SerializableMapPath createdPath = new SerializableMapPath();
            createdPath.initialPosition = moc.transform.position;
            createdPath.targetPosition = bossBattleTransform.position;
            _serializableMapObject.mapPaths.Add(createdPath);
        }
        // Helper function to draw lines between every location.
        DrawLinesBetweenLocations();
    }

    // Draws lines that connect different map locations to each other.
    private void DrawLinesBetweenLocations()
    {
        // Populate the `mapOptions` and `mapOptionDictionary` data structures.
        PopulateMapOptionData();
        // Draw a line between every two map locations from the _serializableMapObject data.
        int linesCreated = 0;
        foreach (SerializableMapPath smp in _serializableMapObject.mapPaths)
        {
            GameObject initialMapObj = _mapOptions.Find((obj) => obj.transform.position == smp.initialPosition);
            GameObject targetMapObj = _mapOptions.Find((obj) => obj.transform.position == smp.targetPosition);
            // If we can't find the target map object, that means it's just an arbitrary position that
            // the path leads to. This is done for the starting location, since it's not really a map location.
            if (initialMapObj == null && targetMapObj != null)
            {
                targetMapObj.GetComponent<MapOptionController>().CreateLineTo(smp.initialPosition, linesCreated++);
            }
            else
            {
                initialMapObj.GetComponent<MapOptionController>().CreateLineTo(targetMapObj.GetComponent<MapOptionController>(), linesCreated++);
            }
        }
        // Draw the first row of nodes to the starting shadow. (purely cosmetic)
        foreach (MapOptionController moc in _mapOptionDictionary[1])
        {
            moc.CreateLineTo(initialPlayerTransform.position, linesCreated);
            linesCreated++;
        }
    }

    // This function creates a map object from a pre-created
    // SerializableMapObject. This makes it so the save/load system
    // actually works.
    private void GenerateMapFromObject(SerializableMapObject mapObject)
    {
        InitializeMapBG();
        _serializableMapObject = mapObject;
        // Set the boss battle transform.
        bossBattleTransform.position = mapParentObject.position + new Vector3(0, (_numFloors + 0.5f) * _yPositionDifference / _numFloors, 0);
        // Set all of the map locations.
        foreach (SerializableMapLocation mapLoc in _serializableMapObject.mapLocations)
        {
            if (mapLoc.mapLocationType.type == MapChoice.BOSS) { continue; } // Don't make one for the boss.
            GameObject mapOptionObject = Instantiate(mapOptionPrefab);
            // Parent the map option object to the map icon parent.
            mapOptionObject.transform.SetParent(mapParentObject);
            mapOptionObject.transform.position = mapLoc.position;
            mapOptionObject.transform.GetComponent<MapOptionController>().SetType(mapLoc.mapLocationType, mapLoc.floorNumber);
        }
        // Populate the map option dictionary after creating every object.
        PopulateMapOptionData();
        // Set the player's position.
        playerTransform.position = _serializableMapObject.currLocation.position;
        // Set the boss battle icon position.
        float yPositionDifference = _yPositionDifference / _numFloors; // Default: 3
        bossBattleTransform.position = mapParentObject.position + new Vector3(0, (_numFloors + 0.5f) * yPositionDifference, 0);
        // Draw all of the lines between nodes on the map.
        DrawLinesBetweenLocations();
    }

    // A weighted probability function that returns one of the possible map events.
    private MapLocationType GetRandomMapLocation(List<MapLocationType> options)
    {
        // Find the total weight of all possibilities.
        float totalWeight = 0;
        options.ForEach((mapLoc) =>
        {
            totalWeight += mapLoc.weightedChance;
        });
        // Get a random number from 0 to the total weight.
        float randomNum = Random.Range(0f, totalWeight);
        // For each map location: if the random number is
        // <= the weight for the current map location,
        // choose it. Or else, subtract the current weight and
        // move to the next one.
        MapLocationType chosenLoc = new MapLocationType();
        bool locationFound = false;
        options.ForEach((mapLoc) =>
        {
            if (!locationFound)
            {
                if (randomNum <= mapLoc.weightedChance)
                {
                    chosenLoc = mapLoc;
                    locationFound = true;
                }
                randomNum -= mapLoc.weightedChance;
            }
        });
        return chosenLoc;
    }

    // Gets all of the options on the map and places them in the
    // `mapOptions` list.
    private void PopulateMapOptionData()
    {
        _mapOptions.Clear();
        _mapOptionDictionary.Clear();
        List<GameObject> allMapOptions = new List<GameObject>(GameObject.FindGameObjectsWithTag("MapOption"));
        foreach (GameObject obj in allMapOptions)
        {
            if (obj.GetComponent<MapOptionController>())
            {
                MapOptionController moc = obj.GetComponent<MapOptionController>();
                // If it's not an obstacle, add it to the arrays.
                if (!moc.serializableMapLocation.mapLocationType.isObstacle)
                {
                    _mapOptions.Add(obj);
                    if (!_mapOptionDictionary.ContainsKey(moc.serializableMapLocation.floorNumber))
                    {
                        _mapOptionDictionary[moc.serializableMapLocation.floorNumber] = new List<MapOptionController>();
                    }
                    _mapOptionDictionary[moc.serializableMapLocation.floorNumber].Add(moc);
                }
            }
        }
    }

    // Modifies the transparencies of all icons on the map.
    // The user should ONLY be able to interact with locations
    // that are ONE floor above them.
    public void UpdateMapIconTransparencies()
    {
        // Find all of the valid paths that include the current position
        // as the path's starting position.
        List<SerializableMapPath> validPaths = _serializableMapObject.mapPaths.FindAll((path) => path.initialPosition == _serializableMapObject.currLocation.position);
        // Loop through every map option and set interactability based on these valid paths.
        foreach (GameObject mapObject in _mapOptions)
        {
            foreach (SerializableMapPath path in validPaths)
            {
                if (mapObject.transform.position == path.targetPosition)
                {
                    mapObject.GetComponent<MapOptionController>().SetInteractable(true, true);
                    break;
                }
                else
                {
                    mapObject.GetComponent<MapOptionController>().SetInteractable(false, true);
                }
            }
        }
    }

    // Enable functionality of all map options as well as the "show all cards in deck" button.
    public void EnableMapOptionColliders()
    {
        bossBattleTransform.GetComponent<BoxCollider2D>().enabled = true;
        // If the floor we're trying to access doesn't exist, we're at the boss.
        if (!_mapOptionDictionary.ContainsKey(_currFloor + 1)) { return; }
        foreach (MapOptionController moc in _mapOptionDictionary[_currFloor + 1])
        {
            moc.GetComponent<BoxCollider2D>().enabled = true;
        }
    }

    // Disable functionality of all map options as well as the "show all cards in deck" button.
    public void DisableMapOptionColliders()
    {
        bossBattleTransform.GetComponent<BoxCollider2D>().enabled = false;
        // If the floor we're trying to access doesn't exist, we're at the boss.
        if (!_mapOptionDictionary.ContainsKey(_currFloor + 1)) { return; }
        // Or else, disable all the next options until prompted.
        foreach (MapOptionController moc in _mapOptionDictionary[_currFloor + 1])
        {
            moc.GetComponent<BoxCollider2D>().enabled = false;
        }
    }

    // Choose a specific option.
    public void ChooseOption(SerializableMapLocation chosenMapLocation)
    {
        // Start the choose option coroutine, to animate and choose the selection.
        StartCoroutine(ChooseOptionCoroutine(chosenMapLocation));
    }

    private IEnumerator ChooseOptionCoroutine(SerializableMapLocation chosenMapLocation)
    {
        // Prevent the player from selecting another option.
        MapController.Instance.DisableMapOptionColliders();
        // Make the character animate towards the next thing.
        yield return HeroTraverseToPositionCoroutine(chosenMapLocation.position);
        MapChoice mapChoice = chosenMapLocation.mapLocationType.type;
        // Change the player's current location.
        GameController.GetMapObject().currLocation = chosenMapLocation;
        // Render the appropriate actions based on the location.
        switch (mapChoice)
        {
            case MapChoice.SHOP:
                FadeTransitionController.Instance.HideScreen("Shop", 0.75f);
                break;
            case MapChoice.TREASURE:
                TreasureController.Instance.ShowChest();
                break;
            case MapChoice.BASIC_ENCOUNTER:
            case MapChoice.MINIBOSS_ENCOUNTER:
            case MapChoice.BOSS:
                Encounter allEnemiesToRender = Globals.GetEnemyEncounterByScene(GameController.GetMapObject().currScene, _currFloor, mapChoice == MapChoice.MINIBOSS_ENCOUNTER, mapChoice == MapChoice.BOSS, GameController.GetLoadedEncounters());
                GameController.AddSeenEnemies(allEnemiesToRender);
                GameController.nextBattleEnemies = allEnemiesToRender.enemies;
                FadeTransitionController.Instance.HideScreen("Battle", 0.75f);
                break;
            case MapChoice.UPGRADE_MACHINE:
                FadeTransitionController.Instance.HideScreen("Upgrade", 0.75f);
                break;
        }
    }

    // Makes the player icon move towards a certain position.
    private IEnumerator HeroTraverseToPositionCoroutine(Vector3 targetPosition)
    {
        Vector3 initialPosition = playerTransform.localPosition;
        ParticleSystem playerParticleSystem = playerTransform.Find("Particle System").GetComponent<ParticleSystem>();
        float walkDuration = 1; // Amount of seconds to reach end of path.
        float timeElapsed = 0;
        float timeSinceLastParticle = 0;
        float particleCooldown = 0.15f;
        SoundManager.Instance.PlayOneShot(footstepsSFX, 0.22f);
        while (timeElapsed < walkDuration)
        {
            playerTransform.localPosition = Vector3.Lerp(initialPosition, targetPosition, timeElapsed / walkDuration);
            timeElapsed += Time.deltaTime;
            timeSinceLastParticle += Time.deltaTime;
            if (timeSinceLastParticle > particleCooldown)
            {
                playerParticleSystem.Emit(1);
                timeSinceLastParticle = 0;
            }
            yield return null;
        }
        playerTransform.localPosition = targetPosition;
        yield return new WaitForSeconds(0.5f);
    }

}
