using System.Collections.Generic;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// One pointer/fingertip observation fed to <see cref="PettingDetector"/>.
    /// <see cref="ScreenPosition"/> is in pixels, <see cref="Timestamp"/> is a
    /// monotonic time in seconds.
    /// </summary>
    public readonly struct PettingSample
    {
        public PettingSample(Vector2 screenPosition, bool isOverDog,
            float confidence, double timestamp)
        {
            ScreenPosition = screenPosition;
            IsOverDog = isOverDog;
            Confidence = confidence;
            Timestamp = timestamp;
        }

        public Vector2 ScreenPosition { get; }
        public bool IsOverDog { get; }
        public float Confidence { get; }
        public double Timestamp { get; }
    }

    /// <summary>
    /// Detects a "petting" gesture: a pointer that stays over the dog and sweeps
    /// back and forth (>= 2 direction reversals) covering enough total travel
    /// inside a sliding time window.
    /// </summary>
    public sealed class PettingDetector
    {
        private readonly float minimumConfidence;
        private readonly float minimumTravel;
        private readonly float minimumSegment;
        private readonly double windowSeconds;
        private readonly Queue<PettingSample> samples = new();
        private float travel;
        private int reversals;
        private Vector2 previousDirection;

        public PettingDetector(float minimumConfidence, float minimumTravel,
            float minimumSegment, double windowSeconds)
        {
            this.minimumConfidence = minimumConfidence;
            this.minimumTravel = minimumTravel;
            this.minimumSegment = minimumSegment;
            this.windowSeconds = windowSeconds;
        }

        /// <summary>Returns true on the frame the gesture completes.</summary>
        public bool AddSample(in PettingSample sample)
        {
            if (!sample.IsOverDog || sample.Confidence < minimumConfidence)
            {
                Reset();
                return false;
            }

            while (samples.Count > 0 &&
                   sample.Timestamp - samples.Peek().Timestamp > windowSeconds)
                samples.Dequeue();

            if (samples.Count == 0)
            {
                Reset();
                samples.Enqueue(sample);
                return false;
            }

            Vector2 segment = sample.ScreenPosition - Last().ScreenPosition;
            float distance = segment.magnitude;
            if (distance >= minimumSegment)
            {
                Vector2 direction = segment / distance;
                if (previousDirection.sqrMagnitude > 0f &&
                    Vector2.Dot(previousDirection, direction) <= -0.25f)
                    reversals++;
                previousDirection = direction;
                travel += distance;
            }

            samples.Enqueue(sample);
            bool triggered = travel >= minimumTravel && reversals >= 2;
            if (triggered)
                Reset();
            return triggered;
        }

        public void Reset()
        {
            samples.Clear();
            travel = 0f;
            reversals = 0;
            previousDirection = Vector2.zero;
        }

        private PettingSample Last()
        {
            PettingSample last = default;
            foreach (PettingSample item in samples)
                last = item;
            return last;
        }
    }
}
