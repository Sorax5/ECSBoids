using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;

/// <summary>
/// System to calculate the steering forces for each boid based on the positions and velocities of neighboring boids, applying the separation, alignment, and cohesion rules of the Boids algorithm.
/// Use a spatial partitioning technique (uniform grid) to optimize the neighbor search, and optionally limit the field of view of the boids to only consider neighbors within a certain angle in front of them.
/// </summary>
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
        if (!_cellMapInitialized || !_cellMap.IsCreated)
        {
            return;
        }
        
        _cellMap.Dispose();
        _cellMapInitialized = false;
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

/// <summary>
/// System to build a spatial partitioning grid (uniform grid) for the boids, where each cell contains a list of indices of the boids that are currently in that cell.
/// </summary>
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

/// <summary>
/// System to calculate the steering forces for each boid based on the positions and velocities of neighboring boids, applying the separation, alignment, and cohesion rules of the Boids algorithm.
/// </summary>
[BurstCompile]
internal partial struct BoidSteeringJob : IJobEntity, ITimedJob
{
    public float DeltaTime { get; set; }
    public double ElapsedTime { get; set; }
    
    [ReadOnly] public BoidSpawnSettings Settings;

    [ReadOnly] public NativeArray<LocalTransform> TargetsTransforms;
    [ReadOnly] public NativeArray<BoidComponent> TargetsBoids;
    [ReadOnly] public NativeArray<Entity> TargetsEntities;
    [ReadOnly] public NativeParallelMultiHashMap<uint, int> CellMap;
    
    public float3 Min { get; set; }
    public float CellSize { get; set; }

    public int NeighborSearchRange { get; set; }
    public bool HasFov { get; set; }
    public float CosLimit { get; set; }

    public int SeparationCap { get; set; }
    public int AlignementCap { get; set; }
    public int CohesionCap { get; set; }

    private void Execute(ref BoidComponent boid, in LocalTransform transform, in Entity entity)
    {
        var pos = transform.Position;
        var vel = boid.Velocity;

        var cell = getCell(pos);

        var separationAccumulation = float3.zero;
        var separationCount = 0;
        var alignmentAccumulation = float3.zero;
        var alignmentCount = 0;
        var cohesionAccumulation = float3.zero;
        var cohesionCount = 0;
        
        var normalizedVelocityDirection = getNormalizedVelocity(vel);

        var sepR2 = Settings.SeparationRadius * Settings.SeparationRadius;
        var aliR2 = Settings.AlignementRadius * Settings.AlignementRadius;
        var cohR2 = Settings.CohesionRadius * Settings.CohesionRadius;

        var r = NeighborSearchRange;
        
        for (var dz = -r; dz <= r; dz++)
        for (var dy = -r; dy <= r; dy++)
        for (var dx = -r; dx <= r; dx++)
        {
            if (isCapsReached(separationCount, alignmentCount, cohesionCount))
            {
                dz = r + 1;
                dy = r + 1;
                
                break;
            }
            
            var localPosition = new int3(dx, dy, dz);
            var cellPosition = cell + localPosition;

            if (!CellMap.TryGetFirstValue(math.hash(cellPosition), 
                    out var otherIndex, 
                    out var it))
            {
                continue;
            }
            
            do
            {
                if (separationCount >= SeparationCap && alignmentCount >= AlignementCap && cohesionCount >= CohesionCap)
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
                
                var lengthSq = math.lengthsq(offset);
                if (lengthSq < float.Epsilon)
                {
                    continue;
                }

                var withinFov = isWithinFov(offset, normalizedVelocityDirection, CosLimit);
                if (!withinFov)
                {
                    continue;
                }

                if (separationCount < SeparationCap && sepR2 > 0f && lengthSq <= sepR2)
                {
                    separationAccumulation += (pos - otherTransform.Position) / math.max(lengthSq, float.Epsilon);
                    separationCount++;
                }

                if (alignmentCount < AlignementCap && aliR2 > 0f && lengthSq <= aliR2)
                {
                    var otherBoid = TargetsBoids[otherIndex];
                    alignmentAccumulation += otherBoid.Velocity;
                    alignmentCount++;
                }

                if (cohesionCount >= CohesionCap || !(cohR2 > 0f) || !(lengthSq <= cohR2))
                {
                    continue;
                }
                
                cohesionAccumulation += otherTransform.Position;
                cohesionCount++;

            } while (CellMap.TryGetNextValue(out otherIndex, ref it));
        }

        if (separationCount > 0)
        {
            applyVelocity(ref boid, Settings.SeparationForce, separationAccumulation);
        }

        if (alignmentCount > 0)
        {
            var avg = alignmentAccumulation / alignmentCount;
            var steer = avg - vel;
            
            applyVelocity(ref boid, Settings.AlignementForce, steer);
        }

        if (cohesionCount <= 0)
        {
            return;
        }
        
        var center = cohesionAccumulation / cohesionCount;
        var toCenter = center - pos;
        
        applyVelocity(ref boid, Settings.CohesionForce, toCenter);
    }

    private void applyVelocity(ref BoidComponent boid, float force, float3 pos)
    {
        boid.Velocity += (force * DeltaTime) * math.normalizesafe(pos);
    }
    
    private int3 getCell(float3 position)
    {
        return (int3)math.floor((position - Min) / CellSize);
    }
    
    private float3 getNormalizedVelocity(float3 velocity)
    {
        var vSq = math.lengthsq(velocity);
        if (vSq < float.Epsilon)
        {
            return new float3(0, 0, 0);
        }

        return velocity * math.rsqrt(vSq);
    }
    
    private bool isWithinFov(float3 toOther, float3 normalizedVelocityDirection, float cosLimit)
    {
        var dir = math.normalizesafe(toOther);
        return math.dot(normalizedVelocityDirection, dir) >= cosLimit;
    }
    
    private bool isCapsReached(int sepCount, int aliCount, int cohCount)
    {
        return (sepCount >= SeparationCap) & (aliCount >= AlignementCap) & (cohCount >= CohesionCap);
    }
}
