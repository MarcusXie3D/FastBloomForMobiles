// author : Marcus Xie

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BloomBreath : MonoBehaviour
{
    public float breathSpeed = 2.0f;

    private Material mat;

    void Start()
    {
        mat = gameObject.GetComponent<Renderer>().material;
    }

    void Update()
    {
        mat.SetFloat("_ObjectBloomStrength", (Mathf.Sin(Time.time * breathSpeed) + 1.0f) * 0.5f);
    }
}
