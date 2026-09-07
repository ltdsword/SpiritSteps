using UnityEngine;

namespace CorgiAR
{
    /// <summary>Shared deterministic trajectory math for previews and flight.</summary>
    public static class ThrowBallistics
    {
        public static float TimeToGround(Vector3 origin, Vector3 velocity, float groundY)
        {
            float gravity = Mathf.Max(0.001f, -Physics.gravity.y);
            float height = Mathf.Max(0f, origin.y - groundY);
            float discriminant = velocity.y * velocity.y + 2f * gravity * height;
            float time = (velocity.y + Mathf.Sqrt(Mathf.Max(0f, discriminant))) / gravity;
            return Mathf.Max(0.01f, time);
        }

        public static Vector3 PositionAtTime(Vector3 origin, Vector3 velocity, float time)
        {
            return origin + velocity * time + Physics.gravity * (0.5f * time * time);
        }

        public static Vector3 LandingPoint(Vector3 origin, Vector3 velocity, float groundY)
        {
            Vector3 landing = PositionAtTime(origin, velocity,
                TimeToGround(origin, velocity, groundY));
            landing.y = groundY;
            return landing;
        }

        public static Vector3 ConstrainPlanarLanding(Vector3 origin, Vector3 initialVelocity,
            float groundY, float minX, float maxX, float minZ, float maxZ,
            out Vector3 landing, out bool wasLimited)
        {
            Vector3 predicted = LandingPoint(origin, initialVelocity, groundY);
            landing = new Vector3(
                Mathf.Clamp(predicted.x, minX, maxX),
                groundY,
                Mathf.Clamp(predicted.z, minZ, maxZ));
            wasLimited = (landing - predicted).sqrMagnitude > 0.000001f;

            float flightTime = TimeToGround(origin, initialVelocity, groundY);
            initialVelocity.x = (landing.x - origin.x) / flightTime;
            initialVelocity.z = (landing.z - origin.z) / flightTime;
            return initialVelocity;
        }

        public static Vector3 VelocityForLanding(Vector3 origin, Vector3 initialVelocity,
            float groundY, Vector3 landing)
        {
            float flightTime = TimeToGround(origin, initialVelocity, groundY);
            initialVelocity.x = (landing.x - origin.x) / flightTime;
            initialVelocity.z = (landing.z - origin.z) / flightTime;
            return initialVelocity;
        }
    }
}
