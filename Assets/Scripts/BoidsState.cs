using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BoidWorldEnum
{
    ALIGNE,
    ESPACE,
    FOLLOW
}

[Serializable]
public struct BoidState
{
    public BoidWorldEnum worldState;
    public InputAction Input;
    public BoidsDefinition boidsDefinition;
}

public class BoidsState : MonoBehaviour
{
    public event Action<BoidState> OnChangeState;

    [SerializeField] private List<BoidState> _states;
    [SerializeField] private BoidWorldEnum _initialState;

    public BoidState _currentState;

    private EntityManager _entityManager;
    private EntityQuery _settingsQuery;

    public static BoidsState Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("ECS World non disponible. Le pont ne peut pas �tre �tabli.");
            return;
        }

        _entityManager = world.EntityManager;
        _settingsQuery = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<BoidSpawnSettings>());

        _currentState = _states.Find(state => state.worldState == _initialState);
        foreach (var boidState in _states)
        {
            boidState.Input?.Enable();
        }

        if (_currentState.boidsDefinition)
        {
            UpdateEcsSettings(_currentState.boidsDefinition);
        }
    }

    private void Update()
    {
        foreach (var state in _states.Where(state => state.Input != null && state.Input.WasPerformedThisFrame()))
        {
            Debug.Log($"State changed to: {state.worldState}");
            _currentState = state;

            OnChangeState?.Invoke(_currentState);

            if (_currentState.boidsDefinition)
            {
                UpdateEcsSettings(_currentState.boidsDefinition);
            }

            break;
        }
    }

    private void UpdateEcsSettings(BoidsDefinition def)
    {
        if (!_settingsQuery.HasSingleton<BoidSpawnSettings>())
        {
            Debug.LogWarning("BoidSpawnSettings entity not found in ECS world.");
            return;
        }

        var spawnerEntity = _settingsQuery.GetSingletonEntity();
        var settings = _entityManager.GetComponentData<BoidSpawnSettings>(spawnerEntity);

        settings.CohesionRadius = def.CohesionRadius;
        settings.CohesionForce = def.CohesionForce;
        settings.AlignementRadius = def.AlignementRadius;
        settings.AlignementForce = def.AlignementForce;
        settings.SeparationRadius = def.SeparationRadius;
        settings.SeparationForce = def.SeparationForce;
        settings.Speed = def.Speed;
        settings.AngleDegrees = def.Angle;

        _entityManager.SetComponentData(spawnerEntity, settings);

        Debug.Log($"ECS Settings mis � jour pour l'�tat : {_currentState.worldState}");
    }

    private void OnDestroy()
    {
        foreach (var boidState in _states)
        {
            if (boidState.Input != null) boidState.Input.Disable();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
