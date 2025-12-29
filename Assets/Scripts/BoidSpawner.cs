using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class BoidSpawnerAuthoring : MonoBehaviour
{
    [Header("Boids Parameters")]
    [SerializeField] public Bounds SpawnZone;
    [SerializeField] public int SpawnAmount = 1000;
    [SerializeField] public BoidsDefinition BoidsDefinition;
    [SerializeField] public List<BoidsDefinition> BoidsDefinitions;

    [Header("Confinement Parameters")]
    [SerializeField] public float BoundaryThreshold = 1.5f;
    [SerializeField] public float ForceGain = 15f;
    [SerializeField] public float MaxSpeed = 5f;

    public BoidsState CurrentState;

    private void Start()
    {
        // Prefer global instance to handle cross-scene/SubScene access.
        Debug.Log("ézdzzdd");
        if (CurrentState == null)
        {
            CurrentState = BoidsState.Instance ?? FindObjectsByType<BoidsState>(FindObjectsSortMode.None).FirstOrDefault();
            Debug.Log("zdddzd");
        }

        if (CurrentState != null)
        {
            CurrentState.OnChangeState += CurrentState_OnChangeState;
            // Apply initial settings from current state once.
            CurrentState_OnChangeState(CurrentState._currentState);
        }
        else
        {
            Debug.LogWarning("BoidSpawnerAuthoring: No BoidsState available; spawn settings won't react to input.");
        }
    }

    private void OnDestroy()
    {
        if (CurrentState != null)
        {
            CurrentState.OnChangeState -= CurrentState_OnChangeState;
        }
    }

    private void CurrentState_OnChangeState(BoidState obj)
    {
        BoidsDefinition = obj.boidsDefinition;
        OnValidate();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(SpawnZone.center, SpawnZone.size);
    }

    private void OnValidate()
    {
        var defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld == null)
        {
            return;
        }

        var entityManager = defaultWorld.EntityManager;
        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BoidSpawnSettings>());
        if (!query.HasSingleton<BoidSpawnSettings>())
        {
            return;
        }

        var spawnerEntity = query.GetSingletonEntity();
        var settings = entityManager.GetComponentData<BoidSpawnSettings>(spawnerEntity);

        var min = SpawnZone.min;
        var max = SpawnZone.max;
        if (min.x > max.x) (min.x, max.x) = (max.x, min.x);
        if (min.y > max.y) (min.y, max.y) = (max.y, min.y);
        if (min.z > max.z) (min.z, max.z) = (max.z, min.z);

        settings.Min = min;
        settings.Max = max;

        settings.CohesionRadius = BoidsDefinition.CohesionRadius;
        settings.CohesionForce = BoidsDefinition.CohesionForce;
        settings.AlignementRadius = BoidsDefinition.AlignementRadius;
        settings.AlignementForce = BoidsDefinition.AlignementForce;
        settings.SeparationRadius = BoidsDefinition.SeparationRadius;
        settings.SeparationForce = BoidsDefinition.SeparationForce;
        settings.Speed = BoidsDefinition.Speed;
        settings.AngleDegrees = BoidsDefinition.Angle;
        settings.BoundaryThreshold = BoundaryThreshold;
        settings.ForceGain = ForceGain;
        settings.MaxSpeed = MaxSpeed;

        entityManager.SetComponentData(spawnerEntity, settings);
    }
}

public struct BoidSpawnSettings : IComponentData
{
    public float3 Min;
    public float3 Max;
    public float AngleDegrees;
    public int Count;
    public float Speed;
    public Entity Prefab;

    public float BoundaryThreshold;
    public float ForceGain;
    public float MaxSpeed;

    public float CohesionRadius;
    public float CohesionForce;

    public float AlignementRadius;
    public float AlignementForce;

    public float SeparationRadius;
    public float SeparationForce;
    public int InstanceId;
}

public struct BoidSpawnRequest : IComponentData
{
    public int Count;
}

public class BoidSpawnerBaker : Baker<BoidSpawnerAuthoring>
{
    public override void Bake(BoidSpawnerAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        var min = authoring.SpawnZone.min;
        var max = authoring.SpawnZone.max;
        if (min.x > max.x) (min.x, max.x) = (max.x, min.x);
        if (min.y > max.y) (min.y, max.y) = (max.y, min.y);
        if (min.z > max.z) (min.z, max.z) = (max.z, min.z);

        var prefabEntity = GetEntity(authoring.BoidsDefinition.BoidPrefab, TransformUsageFlags.Dynamic);

        AddComponent(entity, new BoidSpawnSettings
        {
            CohesionRadius = authoring.BoidsDefinition.CohesionRadius,
            CohesionForce = authoring.BoidsDefinition.CohesionForce,
            AlignementRadius = authoring.BoidsDefinition.AlignementRadius,
            AlignementForce = authoring.BoidsDefinition.AlignementForce,
            SeparationRadius = authoring.BoidsDefinition.SeparationRadius,
            SeparationForce = authoring.BoidsDefinition.SeparationForce,

            Speed = authoring.BoidsDefinition.Speed,
            Min = min,
            Max = max,
            AngleDegrees = authoring.BoidsDefinition.Angle,
            BoundaryThreshold = math.max(0f, authoring.BoundaryThreshold),
            ForceGain = authoring.ForceGain,
            MaxSpeed = math.max(0f, authoring.MaxSpeed),
            Prefab = prefabEntity,
            InstanceId = authoring.GetInstanceID(),
            Count = 0
        });

        AddComponent(entity, new BoidSpawnRequest
        {
            Count = math.max(0, authoring.SpawnAmount)
        });
    }
}

[BurstCompile]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct BoidSpawnSystem : ISystem
{
    private EntityQuery _spawnerQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidSpawnSettings>();
        state.RequireForUpdate<BoidSpawnRequest>();
        _spawnerQuery = state.GetEntityQuery(ComponentType.ReadOnly<BoidSpawnSettings>(), ComponentType.ReadOnly<BoidSpawnRequest>());
    }

    public void OnUpdate(ref SystemState state)
    {
        var settings = SystemAPI.GetSingleton<BoidSpawnSettings>();
        var request = SystemAPI.GetSingleton<BoidSpawnRequest>();
        var spawnerEntity = SystemAPI.GetSingletonEntity<BoidSpawnSettings>();

        if (request.Count <= 0)
        {
            state.EntityManager.RemoveComponent<BoidSpawnRequest>(spawnerEntity);
            return;
        }

        var count = request.Count;
        var entities = new NativeArray<Entity>(count, Allocator.Temp);
        state.EntityManager.Instantiate(settings.Prefab, entities);

        var rng = Unity.Mathematics.Random.CreateFromIndex(0xC0FFEEu + (uint)settings.InstanceId);
        var prefabHasBoid = state.EntityManager.HasComponent<BoidComponent>(settings.Prefab);

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        for (int i = 0; i < count; i++)
        {
            var instance = entities[i];

            var t = rng.NextFloat3();
            var pos = math.lerp(settings.Min, settings.Max, t);
            ecb.SetComponent(instance, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));

            var velocity = math.normalizesafe(rng.NextFloat3Direction());
            var boidData = new BoidComponent
            {
                Velocity = velocity,
                InitialPosition = pos,
            };

            if (prefabHasBoid)
            {
                ecb.SetComponent(instance, boidData);
            }
            else
            {
                ecb.AddComponent(instance, boidData);
            }
        }

        ecb.RemoveComponent<BoidSpawnRequest>(spawnerEntity);
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        entities.Dispose();
    }
}
