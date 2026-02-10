using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


/// <summary>
/// System to apply a confinement force to boids that are close to the boundaries of the defined area.
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(BoidSteeringSystem))]
public partial struct BoidAreaConfinementSystem : ISystem
{
    private BoidSpawnSettings? _settings;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_settings == null)
        {
            if (!SystemAPI.TryGetSingleton(out BoidSpawnSettings s))
            {
                return;
            }
            
            _settings = s;
        }

        var dep = state.Dependency;

        var job = new ConfinementJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            Settings = _settings.Value
        };

        state.Dependency = job.ScheduleParallel(dep);
    }
}

/// <summary>
/// Confinement job that applies a force to boids that are close to the boundaries of the defined area, pushing them back towards the center.
/// </summary>
[BurstCompile]
internal partial struct ConfinementJob : IJobEntity, ITimedJob
{
    public float DeltaTime { get; set; }
    public double ElapsedTime { get; set; }
    
    public BoidSpawnSettings Settings { get; set; }

    private void Execute(in LocalTransform transform, ref BoidComponent boid)
    {
        var position = transform.Position;
        var confinementForce = float3.zero;

        applyConfinement(position, ref confinementForce, ref boid);
    }

    private void applyConfinement(float3 position, ref float3 confinementForce, ref BoidComponent boid)
    {
        var boundaryThreshold = Settings.BoundaryThreshold;
        var forceGain = Settings.ForceGain;
        var maxSpeed = Settings.MaxSpeed;

        confinementForce.x = calculateAxisConfinement(position.x, Settings.Min.x, Settings.Max.x, boundaryThreshold);
        confinementForce.y = calculateAxisConfinement(position.y, Settings.Min.y, Settings.Max.y, boundaryThreshold);
        confinementForce.z = calculateAxisConfinement(position.z, Settings.Min.z, Settings.Max.z, boundaryThreshold);

        confinementForce = math.clamp(confinementForce, -1f, 1f);

        if (!math.any(confinementForce != 0f))
        {
            return;
        }

        boid.Velocity += forceGain * DeltaTime * confinementForce;

        var speedSq = math.lengthsq(boid.Velocity);
        var maxSpeedSq = maxSpeed * maxSpeed;
        if (speedSq > maxSpeedSq && maxSpeed > 0f)
        {
            boid.Velocity = math.normalizesafe(boid.Velocity) * maxSpeed;
        }
    }

    private float calculateAxisConfinement(float position, float min, float max, float boundaryThreshold)
    {
        if (position < min + boundaryThreshold)
        {
            return (min + boundaryThreshold - position) / boundaryThreshold;
        }
        
        if (position > max - boundaryThreshold)
        {
            return - (position - (max - boundaryThreshold)) / boundaryThreshold;
        }
        
        return 0f;
    }
}
