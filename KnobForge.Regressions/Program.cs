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
            RunTest("Project-type switch matrix keeps expected defaults + valid selection", failures, ProjectTypeSwitchMatrixMaintainsExpectedDefaultsAndSelection);
            RunTest("Project-type switch undo/redo replay restores exact state", failures, ProjectTypeSwitchUndoRedoReplayRestoresExactState);
            RunTest("Project-type switch transition states round-trip via envelope", failures, () => ProjectTypeSwitchTransitionStatesRoundTripViaEnvelope(root));
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

    private static readonly InteractorProjectType[] ProjectTypeMatrix =
    {
        InteractorProjectType.RotaryKnob,
        InteractorProjectType.ThumbSlider,
        InteractorProjectType.FlipSwitch,
        InteractorProjectType.PushButton
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
            project.SelectedLightIndex);
    }

    private static void ApplyProjectTypeState(KnobProject project, ProjectTypeStateSnapshot snapshot)
    {
        project.ApplyInteractorProjectTypeDefaults(snapshot.ProjectType);
        project.SliderMode = snapshot.SliderMode;
        project.ToggleMode = snapshot.ToggleMode;

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
        int SelectedLightIndex);

    private sealed record ProjectTypeTransitionSample(
        InteractorProjectType FromType,
        InteractorProjectType ToType,
        ProjectTypeStateSnapshot Before,
        ProjectTypeStateSnapshot After,
        ProjectTypeStateSnapshot Undo,
        ProjectTypeStateSnapshot Redo);

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
