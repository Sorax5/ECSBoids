using System;
using Unity.Entities;
using Unity.Mathematics;

public struct BoidComponent : IComponentData
{
    public float3 Velocity;
    public float3 InitialPosition;

    public static bool IsInZone(float3 selfPosition, float3 otherPosition, float3 velocity, float cohesionRadius, float angle)
    {
        var toOther = otherPosition - selfPosition;
        var r = cohesionRadius;
        if (r <= 0f)
            return false;

        if (math.lengthsq(toOther) > r * r)
            return false;

        // angle semantics fixed : angle <= 0 => no vision, angle >= 360 => full circle
        if (angle <= 0f)
            return false;

        if (angle >= 360f)
            return true;

        var fLenSq = math.lengthsq(velocity);
        // Si la vitesse est quasi nulle, considérer une vision omnidirectionnelle pour la stabilité.
        if (fLenSq < 1e-8f)
        {
            return true;
        }
        var localAngle = velocity * math.rsqrt(fLenSq);

        var toLenSq = math.lengthsq(toOther);
        if (toLenSq < 1e-8f)
            return false;

        var dir = toOther * math.rsqrt(toLenSq);

        var dot = math.dot(localAngle, dir);
        var cosLimit = math.cos(math.radians(angle * 0.5f));

        return dot >= cosLimit;
    }
}
