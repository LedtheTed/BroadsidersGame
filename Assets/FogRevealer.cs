using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FogRevealer : MonoBehaviour
{

    void Start()
    {
        if (FogOfWarManager.Instance != null) {
            FogOfWarManager.Instance.RegisterVisionSource(transform);
        }
    }
    
    private void OnEnable()
    {
        if (FogOfWarManager.Instance != null) {
            FogOfWarManager.Instance.RegisterVisionSource(transform);
        }
    }

    private void OnDisable()
    {
        if (FogOfWarManager.Instance != null) {
            FogOfWarManager.Instance.UnregisterVisionSource(transform);
        }
    }
}
