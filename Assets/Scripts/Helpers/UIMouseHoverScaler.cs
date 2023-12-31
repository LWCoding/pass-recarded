using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class UIMouseHoverScaler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    public UnityEvent OnHoverEnter;
    public UnityEvent OnHoverExit;

    private float _initialScale;
    private float _desiredScale;
    private bool _isInteractable = false;

    private Transform _transformToScale;

    public void SetIsInteractable(bool isInteractable) => _isInteractable = isInteractable;

    public void Initialize(Transform transformToScale)
    {
        _transformToScale = transformToScale;
        _initialScale = transformToScale.transform.localScale.x;
        _desiredScale = _initialScale;
    }

    public void ScaleTo(float desiredScale)
    {
        _desiredScale = desiredScale;
    }

    public void ResetScale()
    {
        _desiredScale = _initialScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable) { return; }
        OnHoverEnter?.Invoke();
        _desiredScale = _initialScale + 0.1f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isInteractable) { return; }
        OnHoverExit?.Invoke();
        _desiredScale = _initialScale;
    }

    public void FixedUpdate()
    {
        Debug.Assert(_transformToScale != null, "Transform to scale not found!", this);
        float difference = Mathf.Abs(_transformToScale.localScale.x - _desiredScale);
        if (difference > 0.011f)
        {
            if (_transformToScale.localScale.x > _desiredScale)
            {
                if (difference < 0.05f)
                {
                    _transformToScale.localScale -= new Vector3(0.01f, 0.01f, 0);
                }
                else
                {
                    _transformToScale.localScale -= new Vector3(0.03f, 0.03f, 0);
                }
            }
            else
            {
                if (difference < 0.05f)
                {
                    _transformToScale.localScale += new Vector3(0.01f, 0.01f, 0);
                }
                else
                {
                    _transformToScale.localScale += new Vector3(0.03f, 0.03f, 0);
                }
            }
        }
    }

}
