using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


[BurstCompile]
[UpdateAfter(typeof(BoidSteeringSystem))]
public partial struct BoidAreaConfinementSystem : ISystem
{
    private BoidSpawnSettings _settings;
    private bool _hasSettings;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoidComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_hasSettings)
        {
            if (SystemAPI.TryGetSingleton(out BoidSpawnSettings s))
            {
                _settings = s;
                _hasSettings = true;
            }
            else
            {
                return;
            }
        }

        var dep = state.Dependency;

        var job = new ConfinementJob
        {
            deltaTime = SystemAPI.Time.DeltaTime,
            settings = _settings
        };

        state.Dependency = job.ScheduleParallel(dep);
    }
}

[BurstCompile]
internal partial struct ConfinementJob : IJobEntity
{
    public float deltaTime;
    public BoidSpawnSettings settings;

    private void Execute(in LocalTransform transform, ref BoidComponent boid)
    {
        var position = transform.Position;
        var confinementForce = float3.zero;

        var boundaryThreshold = settings.BoundaryThreshold;
        var forceGain = settings.ForceGain;
        var maxSpeed = settings.MaxSpeed;

        if (position.x < settings.Min.x + boundaryThreshold)
        {
            confinementForce.x += (settings.Min.x + boundaryThreshold - position.x) / boundaryThreshold;
        }
        else if (position.x > settings.Max.x - boundaryThreshold)
        {
            confinementForce.x -= (position.x - (settings.Max.x - boundaryThreshold)) / boundaryThreshold;
        }

        if (position.y < settings.Min.y + boundaryThreshold)
        {
            confinementForce.y += (settings.Min.y + boundaryThreshold - position.y) / boundaryThreshold;
        }
        else if (position.y > settings.Max.y - boundaryThreshold)
        {
            confinementForce.y -= (position.y - (settings.Max.y - boundaryThreshold)) / boundaryThreshold;
        }

        if (position.z < settings.Min.z + boundaryThreshold)
        {
            confinementForce.z += (settings.Min.z + boundaryThreshold - position.z) / boundaryThreshold;
        }
        else if (position.z > settings.Max.z - boundaryThreshold)
        {
            confinementForce.z -= (position.z - (settings.Max.z - boundaryThreshold)) / boundaryThreshold;
        }

        confinementForce = math.clamp(confinementForce, -1f, 1f);

        if (!math.any(confinementForce != 0f))
        {
            return;
        }

        boid.Velocity += forceGain * deltaTime * confinementForce;

        var speedSq = math.lengthsq(boid.Velocity);
        var maxSpeedSq = maxSpeed * maxSpeed;
        if (speedSq > maxSpeedSq && maxSpeed > 0f)
        {
            boid.Velocity = math.normalizesafe(boid.Velocity) * maxSpeed;
        }
    }
}
