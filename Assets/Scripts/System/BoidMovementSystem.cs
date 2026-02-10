using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// System to update the position of boids based on their velocity, and to apply a force to steer them towards a desired speed.
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(BoidAreaConfinementSystem))]
[UpdateAfter(typeof(BoidSteeringSystem))]
public partial struct BoidMovementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        foreach (var (transform, boid) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<BoidComponent>>())
        {
            transform.ValueRW.Position = boid.ValueRO.InitialPosition;
        }
    }
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<BoidSpawnSettings>(out var settings))
        {
           return; 
        }

        var dep = state.Dependency;

        var deltaTime = SystemAPI.Time.DeltaTime;
        var elapsed = SystemAPI.Time.ElapsedTime;

        state.Dependency = new MoveJob()
        {
            DeltaTime = deltaTime,
            ElapsedTime = elapsed,
            Settings = settings
        }.ScheduleParallel(dep);
    }
}

/// <summary>
/// Job to update the position of boids based on their velocity, and to apply a force to steer them towards a desired speed.
/// </summary>
[BurstCompile]
public partial struct MoveJob: IJobEntity, ITimedJob
{
    public float DeltaTime { get; set; }
    public double ElapsedTime { get; set; }

    public BoidSpawnSettings Settings { get; set; }

    private void Execute(ref LocalTransform transform, ref BoidComponent boid, in Entity entity)
    {
        var desiredSpeed = math.max(0f, Settings.Speed);
        var velocityLengthSquare = math.lengthsq(boid.Velocity);
        
        if (desiredSpeed > 0f)
        {
            if (velocityLengthSquare > 1e-10f)
            {
                var invLen = math.rsqrt(velocityLengthSquare);
                var speed = 1f / invLen;
                var dir = boid.Velocity * invLen;
                var speedError = desiredSpeed - speed;
                
                boid.Velocity += dir * (Settings.ForceGain * speedError) * DeltaTime;
            }
            else
            {
                boid.Velocity += desiredSpeed * Settings.ForceGain * DeltaTime;
            }
        }

        var maxSpeed = Settings.MaxSpeed;
        var speedSq = math.lengthsq(boid.Velocity);
        if (maxSpeed > 0f && speedSq > maxSpeed * maxSpeed)
        {
            var invLen = math.rsqrt(speedSq);
            boid.Velocity *= invLen * maxSpeed;
        }

        transform.Position += boid.Velocity * DeltaTime;
    }
}
