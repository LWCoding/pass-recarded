using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SecretTitleController : MonoBehaviour
{

    public TitleUIButtonHandler quitButtonHandler;
    public GameObject secretTrophyObject;
    public Sprite secretButtonSprite;

    private void Start()
    {
        // TODO: This is just a secret for the demo!
        if (PlayerPrefs.GetInt("BeatGame") == 1)
        {
            EnableSecret();
        }
        secretTrophyObject?.SetActive(PlayerPrefs.GetInt("BeatBoykisser") >= 1);
    }

    private void EnableSecret()
    {
        quitButtonHandler.transform.GetChild(0).GetComponent<Image>().sprite = secretButtonSprite;
        quitButtonHandler.OnClick = new UnityEngine.Events.UnityEvent();
        quitButtonHandler.OnClick.AddListener(() =>
        {
            StartNewGame();
        });
    }

    // Starts a new game by setting all of the variables in GameController
    // and initializing a starting relic. Optionally, start in a different
    // scene by supplying the mapScene parameter.
    public void StartNewGame()
    {
        // Initialize the hero with base information.
        GameController.SetChosenHero(Globals.GetBaseHero(HeroTag.RYAN));
        GameController.SetSeenEnemies(new List<Encounter>());
        GameController.SetMapScene(MapScene.AERICHO_CITY);
        GameController.SetMapObject(null);
        GameController.SetMoney(150);
        GameController.SetXP(15);
        GameController.saveFileName = "Save.ass"; // TODO: Make this vary!
        GameController.alreadyPlayedTutorials.Add("Battle");
        // Start the game.
        StartGame();
    }

    private void StartGame()
    {
        // Make sure the map starts in the forest.
        FadeTransitionController.Instance.HideScreen("Map", 1.5f);
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.J) && Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.C) && Input.GetKey(KeyCode.K))
        {
            PlayerPrefs.SetInt("BeatGame", 1);
            StartCoroutine(WaitUntilNoKeyCodesThenRefresh());
        }
    }

    private IEnumerator WaitUntilNoKeyCodesThenRefresh()
    {
        yield return new WaitUntil(() => !Input.anyKey);
        SceneManager.LoadScene("Title");
    }

}
