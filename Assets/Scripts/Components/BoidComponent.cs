using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Boid current data state
/// </summary>
public struct BoidComponent : IComponentData
{
    public float3 Velocity;
    public float3 InitialPosition;
}
