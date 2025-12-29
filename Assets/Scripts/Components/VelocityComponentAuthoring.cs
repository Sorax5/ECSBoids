using Unity.Entities;
using UnityEngine;

public class VelocityComponentAuthoring : MonoBehaviour
{
}

public class VelocityComponentBaker : Baker<VelocityComponentAuthoring>
{
    public override void Bake(VelocityComponentAuthoring authoring)
    {
        var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        AddComponent(entity, new BoidComponent()
        {
        });
    }
}
