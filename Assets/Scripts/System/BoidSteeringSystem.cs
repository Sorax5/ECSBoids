using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct BoidSteeringSystem : ISystem
{
    private EntityQuery _boidsQuery;
    private NativeParallelMultiHashMap<uint, int> _cellMap;
    private bool _cellMapInitialized;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidComponent>();
        state.RequireForUpdate<BoidSpawnSettings>();

        _boidsQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<BoidComponent>());

        _cellMapInitialized = false;
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_cellMapInitialized && _cellMap.IsCreated)
        {
            _cellMap.Dispose();
            _cellMapInitialized = false;
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<BoidSpawnSettings>(out var settings))
        {
            return;
        }

        var dt = SystemAPI.Time.DeltaTime;
        var count = _boidsQuery.CalculateEntityCount();
        if (count == 0)
        {
            return;
        }

        var cellSize = math.max(settings.SeparationRadius, math.max(settings.AlignementRadius, settings.CohesionRadius));
        if (cellSize <= 0f)
        {
            cellSize = 0.001f;
        }

        var listTransforms = _boidsQuery.ToComponentDataListAsync<LocalTransform>(Allocator.TempJob, state.Dependency, out var h1);
        var listBoids = _boidsQuery.ToComponentDataListAsync<BoidComponent>(Allocator.TempJob, h1, out var h2);
        var listEntities = _boidsQuery.ToEntityListAsync(Allocator.TempJob, h2, out var h3);

        if (!_cellMapInitialized)
        {
            _cellMap = new NativeParallelMultiHashMap<uint, int>(math.max(1, count), Allocator.Persistent);
            _cellMapInitialized = true;
        }
        else if (_cellMap.Capacity < count)
        {
            _cellMap.Capacity = count;
        }
        _cellMap.Clear();

        var buildJob = new SteeringBuildGridJob
        {
            Transforms = listTransforms.AsDeferredJobArray(),
            Map = _cellMap.AsParallelWriter(),
            Min = settings.Min,
            CellSize = cellSize
        };
        var hBuild = buildJob.Schedule(count, 64, h3);

        var maxRadius = cellSize;
        var range = math.max(1, (int)math.ceil(maxRadius / cellSize));
        var angle = settings.AngleDegrees;
        var hasFov = angle is > 0f and < 360f;
        var cosLimit = math.cos(math.radians(angle * 0.5f));

        var job = new BoidSteeringJob
        {
            DeltaTime = dt,
            Settings = settings,
            TargetsTransforms = listTransforms.AsDeferredJobArray(),
            TargetsBoids = listBoids.AsDeferredJobArray(),
            TargetsEntities = listEntities.AsDeferredJobArray(),
            CellMap = _cellMap,
            Min = settings.Min,
            CellSize = cellSize,
            NeighborSearchRange = range,
            HasFov = hasFov,
            CosLimit = cosLimit,
            SeparationCap = 24,
            AlignementCap = 24,
            CohesionCap = 24
        };

        var handle = job.ScheduleParallel(hBuild);
        handle = listTransforms.Dispose(handle);
        handle = listBoids.Dispose(handle);
        handle = listEntities.Dispose(handle);
        state.Dependency = handle;
    }
}

[BurstCompile]
internal struct SteeringBuildGridJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<LocalTransform> Transforms;
    public NativeParallelMultiHashMap<uint, int>.ParallelWriter Map;
    public float3 Min;
    public float CellSize;
    public void Execute(int index)
    {
        var pos = Transforms[index].Position;
        var cell = (int3)math.floor((pos - Min) / CellSize);
        var key = math.hash(cell);
        Map.Add(key, index);
    }
}

[BurstCompile]
internal partial struct BoidSteeringJob : IJobEntity
{
    public float DeltaTime;
    [ReadOnly] public BoidSpawnSettings Settings;

    [ReadOnly] public NativeArray<LocalTransform> TargetsTransforms;
    [ReadOnly] public NativeArray<BoidComponent> TargetsBoids;
    [ReadOnly] public NativeArray<Entity> TargetsEntities;
    [ReadOnly] public NativeParallelMultiHashMap<uint, int> CellMap;
    public float3 Min;
    public float CellSize;

    public int NeighborSearchRange;
    public bool HasFov;
    public float CosLimit;

    public int SeparationCap;
    public int AlignementCap;
    public int CohesionCap;

    private void Execute(ref BoidComponent boid, in LocalTransform transform, in Entity entity)
    {
        var pos = transform.Position;
        var vel = boid.Velocity;

        var cell = (int3)math.floor((pos - Min) / CellSize);

        var sepAccum = float3.zero;
        var sepCount = 0;
        var aliAccum = float3.zero;
        var aliCount = 0;
        var cohAccum = float3.zero;
        var cohCount = 0;

        float3 fwd;
        var vSq = math.lengthsq(vel);
        if (vSq < float.Epsilon)
        {
            fwd = new float3(0, 0, 0);
        }
        else
        {
            fwd = vel * math.rsqrt(vSq);
        }

        var sepR2 = Settings.SeparationRadius * Settings.SeparationRadius;
        var aliR2 = Settings.AlignementRadius * Settings.AlignementRadius;
        var cohR2 = Settings.CohesionRadius * Settings.CohesionRadius;

        var r = NeighborSearchRange;
        for (var dz = -r; dz <= r; dz++)
            for (var dy = -r; dy <= r; dy++)
                for (var dx = -r; dx <= r; dx++)
                {
                    var capsReached = (sepCount >= SeparationCap) & (aliCount >= AlignementCap) & (cohCount >= CohesionCap);
                    if (capsReached)
                    {
                        dz = r + 1;
                        dy = r + 1;
                        break;
                    }

                    var cellPosition = cell + new int3(dx, dy, dz);
                    var key = math.hash(cellPosition);

                    if (CellMap.TryGetFirstValue(key, out var otherIndex, out var it))
                    {
                        do
                        {
                            if (sepCount >= SeparationCap && aliCount >= AlignementCap && cohCount >= CohesionCap)
                            {
                                break;
                            }

                            var otherEntity = TargetsEntities[otherIndex];
                            if (otherEntity == entity)
                            {
                                continue;
                            }

                            var otherTransform = TargetsTransforms[otherIndex];
                            var offset = otherTransform.Position - pos;
                            var lenSq = math.lengthsq(offset);
                            if (lenSq < float.Epsilon)
                            {
                                continue;
                            }

                            var withinFov = true;
                            if (HasFov && vSq >= float.Epsilon)
                            {
                                var dir = offset * math.rsqrt(lenSq);
                                withinFov = math.dot(fwd, dir) >= CosLimit;
                            }

                            if (!withinFov)
                            {
                                continue;
                            }

                            if (sepCount < SeparationCap && sepR2 > 0f && lenSq <= sepR2)
                            {
                                sepAccum += (pos - otherTransform.Position) / math.max(lenSq, float.Epsilon);
                                sepCount++;
                            }

                            if (aliCount < AlignementCap && aliR2 > 0f && lenSq <= aliR2)
                            {
                                var otherBoid = TargetsBoids[otherIndex];
                                aliAccum += otherBoid.Velocity;
                                aliCount++;
                            }

                            if (cohCount < CohesionCap && cohR2 > 0f && lenSq <= cohR2)
                            {
                                cohAccum += otherTransform.Position;
                                cohCount++;
                            }

                        } while (CellMap.TryGetNextValue(out otherIndex, ref it));
                    }
                }

        if (sepCount > 0)
        {
            boid.Velocity += (Settings.SeparationForce * DeltaTime) * math.normalizesafe(sepAccum);
        }

        if (aliCount > 0)
        {
            var avg = aliAccum / aliCount;
            var steer = avg - vel;
            boid.Velocity += (Settings.AlignementForce * DeltaTime) * math.normalizesafe(steer);
        }

        if (cohCount > 0)
        {
            var center = cohAccum / cohCount;
            var toCenter = center - pos;
            boid.Velocity += (Settings.CohesionForce * DeltaTime) * math.normalizesafe(toCenter);
        }
    }
}
