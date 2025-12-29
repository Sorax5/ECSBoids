using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


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
        var elapsed = (float)SystemAPI.Time.ElapsedTime;

        state.Dependency = new MoveJob()
        {
            deltaTime = deltaTime,
            elapsedTime = elapsed,
            settings = settings
        }.ScheduleParallel(dep);
    }
}

[BurstCompile]
public partial struct MoveJob: IJobEntity
{
    public float deltaTime;
    public float elapsedTime;

    public BoidSpawnSettings settings;

    private void Execute(ref LocalTransform transform, ref BoidComponent boid, in Entity entity)
    {
        var p = transform.Position;
        var t = elapsedTime;

        var desiredSpeed = math.max(0f, settings.Speed);
        var vSq = math.lengthsq(boid.Velocity);
        if (desiredSpeed > 0f)
        {
            if (vSq > 1e-10f)
            {
                var invLen = math.rsqrt(vSq);
                var speed = 1f / invLen;
                var dir = boid.Velocity * invLen;
                var speedError = desiredSpeed - speed;
                boid.Velocity += dir * (settings.ForceGain * speedError) * deltaTime;
            }
            else
            {
                boid.Velocity += desiredSpeed * settings.ForceGain * deltaTime;
            }
        }

        var maxSpeed = settings.MaxSpeed;
        var speedSq = math.lengthsq(boid.Velocity);
        if (maxSpeed > 0f && speedSq > maxSpeed * maxSpeed)
        {
            var invLen = math.rsqrt(speedSq);
            boid.Velocity *= invLen * maxSpeed;
        }

        transform.Position += boid.Velocity * deltaTime;
    }
}
