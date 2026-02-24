using SkiaSharp;
using System;
using System.Collections.Generic;

namespace KnobForge.Core
{
    public enum DynamicLightAnimationMode
    {
        Steady = 0,
        Pulse = 1,
        Flicker = 2,
        Custom = 3
    }

    public sealed class DynamicLightSource
    {
        private float _animationPhaseOffsetDegrees;

        public string Name { get; set; } = "Emitter";
        public bool Enabled { get; set; } = true;
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public SKColor Color { get; set; } = new SKColor(180, 255, 210, 255);
        public float Intensity { get; set; } = 1f;
        public float Radius { get; set; } = 220f;
        public float Falloff { get; set; } = 1f;

        public float AnimationPhaseOffsetDegrees
        {
            get => _animationPhaseOffsetDegrees;
            set => _animationPhaseOffsetDegrees = Math.Clamp(value, -360f, 360f);
        }
    }

    public sealed class DynamicLightRig
    {
        public const int DefaultMaxActiveLights = 8;
        private int _maxActiveLights = DefaultMaxActiveLights;
        private float _animationSpeed = 1f;
        private float _flickerAmount;
        private float _flickerDropoutChance;
        private float _flickerSmoothing = 0.5f;
        private int _flickerSeed = 1337;

        public bool Enabled { get; set; }

        public int MaxActiveLights
        {
            get => _maxActiveLights;
            set => _maxActiveLights = Math.Clamp(value, 1, 8);
        }

        public DynamicLightAnimationMode AnimationMode { get; set; } = DynamicLightAnimationMode.Steady;

        public float AnimationSpeed
        {
            get => _animationSpeed;
            set => _animationSpeed = Math.Clamp(value, 0f, 10f);
        }

        public float FlickerAmount
        {
            get => _flickerAmount;
            set => _flickerAmount = Math.Clamp(value, 0f, 1f);
        }

        public float FlickerDropoutChance
        {
            get => _flickerDropoutChance;
            set => _flickerDropoutChance = Math.Clamp(value, 0f, 1f);
        }

        public float FlickerSmoothing
        {
            get => _flickerSmoothing;
            set => _flickerSmoothing = Math.Clamp(value, 0f, 1f);
        }

        public int FlickerSeed
        {
            get => _flickerSeed;
            set => _flickerSeed = value;
        }

        public List<DynamicLightSource> Sources { get; } = new();

        public static string BuildDefaultSourceName(int index)
            => $"Emitter {index + 1}";

        public static float BuildDefaultPhaseOffsetDegrees(int index, int sourceCount)
        {
            if (sourceCount <= 1)
            {
                return 0f;
            }

            float centeredIndex = index - ((sourceCount - 1) * 0.5f);
            return centeredIndex * 35f;
        }

        public static void NormalizeSourceIdentity(DynamicLightSource source, int index, int sourceCount)
        {
            source.Name = string.IsNullOrWhiteSpace(source.Name)
                ? BuildDefaultSourceName(index)
                : source.Name.Trim();

            if (!float.IsFinite(source.AnimationPhaseOffsetDegrees))
            {
                source.AnimationPhaseOffsetDegrees = BuildDefaultPhaseOffsetDegrees(index, sourceCount);
            }
        }

        public void EnsureIndicatorDefaults()
        {
            if (Sources.Count > 0)
            {
                for (int i = 0; i < Sources.Count; i++)
                {
                    NormalizeSourceIdentity(Sources[i], i, Sources.Count);
                }

                return;
            }

            Sources.Add(new DynamicLightSource
            {
                Name = "Emitter A",
                X = -48f,
                Y = 0f,
                Z = -28f,
                Intensity = 1.35f,
                Radius = 180f,
                AnimationPhaseOffsetDegrees = -35f
            });
            Sources.Add(new DynamicLightSource
            {
                Name = "Emitter B",
                X = 0f,
                Y = 0f,
                Z = -28f,
                Intensity = 1.60f,
                Radius = 180f,
                AnimationPhaseOffsetDegrees = 0f
            });
            Sources.Add(new DynamicLightSource
            {
                Name = "Emitter C",
                X = 48f,
                Y = 0f,
                Z = -28f,
                Intensity = 1.35f,
                Radius = 180f,
                AnimationPhaseOffsetDegrees = 35f
            });
        }
    }
}
