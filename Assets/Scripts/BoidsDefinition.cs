using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BoidsDefinition", menuName = "Boids/BoidsDefinition")]
public class BoidsDefinition : ScriptableObject
{
    [Header("Cohesion Parameters")]
    [SerializeField] public float CohesionRadius = 3f;
    [SerializeField] public float CohesionForce = 5f;

    [Header("Alignement Parameters")]
    [SerializeField] public float AlignementRadius = 2f;
    [SerializeField] public float AlignementForce = 5f;

    [Header("Separation Parameters")]
    [SerializeField] public float SeparationRadius = 1f;
    [SerializeField] public float SeparationForce = 10f;

    [Header("Boid Parameter")]
    [SerializeField, Range(0f, 360f)] public float Angle = 270f;
    [SerializeField] public float Speed = 1.0f;
    [SerializeField] public GameObject BoidPrefab;

    public event Action OnDefinitionChanged;

    private void OnValidate()
    {
        CohesionRadius = Mathf.Max(0, CohesionRadius);
        CohesionForce = Mathf.Max(0, CohesionForce);

        AlignementRadius = Mathf.Max(0, AlignementRadius);
        AlignementForce = Mathf.Max(0, AlignementForce);

        SeparationRadius = Mathf.Max(0, SeparationRadius);
        SeparationForce = Mathf.Max(0, SeparationForce);

        Speed = Mathf.Max(0, Speed);
        OnDefinitionChanged?.Invoke();
    }
}
