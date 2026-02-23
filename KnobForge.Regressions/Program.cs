using KnobForge.App.ProjectFiles;
using KnobForge.Core;
using KnobForge.Core.Scene;
using System.Text.Json;

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
            RunTest("Project type resolver infers slider legacy snapshots", failures, ProjectTypeResolverInfersSliderLegacySnapshots);
            RunTest("Project type resolver infers toggle legacy snapshots", failures, ProjectTypeResolverInfersToggleLegacySnapshots);
            RunTest("Project type resolver defaults to rotary when ambiguous", failures, ProjectTypeResolverDefaultsToRotaryWhenAmbiguous);
            RunTest("Hybrid modules prune collar on slider defaults", failures, HybridModulesPruneCollarOnSliderDefaults);
            RunTest("Hybrid modules reattach collar for rotary defaults", failures, HybridModulesReattachCollarForRotaryDefaults);
            RunTest("Hybrid modules keep collar when prune disabled", failures, HybridModulesKeepCollarWhenPruneDisabled);
            RunTest("Removing collar reselects model node", failures, RemovingCollarReselectsModelNode);
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
            ToggleTipRadius: 0f,
            ToggleStateCount: ToggleAssemblyStateCount.TwoPosition,
            ToggleMaxAngleDeg: 24f);

        InteractorProjectType resolved = InteractorProjectTypeResolver.ResolveFromSnapshotHint(hint);
        if (resolved != InteractorProjectType.ThumbSlider)
        {
            throw new InvalidOperationException($"expected ThumbSlider, got {resolved}.");
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
            ToggleTipRadius: 0f,
            ToggleStateCount: ToggleAssemblyStateCount.TwoPosition,
            ToggleMaxAngleDeg: 24f);

        InteractorProjectType resolved = InteractorProjectTypeResolver.ResolveFromSnapshotHint(hint);
        if (resolved != InteractorProjectType.RotaryKnob)
        {
            throw new InvalidOperationException($"expected RotaryKnob, got {resolved}.");
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
