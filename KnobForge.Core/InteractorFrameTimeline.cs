using System;

namespace KnobForge.Core
{
    public static class InteractorFrameTimeline
    {
        private const float SnapHoldFraction = 0.18f;

        public static float ResolveNormalizedProgress(int frameIndex, int frameCount)
        {
            if (frameCount <= 1)
            {
                return 0f;
            }

            float t = frameIndex / MathF.Max(1f, frameCount - 1f);
            return Math.Clamp(t, 0f, 1f);
        }

        public static double ResolveAnimationTimeSeconds(
            int frameIndex,
            int frameCount,
            double loopDurationSeconds = 1d)
        {
            if (loopDurationSeconds <= 0d)
            {
                return 0d;
            }

            float progress = ResolveNormalizedProgress(frameIndex, frameCount);
            return progress * loopDurationSeconds;
        }

        public static float ResolveLoopNormalizedProgress(int frameIndex, int frameCount)
        {
            if (frameCount <= 1)
            {
                return 0f;
            }

            float t = frameIndex / MathF.Max(1f, frameCount);
            return Math.Clamp(t, 0f, 1f);
        }

        public static double ResolveLoopAnimationTimeSeconds(
            int frameIndex,
            int frameCount,
            double loopDurationSeconds = 1d)
        {
            if (loopDurationSeconds <= 0d)
            {
                return 0d;
            }

            float progress = ResolveLoopNormalizedProgress(frameIndex, frameCount);
            return progress * loopDurationSeconds;
        }

        public static int ResolveToggleStateIndex(
            int frameIndex,
            int frameCount,
            ToggleAssemblyStateCount toggleStateCount)
        {
            int maxStateIndex = toggleStateCount == ToggleAssemblyStateCount.ThreePosition ? 2 : 1;
            float blendPosition = ResolveToggleBlendPosition(frameIndex, frameCount, toggleStateCount);
            return Math.Clamp((int)MathF.Round(blendPosition), 0, maxStateIndex);
        }

        public static float ResolveToggleBlendPosition(
            int frameIndex,
            int frameCount,
            ToggleAssemblyStateCount toggleStateCount)
        {
            float t = ResolveNormalizedProgress(frameIndex, frameCount);
            if (toggleStateCount != ToggleAssemblyStateCount.ThreePosition)
            {
                return ResolveSnappedProgress(t);
            }

            const float startHold = 0.10f;
            const float centerHold = 0.10f;
            const float endHold = 0.10f;
            const float transitionSpan = (1f - startHold - centerHold - endHold) * 0.5f;

            float firstTransitionEnd = startHold + transitionSpan;
            float centerHoldEnd = firstTransitionEnd + centerHold;
            float secondTransitionEnd = centerHoldEnd + transitionSpan;

            if (t <= startHold)
            {
                return 0f;
            }

            if (t < firstTransitionEnd)
            {
                float local = (t - startHold) / transitionSpan;
                return ResolveSnappedProgress(local);
            }

            if (t <= centerHoldEnd)
            {
                return 1f;
            }

            if (t < secondTransitionEnd)
            {
                float local = (t - centerHoldEnd) / transitionSpan;
                return 1f + ResolveSnappedProgress(local);
            }

            return 2f;
        }

        private static float ResolveSnappedProgress(float t)
        {
            float x = Math.Clamp(t, 0f, 1f);
            if (x <= SnapHoldFraction)
            {
                return 0f;
            }

            if (x >= (1f - SnapHoldFraction))
            {
                return 1f;
            }

            float active = (x - SnapHoldFraction) / (1f - (2f * SnapHoldFraction));
            return SmootherStep(active);
        }

        private static float SmootherStep(float t)
        {
            float x = Math.Clamp(t, 0f, 1f);
            return x * x * x * (x * ((x * 6f) - 15f) + 10f);
        }
    }
}
