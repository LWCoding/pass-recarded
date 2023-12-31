using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeSceneInteractable : MonoBehaviour, IInteractable
{

    [Header("Object Assignments")]
    [SerializeField] private MouseHoverScaler _spriteObjectScaler;
    [SerializeField] private GameObject _popupObject;
    [Header("Area Properties")]
    [SerializeField] private string _sceneNameWhenInteracted;

    private bool _isInteractable = false;

    public void Awake()
    {
        _spriteObjectScaler.Initialize(_spriteObjectScaler.transform);
        _spriteObjectScaler.SetIsInteractable(true);
        OnLocationExit();
    }

    public void OnInteract()
    {
        if (!_isInteractable) { return; }
        _isInteractable = false;
        TransitionManager.Instance.HideScreen(_sceneNameWhenInteracted, 1.25f);
    }

    public void OnLocationEnter()
    {
        _popupObject.SetActive(true);
        _isInteractable = true;
        StartCoroutine(CheckForInteractCoroutine());
    }

    public void OnLocationExit()
    {
        _popupObject.SetActive(false);
        _isInteractable = false;
        _spriteObjectScaler.ResetScale();
        StopAllCoroutines();
    }

    private IEnumerator CheckForInteractCoroutine()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                OnInteract();
            }
            yield return null;
        }
    }

    public void OnMouseDown()
    {
        if (_isInteractable)
        {
            OnInteract();
        }
    }

    public void OnMouseOver()
    {
        if (_isInteractable)
        {
            _spriteObjectScaler.OnMouseEnter();
        }
    }

    public void OnMouseExit()
    {
        if (_isInteractable)
        {
            _spriteObjectScaler.OnMouseExit();
        }
    }

}
