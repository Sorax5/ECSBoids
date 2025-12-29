using System;
using System.Collections.Generic;
using UnityEngine;

public class BoidStateUI : MonoBehaviour
{
    [SerializeField] private BoidSpawnerAuthoring boidSpawner;

    private List<string> boidStates = new List<string>();

    private void Start()
    {
        foreach (var boidSpawnerBoidsDefinition in boidSpawner.BoidsDefinitions)
        {
            boidStates.Add(boidSpawnerBoidsDefinition.name);
        }
    }
}
