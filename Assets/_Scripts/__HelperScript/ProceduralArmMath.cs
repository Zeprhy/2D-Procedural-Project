using UnityEngine;

public static class ProceduralArmMath
{
    // Menghitung Jangkauan Pegas Maju-Mundur
    public static float UpdateSpringReach(
        float distanceToMouse,
        float mouseNearDist,
        float mouseFarDist,
        float minReach,
        float maxReach,
        float stiffness,
        float damping,
        ref float reachVelocity,
        float currentReach,
        float deltaTime)
    {
        float reachPercent = Mathf.InverseLerp(mouseNearDist, mouseFarDist, distanceToMouse);
        float desiredReach = Mathf.Lerp(minReach, maxReach, reachPercent);

        float springForce = (desiredReach - currentReach) * stiffness;
        float dampingForce = reachVelocity * damping;
        float acceleration = springForce - dampingForce;

        reachVelocity += acceleration * deltaTime;
        return currentReach + (reachVelocity * deltaTime);
    }

    // Menghitung Sudut Bidik Halus Menghadap Mouse
    public static float UpdateAimAngle(
        Vector2 toMouse,
        float facinfDir,
        float minAngle,
        float maxAngle,
        float lagTime,
        ref float angleVelocity,
        float currentAngle)
    {
        float rawAngle = Mathf.Atan2(toMouse.y, toMouse.x * facinfDir) * Mathf.Rad2Deg;
        float clampedAngle = Mathf.Clamp(rawAngle, minAngle, maxAngle);
        return Mathf.SmoothDampAngle(currentAngle, clampedAngle, ref angleVelocity, lagTime);
    }
    
}