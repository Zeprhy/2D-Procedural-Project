using UnityEngine;

/// <summary>
/// Static math utilities for procedural 2D arm animation.
/// Stateless, deterministic, and fully testable.
/// </summary>
public static class ProceduralArmMath2DRebuild
{
    #region Config Structs

    /// <summary>Spring configuration for reach distance.</summary>
    public readonly struct ReachSpringConfig
    {
        public readonly float minReach;
        public readonly float maxReach;
        public readonly float mouseNearDist;
        public readonly float mouseFarDist;
        public readonly float stiffness;
        public readonly float damping;

        public ReachSpringConfig(float minReach, float maxReach, float mouseNearDist, float mouseFarDist, float stiffness, float damping)
        {
            this.minReach = minReach;
            this.maxReach = maxReach;
            this.mouseNearDist = mouseNearDist;
            this.mouseFarDist = mouseFarDist;
            this.stiffness = stiffness;
            this.damping = damping;
        }
    }

    /// <summary>Lag configuration for aim angle smoothing.</summary>
    public readonly struct AimLagConfig
    {
        public readonly float minAngle;
        public readonly float maxAngle;
        public readonly float lagTime;

        public AimLagConfig(float minAngle, float maxAngle, float lagTime)
        {
            this.minAngle = minAngle;
            this.maxAngle = maxAngle;
            this.lagTime = lagTime;
        }
    }

    /// <summary>Two-bone IK configuration (shoulder -> elbow -> hand).</summary>
    public readonly struct TwoBoneIKConfig
    {
        public readonly float upperArmLength;
        public readonly float forearmLength;
        public readonly bool useElbowHint;
        public readonly Vector2 elbowHintPosition; // world space

        public TwoBoneIKConfig(float upperArmLength, float forearmLength, bool useElbowHint = false, Vector2 elbowHintPosition = default)
        {
            this.upperArmLength = upperArmLength;
            this.forearmLength = forearmLength;
            this.useElbowHint = useElbowHint;
            this.elbowHintPosition = elbowHintPosition;
        }
    }

    /// <summary>Shoulder offset configuration (auto-shrug when aiming up/down).</summary>
    public readonly struct ShoulderOffsetConfig
    {
        public readonly float maxUpOffset;
        public readonly float maxDownOffset;
        public readonly float upAngleThreshold;   // degrees, above this = shrug up
        public readonly float downAngleThreshold; // degrees, below this = drop down
        public readonly float smoothSpeed;

        public ShoulderOffsetConfig(float maxUpOffset, float maxDownOffset, float upAngleThreshold, float downAngleThreshold, float smoothSpeed)
        {
            this.maxUpOffset = maxUpOffset;
            this.maxDownOffset = maxDownOffset;
            this.upAngleThreshold = upAngleThreshold;
            this.downAngleThreshold = downAngleThreshold;
            this.smoothSpeed = smoothSpeed;
        }
    }

    /// <summary>Recoil configuration.</summary>
    public readonly struct RecoilConfig
    {
        public readonly float recoverySpeed;
        public readonly float maxRecoilAngle;

        public RecoilConfig(float recoverySpeed, float maxRecoilAngle)
        {
            this.recoverySpeed = recoverySpeed;
            this.maxRecoilAngle = maxRecoilAngle;
        }
    }

    #endregion

    #region Spring Reach

    /// <summary>
    /// Updates a spring-damped reach distance based on mouse distance.
    /// </summary>
    /// <param name="distanceToMouse">Current distance from shoulder to mouse.</param>
    /// <param name="cfg">Spring configuration.</param>
    /// <param name="reachVelocity">Reference to persistent velocity state.</param>
    /// <param name="currentReach">Current reach value.</param>
    /// <param name="deltaTime">Time step.</param>
    /// <returns>New reach value.</returns>
    public static float UpdateSpringReach(float distanceToMouse, in ReachSpringConfig cfg, ref float reachVelocity, float currentReach, float deltaTime)
    {
        float t = Mathf.InverseLerp(cfg.mouseNearDist, cfg.mouseFarDist, distanceToMouse);
        t = Mathf.Clamp01(t);
        float desiredReach = Mathf.Lerp(cfg.minReach, cfg.maxReach, t);

        float springForce = (desiredReach - currentReach) * cfg.stiffness;
        float dampingForce = reachVelocity * cfg.damping;
        float acceleration = springForce - dampingForce;

        reachVelocity += acceleration * deltaTime;
        return currentReach + reachVelocity * deltaTime;
    }

    #endregion

    #region Aim Angle Lag

    /// <summary>
    /// Smoothly interpolates aim angle toward target with clamping and lag.
    /// </summary>
    /// <param name="toMouse">Vector from shoulder to mouse (world space).</param>
    /// <param name="facingDir">Character facing direction (1 = right, -1 = left).</param>
    /// <param name="cfg">Aim lag configuration.</param>
    /// <param name="angleVelocity">Reference to persistent angular velocity state.</param>
    /// <param name="currentAngle">Current aim angle (local space, relative to facing).</param>
    /// <returns>New aim angle (local space).</returns>
    public static float UpdateAimAngle(Vector2 toMouse, float facingDir, in AimLagConfig cfg, ref float angleVelocity, float currentAngle)
    {
        // Convert to local space (relative to facing direction)
        float rawAngle = Mathf.Atan2(toMouse.y, toMouse.x * facingDir) * Mathf.Rad2Deg;
        float clampedAngle = Mathf.Clamp(rawAngle, cfg.minAngle, cfg.maxAngle);
        return Mathf.SmoothDampAngle(currentAngle, clampedAngle, ref angleVelocity, cfg.lagTime);
    }

    #endregion

    #region Two-Bone IK (FABRIK-style, single iteration)

    /// <summary>
    /// Solves two-bone IK for arm (shoulder -> elbow -> hand).
    /// Returns elbow position, hand position, and joint angles.
    /// </summary>
    /// <param name="shoulderPos">Shoulder world position.</param>
    /// <param name="targetPos">Desired hand world position.</param>
    /// <param name="cfg">IK configuration.</param>
    /// <returns>(elbowPos, handPos, shoulderAngle, elbowAngle). Angles in degrees, local to arm facing.</returns>
    public static (Vector2 elbowPos, Vector2 handPos, float shoulderAngle, float elbowAngle) SolveTwoBoneIK(Vector2 shoulderPos, Vector2 targetPos, in TwoBoneIKConfig cfg)
    {
        Vector2 toTarget = targetPos - shoulderPos;
        float targetDist = toTarget.magnitude;
        float maxReach = cfg.upperArmLength + cfg.forearmLength;
        float minReach = Mathf.Abs(cfg.upperArmLength - cfg.forearmLength);

        // Clamp target distance to reachable range
        float clampedDist = Mathf.Clamp(targetDist, minReach + 0.001f, maxReach - 0.001f);
        Vector2 clampedTarget = shoulderPos + toTarget.normalized * clampedDist;

        // Law of cosines for elbow angle
        float cosElbow = (cfg.upperArmLength * cfg.upperArmLength + cfg.forearmLength * cfg.forearmLength - clampedDist * clampedDist) / (2f * cfg.upperArmLength * cfg.forearmLength);
        cosElbow = Mathf.Clamp(cosElbow, -1f, 1f);
        float elbowAngleRad = Mathf.Acos(cosElbow); // 0 = straight, PI = fully folded

        // Shoulder angle to target
        float shoulderAngleRad = Mathf.Atan2(clampedTarget.y - shoulderPos.y, clampedTarget.x - shoulderPos.x);

        // Elbow position (using standard IK geometry)
        float alpha = Mathf.Acos(Mathf.Clamp((cfg.upperArmLength * cfg.upperArmLength + clampedDist * clampedDist - cfg.forearmLength * cfg.forearmLength) / (2f * cfg.upperArmLength * clampedDist), -1f, 1f));
        
        Vector2 elbowPos;
        if (cfg.useElbowHint)
        {
            // Use elbow hint (pole vector) to determine bend direction
            Vector2 toHint = cfg.elbowHintPosition - shoulderPos;
            float hintSide = Mathf.Sign(Vector2.Dot(toHint, new Vector2(-toTarget.y, toTarget.x)));
            elbowPos = shoulderPos + new Vector2(
                Mathf.Cos(shoulderAngleRad + hintSide * alpha) * cfg.upperArmLength,
                Mathf.Sin(shoulderAngleRad + hintSide * alpha) * cfg.upperArmLength
            );
        }
        else
        {
            // Default: bend "forward" (positive Z rotation = counter-clockwise)
            elbowPos = shoulderPos + new Vector2(
                Mathf.Cos(shoulderAngleRad + alpha) * cfg.upperArmLength,
                Mathf.Sin(shoulderAngleRad + alpha) * cfg.upperArmLength
            );
        }

        // Hand position (should match clampedTarget)
        Vector2 handPos = clampedTarget;

        // Convert angles to degrees (local space: 0 = pointing right along +X)
        float shoulderAngleDeg = shoulderAngleRad * Mathf.Rad2Deg;
        float elbowAngleDeg = (Mathf.PI - elbowAngleRad) * Mathf.Rad2Deg; // Interior angle at elbow

        return (elbowPos, handPos, shoulderAngleDeg, elbowAngleDeg);
    }

    #endregion

    #region Shoulder Offset

    /// <summary>
    /// Computes vertical shoulder offset based on aim angle (shrug when aiming up, drop when aiming down).
    /// </summary>
    /// <param name="aimAngleLocal">Current aim angle in local space (degrees).</param>
    /// <param name="cfg">Shoulder offset configuration.</param>
    /// <param name="currentOffset">Reference to persistent offset state.</param>
    /// <param name="deltaTime">Time step.</param>
    /// <returns>New shoulder Y offset.</returns>
    public static float UpdateShoulderOffset(float aimAngleLocal, in ShoulderOffsetConfig cfg, ref float currentOffset, float deltaTime)
    {
        float targetOffset = 0f;
        if (aimAngleLocal > cfg.upAngleThreshold)
        {
            targetOffset = Mathf.Lerp(0f, cfg.maxUpOffset, Mathf.InverseLerp(cfg.upAngleThreshold, 90f, aimAngleLocal));
        }
        else if (aimAngleLocal < cfg.downAngleThreshold)
        {
            targetOffset = Mathf.Lerp(0f, -cfg.maxDownOffset, Mathf.InverseLerp(cfg.downAngleThreshold, -90f, aimAngleLocal));
        }

        return Mathf.MoveTowards(currentOffset, targetOffset, cfg.smoothSpeed * deltaTime);
    }

    #endregion

    #region Recoil

    /// <summary>
    /// Applies recoil impulse and recovers over time.
    /// </summary>
    /// <param name="currentRecoil">Current recoil angle offset (degrees).</param>
    /// <param name="impulse">Recoil impulse to add (degrees).</param>
    /// <param name="cfg">Recoil configuration.</param>
    /// <param name="deltaTime">Time step.</param>
    /// <returns>New recoil angle offset.</returns>
    public static float UpdateRecoil(float currentRecoil, float impulse, in RecoilConfig cfg, float deltaTime)
    {
        float newRecoil = currentRecoil + impulse;
        newRecoil = Mathf.Clamp(newRecoil, -cfg.maxRecoilAngle, cfg.maxRecoilAngle);
        return Mathf.MoveTowards(newRecoil, 0f, cfg.recoverySpeed * deltaTime);
    }

    #endregion

    #region Utility

    /// <summary>
    /// Gets world-space hand position from shoulder, reach, and angle.
    /// </summary>
    public static Vector2 GetHandWorldPos(Vector2 shoulderPos, float reach, float angleLocalDeg, float facingDir)
    {
        float rad = angleLocalDeg * Mathf.Deg2Rad;
        return shoulderPos + new Vector2(Mathf.Cos(rad) * reach * facingDir, Mathf.Sin(rad) * reach);
    }

    /// <summary>
    /// Gets world-space rotation for hand bone (weapon/sprite).
    /// </summary>
    public static Quaternion GetHandWorldRotation(float angleLocalDeg, float facingDir, float handAngleOffset)
    {
        float worldAngle = angleLocalDeg * facingDir + handAngleOffset * facingDir;
        return Quaternion.Euler(0f, 0f, worldAngle);
    }

    /// <summary>
    /// Gets idle/rest hand position relative to shoulder.
    /// </summary>
    public static Vector2 GetIdleHandPos(Vector2 shoulderPos, Vector2 idleOffset, float facingDir)
    {
        return shoulderPos + new Vector2(idleOffset.x * facingDir, idleOffset.y);
    }

    /// <summary>
    /// Gets idle/rest hand rotation.
    /// </summary>
    public static Quaternion GetIdleHandRotation(float idleAngle, float facingDir)
    {
        return Quaternion.Euler(0f, 0f, idleAngle * facingDir);
    }

    #endregion
}