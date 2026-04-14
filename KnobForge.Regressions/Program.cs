using KnobForge.App.ProjectFiles;
using KnobForge.Core;
using KnobForge.Core.Export;
using KnobForge.Core.Scene;
using KnobForge.Rendering;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using SkiaSharp;

internal static class Program
{
    private static int Main(string[] args)
    {
        var failures = new List<string>();
        string root = Path.Combine(Path.GetTempPath(), "knobforge-regressions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            RunTest("Envelope round-trip preserves payload", failures, () => EnvelopeRoundTripPreservesPayload(root));
            RunTest("Load rejects unsupported format", failures, () => LoadRejectsUnsupportedFormat(root));
            RunTest("Load rejects missing snapshot", failures, () => LoadRejectsMissingSnapshot(root));
            RunTest("Loaded snapshot exposes required sections", failures, () => LoadedSnapshotExposesRequiredSections(root));
            RunTest("Project type resolver honors explicit type", failures, ProjectTypeResolverHonorsExplicitType);
            RunTest("Project type resolver honors explicit indicator type", failures, ProjectTypeResolverHonorsExplicitIndicatorType);
            RunTest("Project type resolver infers slider legacy snapshots", failures, ProjectTypeResolverInfersSliderLegacySnapshots);
            RunTest("Project type resolver infers toggle legacy snapshots", failures, ProjectTypeResolverInfersToggleLegacySnapshots);
            RunTest("Project type resolver defaults to rotary when ambiguous", failures, ProjectTypeResolverDefaultsToRotaryWhenAmbiguous);
            RunTest("Loop timeline uses exclusive endpoint progression", failures, LoopTimelineUsesExclusiveEndpointProgression);
            RunTest("Legacy normalized timeline keeps inclusive endpoint progression", failures, LegacyTimelineKeepsInclusiveEndpointProgression);
            RunTest("Rotary timeline starts south and advances exclusively", failures, RotaryTimelineStartsSouthAndAdvancesExclusively);
            RunTest("Absolute export viewpoints override base camera state", failures, AbsoluteExportViewpointOverridesBaseCameraState);
            RunTest("Relative export viewpoints compose with base camera state", failures, RelativeExportViewpointComposesWithBaseCameraState);
            RunTest("Viewport framing without seed uses centered fit", failures, ViewportFramingWithoutSeedUsesCenteredFit);
            RunTest("Hybrid modules prune collar on slider defaults", failures, HybridModulesPruneCollarOnSliderDefaults);
            RunTest("Hybrid modules reattach collar for rotary defaults", failures, HybridModulesReattachCollarForRotaryDefaults);
            RunTest("Hybrid modules keep collar when prune disabled", failures, HybridModulesKeepCollarWhenPruneDisabled);
            RunTest("Removing collar reselects model node", failures, RemovingCollarReselectsModelNode);
            RunTest("Indicator defaults seed dynamic emitters with valid identity", failures, IndicatorDefaultsSeedDynamicEmittersWithValidIdentity);
            RunTest("Dynamic light source JSON round-trip preserves emitter identity", failures, DynamicLightSourceJsonRoundTripPreservesEmitterIdentity);
            RunTest("Dynamic light source identity normalization fills blanks", failures, DynamicLightSourceIdentityNormalizationFillsBlanks);
            RunTest("Indicator lens material snapshot round-trip via envelope", failures, () => IndicatorLensMaterialSnapshotRoundTripViaEnvelope(root));
            RunTest("Indicator lens preset definitions stay in supported ranges", failures, IndicatorLensPresetDefinitionsStayWithinSupportedRanges);
            RunTest("Indicator lens clear preset matches project defaults", failures, IndicatorLensClearPresetMatchesProjectDefaults);
            RunTest("Project-type switch matrix keeps expected defaults + valid selection", failures, ProjectTypeSwitchMatrixMaintainsExpectedDefaultsAndSelection);
            RunTest("Project-type switch undo/redo replay restores exact state", failures, ProjectTypeSwitchUndoRedoReplayRestoresExactState);
            RunTest("Project-type switch transition states round-trip via envelope", failures, () => ProjectTypeSwitchTransitionStatesRoundTripViaEnvelope(root));
            RunTest("Environment look controls clamp to supported ranges", failures, EnvironmentLookControlsClampToSupportedRanges);
            RunTest("Project-type defaults reset environment look controls", failures, ProjectTypeDefaultsResetEnvironmentLookControls);
            RunTest("Export camera distance scale adjusts distance and zoom", failures, ExportCameraDistanceScaleAdjustsDistanceAndZoom);
            RunTest("Export fallback framing honors padding and distance scale", failures, ExportFallbackFramingHonorsPaddingAndDistanceScale);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Best-effort temp cleanup.
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("PASS: all save/load regressions passed.");
            return 0;
        }

        Console.Error.WriteLine($"FAIL: {failures.Count} regression(s) failed.");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine($" - {failure}");
        }

        return 1;
    }

    private static void RunTest(string name, List<string> failures, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"[PASS] {name}");
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
            Console.Error.WriteLine($"[FAIL] {name}: {ex.Message}");
        }
    }

    private static void EnvelopeRoundTripPreservesPayload(string root)
    {
        string path = Path.Combine(root, "roundtrip.knob");
        string snapshotJson = BuildSnapshotJson();
        var source = new KnobProjectFileEnvelope
        {
            DisplayName = "Regression Fixture",
            SnapshotJson = snapshotJson,
            PaintStateJson = "{\"paint\":true}",
            ViewportStateJson = "{\"camera\":true}",
            ThumbnailPngBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })
        };

        if (!KnobProjectFileStore.TrySaveEnvelope(path, source, out string saveError))
        {
            throw new InvalidOperationException($"save failed: {saveError}");
        }

        if (!KnobProjectFileStore.TryLoadEnvelope(path, out KnobProjectFileEnvelope? loaded, out string loadError) || loaded is null)
        {
            throw new InvalidOperationException($"load failed: {loadError}");
        }

        AssertEqual(KnobProjectFileStore.FormatId, loaded.Format, "format");
        AssertEqual(source.DisplayName, loaded.DisplayName, "display name");
        AssertEqual(source.SnapshotJson, loaded.SnapshotJson, "snapshot json");
        AssertEqual(source.PaintStateJson, loaded.PaintStateJson, "paint state");
        AssertEqual(source.ViewportStateJson, loaded.ViewportStateJson, "viewport state");
        AssertEqual(source.ThumbnailPngBase64, loaded.ThumbnailPngBase64, "thumbnail");
    }

    private static void LoadRejectsUnsupportedFormat(string root)
    {
        string path = Path.Combine(root, "invalid-format.knob");
        string json = """
                      {
                        "Format": "knobforge.project.v999",
                        "DisplayName": "Invalid",
                        "SavedUtc": "2026-01-01T00:00:00Z",
                        "SnapshotJson": "{}"
                      }
                      """;
        File.WriteAllText(path, json);

        if (KnobProjectFileStore.TryLoadEnvelope(path, out _, out string error))
        {
            throw new InvalidOperationException("load unexpectedly succeeded for unsupported format.");
        }

        if (!error.Contains("Unsupported project format", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"unexpected error: {error}");
        }
    }

    private static void LoadRejectsMissingSnapshot(string root)
    {
        string path = Path.Combine(root, "missing-snapshot.knob");
        string json = """
                      {
                        "Format": "knobforge.project.v1",
                        "DisplayName": "Missing Snapshot",
                        "SavedUtc": "2026-01-01T00:00:00Z",
                        "SnapshotJson": ""
                      }
                      """;
        File.WriteAllText(path, json);

        if (KnobProjectFileStore.TryLoadEnvelope(path, out _, out string error))
        {
            throw new InvalidOperationException("load unexpectedly succeeded for missing snapshot.");
        }

        if (!error.Contains("snapshot data is missing", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"unexpected error: {error}");
        }
    }

    private static void LoadedSnapshotExposesRequiredSections(string root)
    {
        string path = Path.Combine(root, "sections.knob");
        var envelope = new KnobProjectFileEnvelope
        {
            DisplayName = "Section Coverage",
            SnapshotJson = BuildSnapshotJson()
        };
        if (!KnobProjectFileStore.TrySaveEnvelope(path, envelope, out string saveError))
        {
            throw new InvalidOperationException($"save failed: {saveError}");
        }

        if (!KnobProjectFileStore.TryLoadEnvelope(path, out KnobProjectFileEnvelope? loaded, out string loadError) || loaded is null)
        {
            throw new InvalidOperationException($"load failed: {loadError}");
        }

        using JsonDocument doc = JsonDocument.Parse(loaded.SnapshotJson);
        AssertHasProperty(doc.RootElement, "Lighting");
        AssertHasProperty(doc.RootElement, "Environment");
        AssertHasProperty(doc.RootElement, "Shadows");
        AssertHasProperty(doc.RootElement, "Collar");

        JsonElement collar = doc.RootElement.GetProperty("Collar");
        AssertHasProperty(collar, "ImportedMirrorX");
        AssertHasProperty(collar, "ImportedMirrorY");
        AssertHasProperty(collar, "ImportedMirrorZ");
    }

    private static void ProjectTypeResolverHonorsExplicitType()
    {
        var hint = new ProjectTypeSnapshotHint(
            HasProjectType: true,
            ProjectType: InteractorProjectType.PushButton,
            SliderMode: SliderAssemblyMode.Enabled,
            ToggleMode: ToggleAssemblyMode.Enabled,
            SliderBackplateImportedMeshPath: string.Empty,
            SliderThumbImportedMeshPath: string.Empty,
            SliderBackplateWidth: 0f,
            SliderBackplateHeight: 0f,
            SliderBackplateThickness: 0f,
            SliderThumbWidth: 0f,
            SliderThumbHeight: 0f,
            SliderThumbDepth: 0f,
            ToggleBaseImportedMeshPath: string.Empty,
            ToggleLeverImportedMeshPath: string.Empty,
            TogglePlateWidth: 0f,
            TogglePlateHeight: 0f,
            TogglePlateThickness: 0f,
            ToggleBushingRadius: 0f,
            ToggleBushingHeight: 0f,
            ToggleLeverLength: 0f,
            ToggleLeverRadius: 0f,
            ToggleLeverTopRadius: 0f,
            ToggleTipRadius: 0f,
            ToggleStateCount: ToggleAssemblyStateCount.TwoPosition,
            ToggleMaxAngleDeg: 24f);

        InteractorProjectType resolved = InteractorProjectTypeResolver.ResolveFromSnapshotHint(hint);
        if (resolved != InteractorProjectType.PushButton)
        {
            throw new InvalidOperationException($"expected PushButton, got {resolved}.");
        }
    }

    private static void ProjectTypeResolverInfersSliderLegacySnapshots()
    {
        var hint = new ProjectTypeSnapshotHint(
            HasProjectType: false,
            ProjectType: InteractorProjectType.RotaryKnob,
            SliderMode: SliderAssemblyMode.Auto,
            ToggleMode: ToggleAssemblyMode.Auto,
            SliderBackplateImportedMeshPath: "/tmp/models/slider/base.glb",
            SliderThumbImportedMeshPath: string.Empty,
            SliderBackplateWidth: 0f,
            SliderBackplateHeight: 0f,
            SliderBackplateThickness: 0f,
            SliderThumbWidth: 0f,
            SliderThumbHeight: 0f,
            SliderThumbDepth: 0f,
            ToggleBaseImportedMeshPath: string.Empty,
            ToggleLeverImportedMeshPath: string.Empty,
            TogglePlateWidth: 0f,
            TogglePlateHeight: 0f,
            TogglePlateThickness: 0f,
            ToggleBushingRadius: 0f,
            ToggleBushingHeight: 0f,
            ToggleLeverLength: 0f,
            ToggleLeverRadius: 0f,
            ToggleLeverTopRadius: 0f,
            ToggleTipRadius: 0f,
            ToggleStateCount: ToggleAssemblyStateCount.TwoPosition,
            ToggleMaxAngleDeg: 24f);

        InteractorProjectType resolved = InteractorProjectTypeResolver.ResolveFromSnapshotHint(hint);
        if (resolved != InteractorProjectType.ThumbSlider)
        {
            throw new InvalidOperationException($"expected ThumbSlider, got {resolved}.");
        }
    }

    private static void ProjectTypeResolverHonorsExplicitIndicatorType()
    {
        var hint = new ProjectTypeSnapshotHint(
            HasProjectType: true,
            ProjectType: InteractorProjectType.IndicatorLight,
            SliderMode: SliderAssemblyMode.Disabled,
            ToggleMode: ToggleAssemblyMode.Disabled,
            SliderBackplateImportedMeshPath: string.Empty,
            SliderThumbImportedMeshPath: string.Empty,
            SliderBackplateWidth: 0f,
            SliderBackplateHeight: 0f,
            SliderBackplateThickness: 0f,
            SliderThumbWidth: 0f,
            SliderThumbHeight: 0f,
            SliderThumbDepth: 0f,
            ToggleBaseImportedMeshPath: string.Empty,
            ToggleLeverImportedMeshPath: string.Empty,
            TogglePlateWidth: 0f,
            TogglePlateHeight: 0f,
            TogglePlateThickness: 0f,
            ToggleBushingRadius: 0f,
            ToggleBushingHeight: 0f,
            ToggleLeverLength: 0f,
            ToggleLeverRadius: 0f,
            ToggleLeverTopRadius: 0f,
            ToggleTipRadius: 0f,
            ToggleStateCount: ToggleAssemblyStateCount.TwoPosition,
            ToggleMaxAngleDeg: 24f);

        InteractorProjectType resolved = InteractorProjectTypeResolver.ResolveFromSnapshotHint(hint);
        if (resolved != InteractorProjectType.IndicatorLight)
        {
            throw new InvalidOperationException($"expected IndicatorLight, got {resolved}.");
        }
    }

    private static void ProjectTypeResolverInfersToggleLegacySnapshots()
    {
        var hint = new ProjectTypeSnapshotHint(
            HasProjectType: false,
            ProjectType: InteractorProjectType.RotaryKnob,
            SliderMode: SliderAssemblyMode.Auto,
            ToggleMode: ToggleAssemblyMode.Auto,
            SliderBackplateImportedMeshPath: string.Empty,
            SliderThumbImportedMeshPath: string.Empty,
            SliderBackplateWidth: 0f,
            SliderBackplateHeight: 0f,
            SliderBackplateThickness: 0f,
            SliderThumbWidth: 0f,
            SliderThumbHeight: 0f,
            SliderThumbDepth: 0f,
            ToggleBaseImportedMeshPath: "/tmp/models/switch/base.glb",
            ToggleLeverImportedMeshPath: string.Empty,
            TogglePlateWidth: 0f,
            TogglePlateHeight: 0f,
            TogglePlateThickness: 0f,
            ToggleBushingRadius: 0f,
            ToggleBushingHeight: 0f,
            ToggleLeverLength: 0f,
            ToggleLeverRadius: 0f,
            ToggleLeverTopRadius: 0f,
            ToggleTipRadius: 0f,
            ToggleStateCount: ToggleAssemblyStateCount.TwoPosition,
            ToggleMaxAngleDeg: 24f);

        InteractorProjectType resolved = InteractorProjectTypeResolver.ResolveFromSnapshotHint(hint);
        if (resolved != InteractorProjectType.FlipSwitch)
        {
            throw new InvalidOperationException($"expected FlipSwitch, got {resolved}.");
        }
    }

    private static void ProjectTypeResolverDefaultsToRotaryWhenAmbiguous()
    {
        var hint = new ProjectTypeSnapshotHint(
            HasProjectType: false,
            ProjectType: InteractorProjectType.RotaryKnob,
            SliderMode: SliderAssemblyMode.Auto,
            ToggleMode: ToggleAssemblyMode.Auto,
            SliderBackplateImportedMeshPath: string.Empty,
            SliderThumbImportedMeshPath: string.Empty,
            SliderBackplateWidth: 0f,
            SliderBackplateHeight: 0f,
            SliderBackplateThickness: 0f,
            SliderThumbWidth: 0f,
            SliderThumbHeight: 0f,
            SliderThumbDepth: 0f,
            ToggleBaseImportedMeshPath: string.Empty,
            ToggleLeverImportedMeshPath: string.Empty,
            TogglePlateWidth: 0f,
            TogglePlateHeight: 0f,
            TogglePlateThickness: 0f,
            ToggleBushingRadius: 0f,
            ToggleBushingHeight: 0f,
            ToggleLeverLength: 0f,
            ToggleLeverRadius: 0f,
            ToggleLeverTopRadius: 0f,
            ToggleTipRadius: 0f,
            ToggleStateCount: ToggleAssemblyStateCount.TwoPosition,
            ToggleMaxAngleDeg: 24f);

        InteractorProjectType resolved = InteractorProjectTypeResolver.ResolveFromSnapshotHint(hint);
        if (resolved != InteractorProjectType.RotaryKnob)
        {
            throw new InvalidOperationException($"expected RotaryKnob, got {resolved}.");
        }
    }

    private static void LoopTimelineUsesExclusiveEndpointProgression()
    {
        const int frameCount = 24;
        float start = InteractorFrameTimeline.ResolveLoopNormalizedProgress(0, frameCount);
        float end = InteractorFrameTimeline.ResolveLoopNormalizedProgress(frameCount - 1, frameCount);
        double endSeconds = InteractorFrameTimeline.ResolveLoopAnimationTimeSeconds(frameCount - 1, frameCount);

        if (MathF.Abs(start) > 1e-6f)
        {
            throw new InvalidOperationException($"expected loop timeline start at 0, got {start:0.######}.");
        }

        float expectedEnd = (frameCount - 1) / (float)frameCount;
        if (MathF.Abs(end - expectedEnd) > 1e-6f)
        {
            throw new InvalidOperationException($"expected loop timeline end {expectedEnd:0.######}, got {end:0.######}.");
        }

        if (end >= 1f)
        {
            throw new InvalidOperationException($"expected loop timeline end < 1.0, got {end:0.######}.");
        }

        if (Math.Abs(endSeconds - expectedEnd) > 1e-6)
        {
            throw new InvalidOperationException($"expected loop time seconds {expectedEnd:0.######}, got {endSeconds:0.######}.");
        }
    }

    private static void LegacyTimelineKeepsInclusiveEndpointProgression()
    {
        const int frameCount = 24;
        float end = InteractorFrameTimeline.ResolveNormalizedProgress(frameCount - 1, frameCount);
        double endSeconds = InteractorFrameTimeline.ResolveAnimationTimeSeconds(frameCount - 1, frameCount);
        if (MathF.Abs(end - 1f) > 1e-6f)
        {
            throw new InvalidOperationException($"expected legacy normalized progress end at 1, got {end:0.######}.");
        }

        if (Math.Abs(endSeconds - 1d) > 1e-6)
        {
            throw new InvalidOperationException($"expected legacy animation time end at 1s, got {endSeconds:0.######}.");
        }
    }

    private static void RotaryTimelineStartsSouthAndAdvancesExclusively()
    {
        const int frameCount = 24;
        float start = InteractorFrameTimeline.ResolveRotarySouthStartRotationRadians(0, frameCount);
        float next = InteractorFrameTimeline.ResolveRotarySouthStartRotationRadians(1, frameCount);
        float end = InteractorFrameTimeline.ResolveRotarySouthStartRotationRadians(frameCount - 1, frameCount);
        float expectedStep = MathF.Tau / frameCount;

        if (MathF.Abs(start - MathF.PI) > 1e-6f)
        {
            throw new InvalidOperationException($"expected rotary start angle at pi (south), got {start:0.######}.");
        }

        if (MathF.Abs((next - start) - expectedStep) > 1e-6f)
        {
            throw new InvalidOperationException(
                $"expected rotary step {expectedStep:0.######}, got {(next - start):0.######}.");
        }

        float expectedEnd = MathF.PI + (((frameCount - 1) / (float)frameCount) * MathF.Tau);
        if (MathF.Abs(end - expectedEnd) > 1e-6f)
        {
            throw new InvalidOperationException($"expected rotary end angle {expectedEnd:0.######}, got {end:0.######}.");
        }
    }

    private static void AbsoluteExportViewpointOverridesBaseCameraState()
    {
        var baseState = new ViewportCameraState(30f, -20f, 3.5f, new SKPoint(18f, -12f));
        var viewpoint = new ExportViewpoint
        {
            Name = "Primary",
            FileTag = string.Empty,
            Enabled = true,
            Order = 0,
            UseAbsoluteCamera = true,
            OrbitYawDeg = -8f,
            OrbitPitchDeg = -8f,
            OverrideZoom = true,
            Zoom = 2.25f,
            OverridePan = true,
            PanXPx = 10f,
            PanYPx = -4f
        };

        ViewportCameraState resolved = ExportViewpointResolver.ApplyViewpoint(baseState, viewpoint);
        AssertNearlyEqual(-8f, resolved.OrbitYawDeg, 1e-6f, "absolute yaw");
        AssertNearlyEqual(-8f, resolved.OrbitPitchDeg, 1e-6f, "absolute pitch");
        AssertNearlyEqual(2.25f, resolved.Zoom, 1e-6f, "absolute zoom");
        AssertNearlyEqual(10f, resolved.PanPx.X, 1e-6f, "absolute pan x");
        AssertNearlyEqual(-4f, resolved.PanPx.Y, 1e-6f, "absolute pan y");
    }

    private static void RelativeExportViewpointComposesWithBaseCameraState()
    {
        var baseState = new ViewportCameraState(12f, -18f, 4f, new SKPoint(-6f, 14f));
        var viewpoint = new ExportViewpoint
        {
            Name = "Over Right",
            FileTag = "over_right",
            Enabled = true,
            Order = 1,
            UseAbsoluteCamera = false,
            YawOffsetDeg = 14f,
            PitchOffsetDeg = 9f,
            OverrideZoom = false,
            OverridePan = false
        };

        ViewportCameraState resolved = ExportViewpointResolver.ApplyViewpoint(baseState, viewpoint);
        AssertNearlyEqual(26f, resolved.OrbitYawDeg, 1e-6f, "relative yaw");
        AssertNearlyEqual(-9f, resolved.OrbitPitchDeg, 1e-6f, "relative pitch");
        AssertNearlyEqual(4f, resolved.Zoom, 1e-6f, "relative zoom");
        AssertNearlyEqual(-6f, resolved.PanPx.X, 1e-6f, "relative pan x");
        AssertNearlyEqual(14f, resolved.PanPx.Y, 1e-6f, "relative pan y");
    }

    private static void ViewportFramingWithoutSeedUsesCenteredFit()
    {
        ViewportCameraState framed = ViewportCameraFraming.BuildViewportCameraState(
            referenceRadius: 220f,
            outputResolution: 300,
            renderResolution: 600,
            padding: 12f,
            cameraDistanceScale: 0.5f,
            yawDeg: -8f,
            pitchDeg: -8f,
            seedCameraState: null);

        AssertNearlyEqual(-8f, framed.OrbitYawDeg, 1e-6f, "framed yaw");
        AssertNearlyEqual(-8f, framed.OrbitPitchDeg, 1e-6f, "framed pitch");
        AssertNearlyEqual(0f, framed.PanPx.X, 1e-6f, "framed pan x");
        AssertNearlyEqual(0f, framed.PanPx.Y, 1e-6f, "framed pan y");

        if (!(framed.Zoom > 0.2f))
        {
            throw new InvalidOperationException($"expected framed zoom to stay above minimum, got {framed.Zoom:0.######}.");
        }
    }

    private static void HybridModulesPruneCollarOnSliderDefaults()
    {
        var project = new KnobProject();
        project.ApplyInteractorProjectTypeDefaults(InteractorProjectType.ThumbSlider);
        ModelNode model = project.EnsureModelNode();
        bool hasMaterial = model.Children.OfType<MaterialNode>().Any();
        bool hasCollar = model.Children.OfType<CollarNode>().Any();

        if (!hasMaterial)
        {
            throw new InvalidOperationException("expected material module for slider project.");
        }

        if (hasCollar)
        {
            throw new InvalidOperationException("expected collar module to be pruned for slider defaults.");
        }
    }

    private static void HybridModulesReattachCollarForRotaryDefaults()
    {
        var project = new KnobProject();
        project.ApplyInteractorProjectTypeDefaults(InteractorProjectType.ThumbSlider);
        project.ApplyInteractorProjectTypeDefaults(InteractorProjectType.RotaryKnob);
        ModelNode model = project.EnsureModelNode();
        bool hasCollar = model.Children.OfType<CollarNode>().Any();
        if (!hasCollar)
        {
            throw new InvalidOperationException("expected collar module to be attached for rotary defaults.");
        }
    }

    private static void HybridModulesKeepCollarWhenPruneDisabled()
    {
        var project = new KnobProject();
        ModelNode model = project.EnsureModelNode();
        bool hadCollarInitially = model.Children.OfType<CollarNode>().Any();
        project.EnsureInteractorModulesForProjectType(InteractorProjectType.ThumbSlider, pruneUnsupportedModules: false);
        bool hasCollarAfterNoPrune = model.Children.OfType<CollarNode>().Any();

        if (!hadCollarInitially || !hasCollarAfterNoPrune)
        {
            throw new InvalidOperationException("expected collar module to remain attached when pruneUnsupportedModules=false.");
        }
    }

    private static void RemovingCollarReselectsModelNode()
    {
        var project = new KnobProject();
        ModelNode model = project.EnsureModelNode();
        CollarNode collar = project.EnsureCollarNode();
        project.SetSelectedNode(collar);
        bool removed = project.RemoveCollarNode();
        if (!removed)
        {
            throw new InvalidOperationException("expected collar removal to succeed.");
        }

        if (project.SelectedNode == null || project.SelectedNode.Id != model.Id)
        {
            throw new InvalidOperationException("expected selection to fall back to model after collar removal.");
        }
    }

    private static void IndicatorDefaultsSeedDynamicEmittersWithValidIdentity()
    {
        var project = new KnobProject();
        project.ApplyInteractorProjectTypeDefaults(InteractorProjectType.IndicatorLight);

        DynamicLightRig rig = project.DynamicLightRig;
        if (!rig.Enabled)
        {
            throw new InvalidOperationException("expected dynamic light rig to be enabled for indicator defaults.");
        }

        if (rig.Sources.Count < 3)
        {
            throw new InvalidOperationException($"expected at least 3 indicator emitters, got {rig.Sources.Count}.");
        }

        for (int i = 0; i < rig.Sources.Count; i++)
        {
            DynamicLightSource source = rig.Sources[i];
            if (string.IsNullOrWhiteSpace(source.Name))
            {
                throw new InvalidOperationException($"emitter {i} name should not be blank.");
            }

            if (!float.IsFinite(source.AnimationPhaseOffsetDegrees))
            {
                throw new InvalidOperationException($"emitter {i} phase offset should be finite.");
            }

            if (source.AnimationPhaseOffsetDegrees < -360f || source.AnimationPhaseOffsetDegrees > 360f)
            {
                throw new InvalidOperationException($"emitter {i} phase offset should stay within [-360, 360].");
            }
        }
    }

    private static void DynamicLightSourceJsonRoundTripPreservesEmitterIdentity()
    {
        var source = new DynamicLightSource
        {
            Name = "CoreAmber",
            AnimationPhaseOffsetDegrees = 57.25f,
            Enabled = true,
            X = -12f,
            Y = 6f,
            Z = -24f,
            Intensity = 1.4f,
            Radius = 330f,
            Falloff = 1.2f
        };

        string json = JsonSerializer.Serialize(source);
        DynamicLightSource? roundTripped = JsonSerializer.Deserialize<DynamicLightSource>(json);
        if (roundTripped == null)
        {
            throw new InvalidOperationException("dynamic light source JSON round-trip returned null.");
        }

        if (!string.Equals(source.Name, roundTripped.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("emitter name did not survive JSON round-trip.");
        }

        if (MathF.Abs(source.AnimationPhaseOffsetDegrees - roundTripped.AnimationPhaseOffsetDegrees) > 1e-4f)
        {
            throw new InvalidOperationException("emitter phase offset did not survive JSON round-trip.");
        }
    }

    private static void DynamicLightSourceIdentityNormalizationFillsBlanks()
    {
        var source = new DynamicLightSource
        {
            Name = "   ",
            AnimationPhaseOffsetDegrees = float.NaN
        };

        DynamicLightRig.NormalizeSourceIdentity(source, index: 2, sourceCount: 5);

        if (!string.Equals(source.Name, "Emitter 3", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"expected normalized emitter name 'Emitter 3', got '{source.Name}'.");
        }

        if (!float.IsFinite(source.AnimationPhaseOffsetDegrees))
        {
            throw new InvalidOperationException("expected normalized emitter phase offset to be finite.");
        }
    }

    private static void IndicatorLensMaterialSnapshotRoundTripViaEnvelope(string root)
    {
        string path = Path.Combine(root, "indicator-lens-roundtrip.knob");
        var expected = new IndicatorLensMaterialSnapshot(
            Transmission: 0.67f,
            Ior: 1.58f,
            Thickness: 2.35f,
            TintX: 0.71f,
            TintY: 0.88f,
            TintZ: 0.79f,
            Absorption: 1.93f,
            SurfaceRoughness: 0.21f,
            SurfaceSpecularStrength: 1.44f);

        var envelope = new KnobProjectFileEnvelope
        {
            DisplayName = "Indicator Lens RoundTrip",
            SnapshotJson = JsonSerializer.Serialize(expected)
        };

        if (!KnobProjectFileStore.TrySaveEnvelope(path, envelope, out string saveError))
        {
            throw new InvalidOperationException($"save failed: {saveError}");
        }

        if (!KnobProjectFileStore.TryLoadEnvelope(path, out KnobProjectFileEnvelope? loaded, out string loadError) || loaded is null)
        {
            throw new InvalidOperationException($"load failed: {loadError}");
        }

        IndicatorLensMaterialSnapshot? actual = JsonSerializer.Deserialize<IndicatorLensMaterialSnapshot>(loaded.SnapshotJson);
        if (actual is null)
        {
            throw new InvalidOperationException("indicator lens material snapshot deserialized as null.");
        }

        AssertNearlyEqual(expected.Transmission, actual.Transmission, 1e-5f, "lens transmission");
        AssertNearlyEqual(expected.Ior, actual.Ior, 1e-5f, "lens ior");
        AssertNearlyEqual(expected.Thickness, actual.Thickness, 1e-5f, "lens thickness");
        AssertNearlyEqual(expected.TintX, actual.TintX, 1e-5f, "lens tint X");
        AssertNearlyEqual(expected.TintY, actual.TintY, 1e-5f, "lens tint Y");
        AssertNearlyEqual(expected.TintZ, actual.TintZ, 1e-5f, "lens tint Z");
        AssertNearlyEqual(expected.Absorption, actual.Absorption, 1e-5f, "lens absorption");
        AssertNearlyEqual(expected.SurfaceRoughness, actual.SurfaceRoughness, 1e-5f, "lens surface roughness");
        AssertNearlyEqual(expected.SurfaceSpecularStrength, actual.SurfaceSpecularStrength, 1e-5f, "lens specular strength");
    }

    private static void IndicatorLensPresetDefinitionsStayWithinSupportedRanges()
    {
        var presets = new[]
        {
            IndicatorLensMaterialPresets.Clear,
            IndicatorLensMaterialPresets.Frosted,
            IndicatorLensMaterialPresets.SaturatedLed
        };

        foreach (IndicatorLensMaterialPresetDefinition preset in presets)
        {
            if (!IndicatorLensMaterialPresets.IsWithinSupportedRange(preset))
            {
                throw new InvalidOperationException($"indicator lens preset is out of supported range: {preset}.");
            }
        }
    }

    private static void IndicatorLensClearPresetMatchesProjectDefaults()
    {
        var project = new KnobProject();
        project.ApplyInteractorProjectTypeDefaults(InteractorProjectType.IndicatorLight);
        IndicatorLensMaterialPresetDefinition clear = IndicatorLensMaterialPresets.Clear;

        AssertNearlyEqual(clear.Transmission, project.IndicatorLensTransmission, 1e-5f, "clear lens transmission");
        AssertNearlyEqual(clear.Ior, project.IndicatorLensIor, 1e-5f, "clear lens ior");
        AssertNearlyEqual(clear.Thickness, project.IndicatorLensThickness, 1e-5f, "clear lens thickness");
        AssertNearlyEqual(clear.Tint.X, project.IndicatorLensTint.X, 1e-5f, "clear lens tint X");
        AssertNearlyEqual(clear.Tint.Y, project.IndicatorLensTint.Y, 1e-5f, "clear lens tint Y");
        AssertNearlyEqual(clear.Tint.Z, project.IndicatorLensTint.Z, 1e-5f, "clear lens tint Z");
        AssertNearlyEqual(clear.Absorption, project.IndicatorLensAbsorption, 1e-5f, "clear lens absorption");
        AssertNearlyEqual(clear.SurfaceRoughness, project.IndicatorLensSurfaceRoughness, 1e-5f, "clear lens roughness");
        AssertNearlyEqual(clear.SurfaceSpecularStrength, project.IndicatorLensSurfaceSpecularStrength, 1e-5f, "clear lens specular");
    }

    private static readonly InteractorProjectType[] ProjectTypeMatrix =
    {
        InteractorProjectType.RotaryKnob,
        InteractorProjectType.ThumbSlider,
        InteractorProjectType.FlipSwitch,
        InteractorProjectType.PushButton,
        InteractorProjectType.IndicatorLight
    };

    private static void ProjectTypeSwitchMatrixMaintainsExpectedDefaultsAndSelection()
    {
        foreach (InteractorProjectType fromType in ProjectTypeMatrix)
        {
            foreach (InteractorProjectType toType in ProjectTypeMatrix)
            {
                if (fromType == toType)
                {
                    continue;
                }

                var project = new KnobProject();
                project.ApplyInteractorProjectTypeDefaults(fromType);
                SelectStressNodeForProjectType(project, fromType);

                project.ApplyInteractorProjectTypeDefaults(toType);
                AssertProjectTypeDefaults(project, toType, $"{fromType}->{toType}");
                AssertSelectionIsValid(project, $"{fromType}->{toType}");

                if (fromType == InteractorProjectType.RotaryKnob &&
                    toType != InteractorProjectType.RotaryKnob &&
                    project.SelectedNode is not ModelNode)
                {
                    throw new InvalidOperationException(
                        $"expected collar selection fallback to model for transition {fromType}->{toType}.");
                }
            }
        }
    }

    private static void ProjectTypeSwitchUndoRedoReplayRestoresExactState()
    {
        List<ProjectTypeTransitionSample> samples = BuildProjectTypeTransitionSamples();
        foreach (ProjectTypeTransitionSample sample in samples)
        {
            if (!Equals(sample.Before, sample.Undo))
            {
                throw new InvalidOperationException(
                    $"undo replay mismatch for {sample.FromType}->{sample.ToType}.");
            }

            if (!Equals(sample.After, sample.Redo))
            {
                throw new InvalidOperationException(
                    $"redo replay mismatch for {sample.FromType}->{sample.ToType}.");
            }
        }
    }

    private static void ProjectTypeSwitchTransitionStatesRoundTripViaEnvelope(string root)
    {
        string path = Path.Combine(root, "project-type-switch-roundtrip.knob");
        List<ProjectTypeTransitionSample> expectedSamples = BuildProjectTypeTransitionSamples();
        string snapshotJson = JsonSerializer.Serialize(expectedSamples);

        var envelope = new KnobProjectFileEnvelope
        {
            DisplayName = "Project Type Transition RoundTrip",
            SnapshotJson = snapshotJson
        };

        if (!KnobProjectFileStore.TrySaveEnvelope(path, envelope, out string saveError))
        {
            throw new InvalidOperationException($"save failed: {saveError}");
        }

        if (!KnobProjectFileStore.TryLoadEnvelope(path, out KnobProjectFileEnvelope? loaded, out string loadError) || loaded is null)
        {
            throw new InvalidOperationException($"load failed: {loadError}");
        }

        List<ProjectTypeTransitionSample>? loadedSamples =
            JsonSerializer.Deserialize<List<ProjectTypeTransitionSample>>(loaded.SnapshotJson);
        if (loadedSamples == null)
        {
            throw new InvalidOperationException("deserialized transition samples were null.");
        }

        if (loadedSamples.Count != expectedSamples.Count)
        {
            throw new InvalidOperationException(
                $"transition sample count mismatch: expected {expectedSamples.Count}, got {loadedSamples.Count}.");
        }

        for (int i = 0; i < expectedSamples.Count; i++)
        {
            if (!Equals(expectedSamples[i], loadedSamples[i]))
            {
                throw new InvalidOperationException($"transition sample mismatch at index {i}.");
            }
        }
    }

    private static void EnvironmentLookControlsClampToSupportedRanges()
    {
        var project = new KnobProject();

        project.BloomRadius = 99f;
        AssertNearlyEqual(4f, project.BloomRadius, 1e-5f, "bloom radius max clamp");
        project.BloomRadius = -2f;
        AssertNearlyEqual(0.25f, project.BloomRadius, 1e-5f, "bloom radius min clamp");

        project.GlareRotationDegrees = 999f;
        AssertNearlyEqual(180f, project.GlareRotationDegrees, 1e-5f, "glare rotation max clamp");
        project.GlareRotationDegrees = -999f;
        AssertNearlyEqual(-180f, project.GlareRotationDegrees, 1e-5f, "glare rotation min clamp");

        project.BloomCompositeIntensity = 9f;
        AssertNearlyEqual(4f, project.BloomCompositeIntensity, 1e-5f, "bloom composite max clamp");
        project.BloomCompositeIntensity = -1f;
        AssertNearlyEqual(0f, project.BloomCompositeIntensity, 1e-5f, "bloom composite min clamp");

        project.ReflectionStrength = 9f;
        AssertNearlyEqual(4f, project.ReflectionStrength, 1e-5f, "reflection strength max clamp");
        project.ReflectionStrength = -1f;
        AssertNearlyEqual(0f, project.ReflectionStrength, 1e-5f, "reflection strength min clamp");

        project.ReflectionFresnelBias = 9f;
        AssertNearlyEqual(1f, project.ReflectionFresnelBias, 1e-5f, "fresnel bias max clamp");
        project.ReflectionFresnelBias = -1f;
        AssertNearlyEqual(0f, project.ReflectionFresnelBias, 1e-5f, "fresnel bias min clamp");

        project.ClearCoatReflectionStrength = 9f;
        AssertNearlyEqual(4f, project.ClearCoatReflectionStrength, 1e-5f, "clear-coat reflection max clamp");
        project.ClearCoatReflectionStrength = -1f;
        AssertNearlyEqual(0f, project.ClearCoatReflectionStrength, 1e-5f, "clear-coat reflection min clamp");
    }

    private static void ProjectTypeDefaultsResetEnvironmentLookControls()
    {
        var project = new KnobProject();

        foreach (InteractorProjectType type in ProjectTypeMatrix)
        {
            project.BloomRadius = 3.5f;
            project.GlareRotationDegrees = 42f;
            project.BloomCompositeIntensity = 2.75f;
            project.ReflectionStrength = 2.25f;
            project.ReflectionFresnelBias = 0.35f;
            project.ClearCoatReflectionStrength = 2.5f;
            project.ReflectionOnlyPreview = true;

            project.ApplyInteractorProjectTypeDefaults(type);

            AssertNearlyEqual(1f, project.BloomRadius, 1e-5f, $"{type} bloom radius");
            AssertNearlyEqual(0f, project.GlareRotationDegrees, 1e-5f, $"{type} glare rotation");
            AssertNearlyEqual(1f, project.BloomCompositeIntensity, 1e-5f, $"{type} bloom composite");
            AssertNearlyEqual(1f, project.ReflectionStrength, 1e-5f, $"{type} reflection strength");
            AssertNearlyEqual(0.04f, project.ReflectionFresnelBias, 1e-5f, $"{type} fresnel bias");
            AssertNearlyEqual(1f, project.ClearCoatReflectionStrength, 1e-5f, $"{type} clear-coat reflection");
            if (project.ReflectionOnlyPreview)
            {
                throw new InvalidOperationException($"{type} reflection-only preview was not reset.");
            }
        }
    }

    private static void ExportCameraDistanceScaleAdjustsDistanceAndZoom()
    {
        var settings = new KnobExportSettings
        {
            CameraDistanceScale = 2f,
            Padding = 0f
        };
        var cameraState = new ViewportCameraState(0f, 0f, 4f, SKPoint.Empty);

        Camera camera = InvokePrivateStatic<Camera>(
            typeof(KnobExporter),
            "BuildExportCamera",
            10f,
            settings,
            256,
            512,
            new OrientationDebug(),
            cameraState);

        AssertNearlyEqual(120f, camera.Position.Length(), 1e-4f, "scaled export camera distance");
        AssertNearlyEqual(4f, camera.Zoom, 1e-4f, "scaled export camera zoom");
        AssertNearlyEqual(0f, camera.Position.X, 1e-4f, "scaled export camera position X");
        AssertNearlyEqual(0f, camera.Position.Y, 1e-4f, "scaled export camera position Y");
        AssertNearlyEqual(120f, camera.Position.Z, 1e-4f, "scaled export camera position Z");
    }

    private static void ExportFallbackFramingHonorsPaddingAndDistanceScale()
    {
        var settings = new KnobExportSettings
        {
            CameraDistanceScale = 4f,
            Padding = 16f
        };

        ViewportCameraState exportState = InvokePrivateStatic<ViewportCameraState>(
            typeof(KnobExporter),
            "BuildExportViewportCameraState",
            20f,
            settings,
            256,
            512,
            null);

        Camera fallbackCamera = InvokePrivateStatic<Camera>(
            typeof(KnobExporter),
            "BuildExportCamera",
            20f,
            settings,
            256,
            512,
            new OrientationDebug(),
            null);

        float expectedZoom = ((512f - (settings.Padding * 2f)) / (20f * 2f)) / settings.CameraDistanceScale;
        AssertNearlyEqual(30f, exportState.OrbitYawDeg, 1e-5f, "fallback viewport yaw");
        AssertNearlyEqual(-20f, exportState.OrbitPitchDeg, 1e-5f, "fallback viewport pitch");
        AssertNearlyEqual(expectedZoom, exportState.Zoom, 1e-4f, "fallback viewport zoom");
        AssertNearlyEqual(0f, exportState.PanPx.X, 1e-5f, "fallback viewport pan X");
        AssertNearlyEqual(0f, exportState.PanPx.Y, 1e-5f, "fallback viewport pan Y");
        AssertNearlyEqual(80f, fallbackCamera.Position.Length(), 1e-4f, "fallback export camera distance");
        AssertNearlyEqual(expectedZoom, fallbackCamera.Zoom, 1e-4f, "fallback export camera zoom");
    }

    private static List<ProjectTypeTransitionSample> BuildProjectTypeTransitionSamples()
    {
        var samples = new List<ProjectTypeTransitionSample>();

        foreach (InteractorProjectType fromType in ProjectTypeMatrix)
        {
            foreach (InteractorProjectType toType in ProjectTypeMatrix)
            {
                if (fromType == toType)
                {
                    continue;
                }

                var project = new KnobProject();
                project.ApplyInteractorProjectTypeDefaults(fromType);
                ApplyLensStressState(project, fromType, toType);
                SelectStressNodeForProjectType(project, fromType);
                ProjectTypeStateSnapshot before = CaptureProjectTypeState(project);

                project.ApplyInteractorProjectTypeDefaults(toType);
                AssertSelectionIsValid(project, $"{fromType}->{toType} after");
                ProjectTypeStateSnapshot after = CaptureProjectTypeState(project);

                ApplyProjectTypeState(project, before);
                AssertSelectionIsValid(project, $"{fromType}->{toType} undo");
                ProjectTypeStateSnapshot undo = CaptureProjectTypeState(project);

                ApplyProjectTypeState(project, after);
                AssertSelectionIsValid(project, $"{fromType}->{toType} redo");
                ProjectTypeStateSnapshot redo = CaptureProjectTypeState(project);

                samples.Add(new ProjectTypeTransitionSample(
                    fromType,
                    toType,
                    before,
                    after,
                    undo,
                    redo));
            }
        }

        return samples;
    }

    private static void ApplyLensStressState(KnobProject project, InteractorProjectType fromType, InteractorProjectType toType)
    {
        float fromSeed = (int)fromType + 1;
        float toSeed = (int)toType + 1;
        float combined = fromSeed + (toSeed * 0.5f);

        project.IndicatorLensTransmission = Math.Clamp(0.38f + (combined * 0.045f), 0f, 1f);
        project.IndicatorLensIor = Math.Clamp(1.28f + (fromSeed * 0.09f) + (toSeed * 0.03f), 1f, 2.5f);
        project.IndicatorLensThickness = Math.Clamp(0.45f + (combined * 0.2f), 0f, 10f);
        project.IndicatorLensTint = new Vector3(
            Math.Clamp(0.55f + (fromSeed * 0.035f), 0f, 1f),
            Math.Clamp(0.58f + (toSeed * 0.04f), 0f, 1f),
            Math.Clamp(0.52f + (combined * 0.025f), 0f, 1f));
        project.IndicatorLensAbsorption = Math.Clamp(0.35f + (combined * 0.3f), 0f, 8f);
        project.IndicatorLensSurfaceRoughness = Math.Clamp(0.08f + (fromSeed * 0.04f), 0.04f, 1f);
        project.IndicatorLensSurfaceSpecularStrength = Math.Clamp(0.75f + (toSeed * 0.18f), 0f, 2.5f);
    }

    private static void SelectStressNodeForProjectType(KnobProject project, InteractorProjectType projectType)
    {
        if (projectType == InteractorProjectType.RotaryKnob)
        {
            project.SetSelectedNode(project.EnsureCollarNode());
        }
        else
        {
            project.SetSelectedNode(project.EnsureMaterialNode());
        }

        project.EnsureSelection();
    }

    private static ProjectTypeStateSnapshot CaptureProjectTypeState(KnobProject project)
    {
        ModelNode model = project.EnsureModelNode();
        bool hasMaterialNode = model.Children.OfType<MaterialNode>().Any();
        bool hasCollarNode = model.Children.OfType<CollarNode>().Any();

        return new ProjectTypeStateSnapshot(
            project.ProjectType,
            project.SliderMode,
            project.ToggleMode,
            hasMaterialNode,
            hasCollarNode,
            ClassifySelectedNode(project.SelectedNode),
            project.SelectedLightIndex,
            project.IndicatorLensTransmission,
            project.IndicatorLensIor,
            project.IndicatorLensThickness,
            project.IndicatorLensTint.X,
            project.IndicatorLensTint.Y,
            project.IndicatorLensTint.Z,
            project.IndicatorLensAbsorption,
            project.IndicatorLensSurfaceRoughness,
            project.IndicatorLensSurfaceSpecularStrength);
    }

    private static void ApplyProjectTypeState(KnobProject project, ProjectTypeStateSnapshot snapshot)
    {
        project.ApplyInteractorProjectTypeDefaults(snapshot.ProjectType);
        project.SliderMode = snapshot.SliderMode;
        project.ToggleMode = snapshot.ToggleMode;
        project.IndicatorLensTransmission = snapshot.IndicatorLensTransmission;
        project.IndicatorLensIor = snapshot.IndicatorLensIor;
        project.IndicatorLensThickness = snapshot.IndicatorLensThickness;
        project.IndicatorLensTint = new Vector3(
            snapshot.IndicatorLensTintX,
            snapshot.IndicatorLensTintY,
            snapshot.IndicatorLensTintZ);
        project.IndicatorLensAbsorption = snapshot.IndicatorLensAbsorption;
        project.IndicatorLensSurfaceRoughness = snapshot.IndicatorLensSurfaceRoughness;
        project.IndicatorLensSurfaceSpecularStrength = snapshot.IndicatorLensSurfaceSpecularStrength;

        ModelNode model = project.EnsureModelNode();
        if (snapshot.HasMaterialNode)
        {
            project.EnsureMaterialNode();
        }

        bool hasCollarNode = model.Children.OfType<CollarNode>().Any();
        if (snapshot.HasCollarNode && !hasCollarNode)
        {
            project.EnsureCollarNode();
        }
        else if (!snapshot.HasCollarNode && hasCollarNode)
        {
            project.RemoveCollarNode();
        }

        if (project.Lights.Count > 0)
        {
            int clampedLightIndex = Math.Clamp(snapshot.SelectedLightIndex, 0, project.Lights.Count - 1);
            project.SetSelectedLightIndex(clampedLightIndex);
        }

        project.SetSelectedNode(snapshot.SelectedNodeKind switch
        {
            SelectedNodeKind.SceneRoot => project.SceneRoot,
            SelectedNodeKind.Model => project.EnsureModelNode(),
            SelectedNodeKind.Material => project.EnsureMaterialNode(),
            SelectedNodeKind.Collar => project.EnsureCollarNode(),
            SelectedNodeKind.Light => ResolveSelectedLightNode(project),
            _ => project.EnsureModelNode()
        });

        project.EnsureSelection();
    }

    private static SelectedNodeKind ClassifySelectedNode(SceneNode? selectedNode)
    {
        return selectedNode switch
        {
            null => SelectedNodeKind.None,
            SceneRootNode => SelectedNodeKind.SceneRoot,
            ModelNode => SelectedNodeKind.Model,
            MaterialNode => SelectedNodeKind.Material,
            CollarNode => SelectedNodeKind.Collar,
            LightNode => SelectedNodeKind.Light,
            _ => SelectedNodeKind.Unknown
        };
    }

    private static SceneNode ResolveSelectedLightNode(KnobProject project)
    {
        if (project.Lights.Count == 0)
        {
            return project.EnsureModelNode();
        }

        int index = Math.Clamp(project.SelectedLightIndex, 0, project.Lights.Count - 1);
        KnobLight light = project.Lights[index];
        SceneNode? selectedLightNode = project.SceneRoot.Children
            .OfType<LightNode>()
            .FirstOrDefault(node => ReferenceEquals(node.Light, light));
        return selectedLightNode ?? project.EnsureModelNode();
    }

    private static void AssertProjectTypeDefaults(KnobProject project, InteractorProjectType type, string context)
    {
        if (project.ProjectType != type)
        {
            throw new InvalidOperationException($"expected ProjectType={type} after {context}, got {project.ProjectType}.");
        }

        (SliderAssemblyMode expectedSliderMode, ToggleAssemblyMode expectedToggleMode, bool expectedCollarNode) =
            ResolveExpectedDefaults(type);

        if (project.SliderMode != expectedSliderMode)
        {
            throw new InvalidOperationException(
                $"expected SliderMode={expectedSliderMode} after {context}, got {project.SliderMode}.");
        }

        if (project.ToggleMode != expectedToggleMode)
        {
            throw new InvalidOperationException(
                $"expected ToggleMode={expectedToggleMode} after {context}, got {project.ToggleMode}.");
        }

        ModelNode model = project.EnsureModelNode();
        bool hasMaterialNode = model.Children.OfType<MaterialNode>().Any();
        bool hasCollarNode = model.Children.OfType<CollarNode>().Any();

        if (!hasMaterialNode)
        {
            throw new InvalidOperationException($"expected material node after {context}.");
        }

        if (hasCollarNode != expectedCollarNode)
        {
            throw new InvalidOperationException(
                $"expected HasCollarNode={expectedCollarNode} after {context}, got {hasCollarNode}.");
        }
    }

    private static void AssertSelectionIsValid(KnobProject project, string context)
    {
        project.EnsureSelection();
        SceneNode? selectedNode = project.SelectedNode;
        if (selectedNode == null)
        {
            throw new InvalidOperationException($"selection was null for {context}.");
        }

        if (!ContainsNode(project.SceneRoot, selectedNode.Id))
        {
            throw new InvalidOperationException($"selection points to non-scene node for {context}.");
        }
    }

    private static bool ContainsNode(SceneNode root, Guid id)
    {
        if (root.Id == id)
        {
            return true;
        }

        foreach (SceneNode child in root.Children)
        {
            if (ContainsNode(child, id))
            {
                return true;
            }
        }

        return false;
    }

    private static (SliderAssemblyMode SliderMode, ToggleAssemblyMode ToggleMode, bool HasCollarNode)
        ResolveExpectedDefaults(InteractorProjectType type)
    {
        return type switch
        {
            InteractorProjectType.ThumbSlider => (SliderAssemblyMode.Enabled, ToggleAssemblyMode.Disabled, false),
            InteractorProjectType.FlipSwitch => (SliderAssemblyMode.Disabled, ToggleAssemblyMode.Enabled, false),
            InteractorProjectType.PushButton => (SliderAssemblyMode.Disabled, ToggleAssemblyMode.Disabled, false),
            InteractorProjectType.IndicatorLight => (SliderAssemblyMode.Disabled, ToggleAssemblyMode.Disabled, false),
            _ => (SliderAssemblyMode.Disabled, ToggleAssemblyMode.Disabled, true)
        };
    }

    private enum SelectedNodeKind
    {
        None = 0,
        SceneRoot = 1,
        Model = 2,
        Material = 3,
        Collar = 4,
        Light = 5,
        Unknown = 6
    }

    private sealed record ProjectTypeStateSnapshot(
        InteractorProjectType ProjectType,
        SliderAssemblyMode SliderMode,
        ToggleAssemblyMode ToggleMode,
        bool HasMaterialNode,
        bool HasCollarNode,
        SelectedNodeKind SelectedNodeKind,
        int SelectedLightIndex,
        float IndicatorLensTransmission,
        float IndicatorLensIor,
        float IndicatorLensThickness,
        float IndicatorLensTintX,
        float IndicatorLensTintY,
        float IndicatorLensTintZ,
        float IndicatorLensAbsorption,
        float IndicatorLensSurfaceRoughness,
        float IndicatorLensSurfaceSpecularStrength);

    private sealed record ProjectTypeTransitionSample(
        InteractorProjectType FromType,
        InteractorProjectType ToType,
        ProjectTypeStateSnapshot Before,
        ProjectTypeStateSnapshot After,
        ProjectTypeStateSnapshot Undo,
        ProjectTypeStateSnapshot Redo);

    private sealed record IndicatorLensMaterialSnapshot(
        float Transmission,
        float Ior,
        float Thickness,
        float TintX,
        float TintY,
        float TintZ,
        float Absorption,
        float SurfaceRoughness,
        float SurfaceSpecularStrength);

    private static string BuildSnapshotJson()
    {
        var snapshot = new
        {
            Lighting = new
            {
                Mode = "Studio",
                Lights = new[]
                {
                    new { Name = "Key", Type = "Point", X = -0.4f, Y = 0.8f, Z = 1.1f, Intensity = 1.2f }
                }
            },
            Environment = new
            {
                Intensity = 0.62f,
                RoughnessMix = 0.35f
            },
            Shadows = new
            {
                Enabled = true,
                Strength = 0.68f,
                Softness = 0.34f
            },
            Collar = new
            {
                Enabled = true,
                Preset = "ImportedStl",
                ImportedMeshPath = "/tmp/collar_models/dragon.glb",
                ImportedScale = 1.09f,
                ImportedMirrorX = true,
                ImportedMirrorY = false,
                ImportedMirrorZ = true
            }
        };

        return JsonSerializer.Serialize(snapshot);
    }

    private static T InvokePrivateStatic<T>(Type ownerType, string methodName, params object?[] arguments)
    {
        MethodInfo? method = ownerType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException($"could not locate {ownerType.Name}.{methodName}.");
        }

        object? result = method.Invoke(null, arguments);
        if (result is T typedResult)
        {
            return typedResult;
        }

        throw new InvalidOperationException($"{ownerType.Name}.{methodName} returned an unexpected result.");
    }

    private static void AssertNearlyEqual(float expected, float actual, float tolerance, string fieldName)
    {
        if (MathF.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{fieldName} mismatch. expected {expected:0.######}, got {actual:0.######}.");
        }
    }

    private static void AssertHasProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out _))
        {
            throw new InvalidOperationException($"missing required property '{name}'.");
        }
    }

    private static void AssertEqual(string? expected, string? actual, string fieldName)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{fieldName} mismatch.");
        }
    }
}
