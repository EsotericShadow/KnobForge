using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Avalonia;
using KnobForge.Core;
using KnobForge.Core.Scene;
using KnobForge.Rendering;

namespace KnobForge.App.Controls
{
    public sealed partial class MetalViewport
    {
        public void RefreshPaintHud()
        {
            PublishPaintHudSnapshot();
        }

        public IReadOnlyList<PaintLayerInfo> GetPaintLayers()
        {
            EnsureDefaultPaintLayer();
            var result = new PaintLayerInfo[_paintLayers.Count];
            for (int i = 0; i < _paintLayers.Count; i++)
            {
                PaintLayerState layer = _paintLayers[i];
                result[i] = new PaintLayerInfo(
                    i,
                    layer.Name,
                    i == _activePaintLayerIndex,
                    i == _focusedPaintLayerIndex,
                    layer.Opacity,
                    layer.BlendMode,
                    layer.Visible);
            }

            return result;
        }

        public bool AddPaintLayer(string? name = null)
        {
            EnsureDefaultPaintLayer();
            string layerName = NormalizePaintLayerName(name);
            if (string.IsNullOrWhiteSpace(layerName))
            {
                layerName = BuildNextPaintLayerName();
            }

            _paintLayers.Add(new PaintLayerState(layerName));
            SyncProjectPaintLayersFromViewport();
            _activePaintLayerIndex = _paintLayers.Count - 1;
            InvalidateGpu();
            RaisePaintLayersChanged();
            return true;
        }

        public bool RenamePaintLayer(int index, string? name)
        {
            EnsureDefaultPaintLayer();
            if (index < 0 || index >= _paintLayers.Count)
            {
                return false;
            }

            string normalized = NormalizePaintLayerName(name);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            _paintLayers[index].Name = normalized;
            SyncProjectPaintLayerMetadata(index);
            RaisePaintLayersChanged();
            return true;
        }

        public bool DeletePaintLayer(int index)
        {
            EnsureDefaultPaintLayer();
            if (_paintLayers.Count <= 1 || index < 0 || index >= _paintLayers.Count)
            {
                return false;
            }

            _paintLayers.RemoveAt(index);
            SyncProjectPaintLayersFromViewport();
            _activePaintLayerIndex = Math.Clamp(_activePaintLayerIndex, 0, _paintLayers.Count - 1);
            if (_focusedPaintLayerIndex == index)
            {
                _focusedPaintLayerIndex = -1;
            }
            else if (_focusedPaintLayerIndex > index)
            {
                _focusedPaintLayerIndex--;
            }

            if (_activeStrokeLayerIndex == index)
            {
                _activeStrokeLayerIndex = _activePaintLayerIndex;
            }
            else if (_activeStrokeLayerIndex > index)
            {
                _activeStrokeLayerIndex--;
            }

            RemapQueuedPaintCommandsAfterLayerDelete(_pendingPaintStampCommands, index);
            RemapQueuedPaintCommandsAfterLayerDelete(_activeStrokeCommands, index);

            bool historyShifted = false;
            for (int i = _committedPaintStrokes.Count - 1; i >= 0; i--)
            {
                PaintStrokeRecord stroke = _committedPaintStrokes[i];
                if (stroke.LayerIndex == index)
                {
                    _committedPaintStrokes.RemoveAt(i);
                    historyShifted = true;
                    continue;
                }

                if (stroke.LayerIndex > index)
                {
                    PaintStampCommand[] shifted = new PaintStampCommand[stroke.Commands.Length];
                    for (int c = 0; c < stroke.Commands.Length; c++)
                    {
                        PaintStampCommand command = stroke.Commands[c];
                        shifted[c] = command with { LayerIndex = command.LayerIndex - 1 };
                    }

                    _committedPaintStrokes[i] = new PaintStrokeRecord(stroke.LayerIndex - 1, shifted);
                    historyShifted = true;
                }
            }

            if (historyShifted)
            {
                _paintHistoryRevision = Math.Clamp(_paintHistoryRevision, 0, _committedPaintStrokes.Count);
                RaisePaintHistoryRevisionChanged();
            }

            RebuildProjectPaintStateFromHistory();
            InvalidatePaintTexturesForDisplayState();
            InvalidateGpu();
            RaisePaintLayersChanged();
            return true;
        }

        public bool SetPaintLayerOpacity(int index, float opacity)
        {
            EnsureDefaultPaintLayer();
            if (index < 0 || index >= _paintLayers.Count)
            {
                return false;
            }

            float clamped = Math.Clamp(opacity, 0f, 1f);
            if (MathF.Abs(_paintLayers[index].Opacity - clamped) <= 1e-6f)
            {
                return false;
            }

            _paintLayers[index].Opacity = clamped;
            SyncProjectPaintLayerMetadata(index);
            InvalidatePaintTexturesForDisplayState();
            InvalidateGpu();
            RaisePaintLayersChanged();
            return true;
        }

        public bool SetPaintLayerBlendMode(int index, PaintBlendMode blendMode)
        {
            EnsureDefaultPaintLayer();
            if (index < 0 || index >= _paintLayers.Count)
            {
                return false;
            }

            if (_paintLayers[index].BlendMode == blendMode)
            {
                return false;
            }

            _paintLayers[index].BlendMode = blendMode;
            SyncProjectPaintLayerMetadata(index);
            InvalidatePaintTexturesForDisplayState();
            InvalidateGpu();
            RaisePaintLayersChanged();
            return true;
        }

        public bool SetPaintLayerVisible(int index, bool visible)
        {
            EnsureDefaultPaintLayer();
            if (index < 0 || index >= _paintLayers.Count)
            {
                return false;
            }

            if (_paintLayers[index].Visible == visible)
            {
                return false;
            }

            _paintLayers[index].Visible = visible;
            SyncProjectPaintLayerMetadata(index);
            InvalidatePaintTexturesForDisplayState();
            InvalidateGpu();
            RaisePaintLayersChanged();
            return true;
        }

        public bool SetActivePaintLayer(int index)
        {
            EnsureDefaultPaintLayer();
            int clamped = Math.Clamp(index, 0, _paintLayers.Count - 1);
            if (_activePaintLayerIndex == clamped)
            {
                return false;
            }

            _activePaintLayerIndex = clamped;
            RaisePaintLayersChanged();
            return true;
        }

        public bool SetFocusedPaintLayer(int index)
        {
            EnsureDefaultPaintLayer();
            int clamped = index < 0 ? -1 : Math.Clamp(index, 0, _paintLayers.Count - 1);
            if (_focusedPaintLayerIndex == clamped)
            {
                return false;
            }

            _focusedPaintLayerIndex = clamped;
            InvalidatePaintTexturesForDisplayState();
            InvalidateGpu();
            RaisePaintLayersChanged();
            return true;
        }

        public bool ClearPaintToRevisionZero()
        {
            if (_paintHistoryRevision == 0 && _pendingPaintStampCommands.Count == 0 && _activeStrokeCommands.Count == 0)
            {
                return false;
            }

            _pendingPaintStampCommands.Clear();
            _activeStrokeCommands.Clear();
            _paintHistoryRevision = 0;
            _project?.ClearPaintMask();
            InvalidatePaintTexturesForDisplayState();
            _paintColorTextureNeedsClear = true;
            InvalidateGpu();
            RaisePaintHistoryRevisionChanged();
            return true;
        }

        public bool RestorePaintHistoryRevision(int revision)
        {
            int clamped = Math.Clamp(revision, 0, _committedPaintStrokes.Count);
            if (_paintHistoryRevision == clamped)
            {
                return false;
            }

            _pendingPaintStampCommands.Clear();
            _activeStrokeCommands.Clear();
            _paintHistoryRevision = clamped;
            RebuildProjectPaintStateFromHistory();
            InvalidatePaintTexturesForDisplayState();
            _paintColorTextureNeedsClear = true;
            InvalidateGpu();
            RaisePaintHistoryRevisionChanged();
            return true;
        }

        public bool ResetPaintStateForMaskResize()
        {
            bool hadData = _paintHistoryRevision > 0 ||
                _pendingPaintStampCommands.Count > 0 ||
                _activeStrokeCommands.Count > 0 ||
                _committedPaintStrokes.Count > 0;
            if (!hadData)
            {
                return false;
            }

            _pendingPaintStampCommands.Clear();
            _activeStrokeCommands.Clear();
            _committedPaintStrokes.Clear();
            _paintHistoryRevision = 0;
            _project?.ClearPaintMask();
            InvalidatePaintTexturesForDisplayState();
            _paintColorTextureNeedsClear = true;
            _activeStrokeLayerIndex = _activePaintLayerIndex;
            InvalidateGpu();
            RaisePaintHistoryRevisionChanged();
            PublishPaintHudSnapshot();
            return true;
        }

        private void EnsureDefaultPaintLayer()
        {
            if (_paintLayers.Count > 0)
            {
                _activePaintLayerIndex = Math.Clamp(_activePaintLayerIndex, 0, _paintLayers.Count - 1);
                if (_focusedPaintLayerIndex >= _paintLayers.Count)
                {
                    _focusedPaintLayerIndex = -1;
                }

                return;
            }

            _paintLayers.Add(new PaintLayerState("Layer 1"));
            SyncProjectPaintLayersFromViewport();
            _activePaintLayerIndex = 0;
            if (_focusedPaintLayerIndex >= 0)
            {
                _focusedPaintLayerIndex = 0;
            }
        }

        private static string NormalizePaintLayerName(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private string BuildNextPaintLayerName()
        {
            int ordinal = _paintLayers.Count + 1;
            string candidate = $"Layer {ordinal}";
            while (_paintLayers.Any(layer => string.Equals(layer.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                ordinal++;
                candidate = $"Layer {ordinal}";
            }

            return candidate;
        }

        private static void RemapQueuedPaintCommandsAfterLayerDelete(List<PaintStampCommand> commands, int deletedLayerIndex)
        {
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                PaintStampCommand command = commands[i];
                if (command.LayerIndex == deletedLayerIndex)
                {
                    commands.RemoveAt(i);
                    continue;
                }

                if (command.LayerIndex > deletedLayerIndex)
                {
                    commands[i] = command with { LayerIndex = command.LayerIndex - 1 };
                }
            }
        }

        private void RaisePaintLayersChanged()
        {
            PaintLayersChanged?.Invoke();
        }

        private void RaisePaintHistoryRevisionChanged()
        {
            PaintHistoryRevisionChanged?.Invoke(_paintHistoryRevision);
        }

        public string ExportPaintStateJson()
        {
            EnsureDefaultPaintLayer();

            var layers = new List<PaintLayerPersisted>(_paintLayers.Count);
            foreach (PaintLayerState layer in _paintLayers)
            {
                layers.Add(new PaintLayerPersisted
                {
                    Name = layer.Name,
                    Opacity = layer.Opacity,
                    BlendMode = layer.BlendMode,
                    Visible = layer.Visible
                });
            }

            var strokes = new List<PaintStrokePersisted>(_committedPaintStrokes.Count);
            foreach (PaintStrokeRecord stroke in _committedPaintStrokes)
            {
                var commands = new List<PaintStampPersisted>(stroke.Commands.Length);
                for (int i = 0; i < stroke.Commands.Length; i++)
                {
                    PaintStampCommand command = stroke.Commands[i];
                    commands.Add(new PaintStampPersisted
                    {
                        UvX = command.UvCenter.X,
                        UvY = command.UvCenter.Y,
                        UvRadius = command.UvRadius,
                        Opacity = command.Opacity,
                        Spread = command.Spread,
                        Channel = command.Channel,
                        BrushType = command.BrushType,
                        ScratchAbrasionType = command.ScratchAbrasionType,
                        PaintColorX = command.PaintColor.X,
                        PaintColorY = command.PaintColor.Y,
                        PaintColorZ = command.PaintColor.Z,
                        TargetValue = command.TargetValue,
                        Seed = command.Seed,
                        LayerIndex = command.LayerIndex
                    });
                }

                strokes.Add(new PaintStrokePersisted
                {
                    LayerIndex = stroke.LayerIndex,
                    Commands = commands
                });
            }

            var state = new PaintProjectState
            {
                Layers = layers,
                ActiveLayerIndex = _activePaintLayerIndex,
                FocusedLayerIndex = _focusedPaintLayerIndex,
                PaintHistoryRevision = _paintHistoryRevision,
                Strokes = strokes
            };

            return JsonSerializer.Serialize(state, ProjectStateJsonOptions);
        }

        public bool TryImportPaintStateJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            PaintProjectState? state;
            try
            {
                state = JsonSerializer.Deserialize<PaintProjectState>(json, ProjectStateJsonOptions);
            }
            catch
            {
                return false;
            }

            if (state == null)
            {
                return false;
            }

            _pendingPaintStampCommands.Clear();
            _activeStrokeCommands.Clear();
            _committedPaintStrokes.Clear();
            _paintLayers.Clear();

            if (state.Layers != null)
            {
                for (int i = 0; i < state.Layers.Count; i++)
                {
                    string name = NormalizePaintLayerName(state.Layers[i].Name);
                    _paintLayers.Add(new PaintLayerState(string.IsNullOrWhiteSpace(name) ? $"Layer {i + 1}" : name)
                    {
                        Opacity = Math.Clamp(state.Layers[i].Opacity, 0f, 1f),
                        BlendMode = state.Layers[i].BlendMode,
                        Visible = state.Layers[i].Visible
                    });
                }
            }

            EnsureDefaultPaintLayer();
            int layerCount = _paintLayers.Count;

            if (state.Strokes != null)
            {
                for (int i = 0; i < state.Strokes.Count; i++)
                {
                    PaintStrokePersisted stroke = state.Strokes[i];
                    if (stroke.Commands == null || stroke.Commands.Count == 0)
                    {
                        continue;
                    }

                    PaintStampCommand[] commands = new PaintStampCommand[stroke.Commands.Count];
                    for (int c = 0; c < stroke.Commands.Count; c++)
                    {
                        PaintStampPersisted persisted = stroke.Commands[c];
                        int commandLayer = Math.Clamp(persisted.LayerIndex, 0, Math.Max(0, layerCount - 1));
                        commands[c] = new PaintStampCommand(
                            UvCenter: new Vector2(persisted.UvX, persisted.UvY),
                            UvRadius: MathF.Max(1e-6f, persisted.UvRadius),
                            Opacity: Math.Clamp(persisted.Opacity, 0f, 1f),
                            Spread: Math.Clamp(persisted.Spread, 0f, 1f),
                            Channel: persisted.Channel,
                            BrushType: persisted.BrushType,
                            ScratchAbrasionType: persisted.ScratchAbrasionType,
                            PaintColor: new Vector3(
                                Math.Clamp(persisted.PaintColorX, 0f, 1f),
                                Math.Clamp(persisted.PaintColorY, 0f, 1f),
                                Math.Clamp(persisted.PaintColorZ, 0f, 1f)),
                            TargetValue: Math.Clamp(persisted.TargetValue, 0f, 1f),
                            Seed: persisted.Seed,
                            LayerIndex: commandLayer);
                    }

                    int strokeLayer = Math.Clamp(stroke.LayerIndex, 0, Math.Max(0, layerCount - 1));
                    _committedPaintStrokes.Add(new PaintStrokeRecord(strokeLayer, commands));
                }
            }

            _activePaintLayerIndex = Math.Clamp(state.ActiveLayerIndex, 0, Math.Max(0, _paintLayers.Count - 1));
            _focusedPaintLayerIndex = state.FocusedLayerIndex < 0
                ? -1
                : Math.Clamp(state.FocusedLayerIndex, 0, Math.Max(0, _paintLayers.Count - 1));
            _paintHistoryRevision = Math.Clamp(state.PaintHistoryRevision, 0, _committedPaintStrokes.Count);
            _activeStrokeLayerIndex = _activePaintLayerIndex;
            _paintColorTextureNeedsClear = true;
            RebuildProjectPaintStateFromHistory();
            InvalidatePaintTexturesForDisplayState();

            RaisePaintLayersChanged();
            RaisePaintHistoryRevisionChanged();
            PublishPaintHudSnapshot();
            InvalidateGpu();
            return true;
        }

        private void SyncProjectPaintLayersFromViewport()
        {
            if (_project is null)
            {
                return;
            }

            _project.EnsurePaintLayerCount(_paintLayers.Count);
            for (int i = 0; i < _paintLayers.Count; i++)
            {
                SyncProjectPaintLayerMetadata(i);
            }
        }

        private void SyncProjectPaintLayerMetadata(int index)
        {
            if (_project is null || index < 0 || index >= _paintLayers.Count)
            {
                return;
            }

            PaintLayerState layer = _paintLayers[index];
            _project.SetPaintLayerProperties(index, layer.Name, layer.Opacity, layer.BlendMode, layer.Visible);
        }

        private void RebuildProjectPaintStateFromHistory()
        {
            if (_project is null)
            {
                return;
            }

            _project.BeginPaintRecomposeBatch();
            try
            {
                SyncProjectPaintLayersFromViewport();
                _project.ClearPaintMask();
                int clampedRevision = Math.Clamp(_paintHistoryRevision, 0, _committedPaintStrokes.Count);
                for (int strokeIndex = 0; strokeIndex < clampedRevision; strokeIndex++)
                {
                    PaintStrokeRecord stroke = _committedPaintStrokes[strokeIndex];
                    for (int commandIndex = 0; commandIndex < stroke.Commands.Length; commandIndex++)
                    {
                        ApplyPaintCommandToProject(stroke.Commands[commandIndex]);
                    }
                }
            }
            finally
            {
                _project.EndPaintRecomposeBatch();
            }
        }

        private void ApplyPaintCommandToProject(PaintStampCommand command)
        {
            _project?.StampPaintMaskUv(
                command.LayerIndex,
                command.UvCenter,
                command.UvRadius,
                command.BrushType,
                command.ScratchAbrasionType,
                command.Channel,
                command.Opacity,
                command.Spread,
                command.Seed,
                command.PaintColor,
                command.TargetValue);
        }

        /// <summary>
        /// Mirrors all stored stroke UVs on the specified axis, then rebuilds
        /// the paint mask from scratch. Use this to repair legacy projects
        /// whose paint history was recorded with a flipped brush mapping.
        /// </summary>
        public void MirrorPaintHistoryUvs(bool mirrorX, bool mirrorY)
        {
            if (!mirrorX && !mirrorY)
            {
                return;
            }

            for (int s = 0; s < _committedPaintStrokes.Count; s++)
            {
                PaintStrokeRecord stroke = _committedPaintStrokes[s];
                PaintStampCommand[] mirrored = new PaintStampCommand[stroke.Commands.Length];
                for (int c = 0; c < stroke.Commands.Length; c++)
                {
                    PaintStampCommand cmd = stroke.Commands[c];
                    float uvX = mirrorX ? (1.0f - cmd.UvCenter.X) : cmd.UvCenter.X;
                    float uvY = mirrorY ? (1.0f - cmd.UvCenter.Y) : cmd.UvCenter.Y;
                    mirrored[c] = new PaintStampCommand(
                        UvCenter: new Vector2(uvX, uvY),
                        UvRadius: cmd.UvRadius,
                        Opacity: cmd.Opacity,
                        Spread: cmd.Spread,
                        Channel: cmd.Channel,
                        BrushType: cmd.BrushType,
                        ScratchAbrasionType: cmd.ScratchAbrasionType,
                        PaintColor: cmd.PaintColor,
                        TargetValue: cmd.TargetValue,
                        Seed: cmd.Seed,
                        LayerIndex: cmd.LayerIndex);
                }

                _committedPaintStrokes[s] = new PaintStrokeRecord(stroke.LayerIndex, mirrored);
            }

            _paintColorTextureNeedsClear = true;
            RebuildProjectPaintStateFromHistory();
            InvalidatePaintTexturesForDisplayState();
            InvalidateGpu();
        }

        private void InvalidatePaintTexturesForDisplayState()
        {
            _paintMaskTextureVersion = -1;
            _paintColorTextureVersion = -1;
            _paintMask2TextureVersion = -1;
            _paintTextureFocusedLayerIndex = int.MinValue;
        }

        public string ExportViewportStateJson()
        {
            var state = new ViewportProjectState
            {
                OrbitYawDeg = _orbitYawDeg,
                OrbitPitchDeg = _orbitPitchDeg,
                Zoom = _zoom,
                PanX = _panPx.X,
                PanY = _panPx.Y,
                Orientation = new OrientationProjectState
                {
                    InvertX = _orientation.InvertX,
                    InvertY = _orientation.InvertY,
                    InvertZ = _orientation.InvertZ,
                    FlipCamera180 = _orientation.FlipCamera180
                },
                GizmoInvertX = _gizmoInvertX,
                GizmoInvertY = _gizmoInvertY,
                GizmoInvertZ = _gizmoInvertZ,
                BrushInvertX = _brushInvertX,
                BrushInvertY = _brushInvertY,
                BrushInvertZ = _brushInvertZ,
                LightEffectInvertX = _lightEffectInvertX,
                LightEffectInvertY = _lightEffectInvertY,
                LightEffectInvertZ = _lightEffectInvertZ,
                CollarCompensationInvertX = _collarCompensationInvertX,
                CollarCompensationInvertY = _collarCompensationInvertY,
                CollarCompensationInvertZ = _collarCompensationInvertZ,
                BloomCompositeInvertX = _bloomCompositeInvertX,
                BloomCompositeInvertY = _bloomCompositeInvertY,
                InvertImportedCollarOrbit = false,
                InvertKnobFrontFaceWinding = _invertKnobFrontFaceWinding,
                InvertImportedStlFrontFaceWinding = _invertImportedStlFrontFaceWinding
            };

            return JsonSerializer.Serialize(state, ProjectStateJsonOptions);
        }

        public bool TryImportViewportStateJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            ViewportProjectState? state;
            try
            {
                state = JsonSerializer.Deserialize<ViewportProjectState>(json, ProjectStateJsonOptions);
            }
            catch
            {
                return false;
            }

            if (state == null)
            {
                return false;
            }

            _orbitYawDeg = state.OrbitYawDeg;
            _orbitPitchDeg = Math.Clamp(state.OrbitPitchDeg, -89f, 89f);
            _zoom = Math.Clamp(state.Zoom, 0.2f, 8f);
            _panPx = new Vector2(state.PanX, state.PanY);

            if (state.Orientation != null)
            {
                bool migrateLegacyDefaultInvertY =
                    state.Orientation.InvertX &&
                    state.Orientation.InvertY &&
                    state.Orientation.InvertZ &&
                    state.Orientation.FlipCamera180;
                _orientation = new OrientationDebug
                {
                    InvertX = state.Orientation.InvertX,
                    InvertY = migrateLegacyDefaultInvertY ? false : state.Orientation.InvertY,
                    InvertZ = state.Orientation.InvertZ,
                    FlipCamera180 = state.Orientation.FlipCamera180
                };
            }

            bool migrateLegacyGizmoDefaults =
                !state.GizmoInvertX &&
                state.GizmoInvertY &&
                !state.GizmoInvertZ;
            bool migrateBrokenBrushDefaults =
                !state.BrushInvertX &&
                !state.BrushInvertY &&
                !state.BrushInvertZ;

            _gizmoInvertX = state.GizmoInvertX;
            _gizmoInvertY = migrateLegacyGizmoDefaults ? false : state.GizmoInvertY;
            _gizmoInvertZ = state.GizmoInvertZ;
            _brushInvertX = state.BrushInvertX;
            _brushInvertY = migrateBrokenBrushDefaults ? true : state.BrushInvertY;
            _brushInvertZ = state.BrushInvertZ;
            _lightEffectInvertX = state.LightEffectInvertX ?? true;
            _lightEffectInvertY = state.LightEffectInvertY ?? true;
            _lightEffectInvertZ = state.LightEffectInvertZ ?? false;
            _collarCompensationInvertX = state.CollarCompensationInvertX;
            _collarCompensationInvertY = state.CollarCompensationInvertY;
            _collarCompensationInvertZ = state.CollarCompensationInvertZ;
            _bloomCompositeInvertX = state.BloomCompositeInvertX;
            _bloomCompositeInvertY = state.BloomCompositeInvertY;
            _invertImportedCollarOrbit = false;
            _invertKnobFrontFaceWinding = state.InvertKnobFrontFaceWinding;
            _invertImportedStlFrontFaceWinding = state.InvertImportedStlFrontFaceWinding;
            InvalidateGpu();
            return true;
        }

        public IReadOnlyList<LightGizmoSnapshot> GetLightGizmoSnapshots()
        {
            _lightGizmoSnapshots.Clear();
            if (_project is null || _project.Lights.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return _lightGizmoSnapshots;
            }

            GetCameraBasis(out Vector3 right, out Vector3 up, out Vector3 forward);
            float referenceRadius = GetCurrentSceneReferenceRadius();

            float cameraDistance = MathF.Max(1f, referenceRadius * 6f);
            Vector3 cameraPos = -forward * cameraDistance;
            Vector3 viewOrigin = -cameraPos;
            float referenceDepth = Vector3.Dot(viewOrigin, forward);
            float depthRange = MathF.Max(1f, referenceRadius * 2f);
            float renderScale = MathF.Max(1e-4f, GetRenderScale());
            float centerX = (float)(Bounds.Width * 0.5) + _panPx.X;
            float centerY = (float)(Bounds.Height * 0.5) + _panPx.Y;
            float zoomDip = _zoom / renderScale;

            for (int i = 0; i < _project.Lights.Count; i++)
            {
                KnobLight light = _project.Lights[i];
                Vector3 lightPos = ApplyGizmoDisplayOrientation(
                    ApplyLightOrientation(new Vector3(light.X, light.Y, light.Z)));
                Vector3 viewLight = lightPos - cameraPos;
                Point gizmoPoint = new(
                    centerX + (Vector3.Dot(viewLight, right) * zoomDip),
                    centerY - (Vector3.Dot(viewLight, up) * zoomDip));
                Point originPoint = new(
                    centerX + (Vector3.Dot(viewOrigin, right) * zoomDip),
                    centerY - (Vector3.Dot(viewOrigin, up) * zoomDip));

                float depth = Vector3.Dot(viewLight, forward);
                float depthOffset = (depth - referenceDepth) / depthRange;
                float nearFactor = (1f - Math.Clamp(depthOffset, -1f, 1f)) * 0.5f;
                double radiusDip = 4d + (nearFactor * 5f);
                byte fillAlpha = (byte)(110 + (nearFactor * 145f));
                byte lineAlpha = (byte)(70 + (nearFactor * 120f));
                bool isSelected = i == _project.SelectedLightIndex;
                double selectedRingRadiusDip = Math.Max(radiusDip + 4d, 10d);

                bool hasDirectionTip = false;
                Point directionTipPoint = default;
                double directionTipRadiusDip = 2.5d;
                if (light.Type == LightType.Directional)
                {
                    Vector3 lightDir = ApplyGizmoDisplayOrientation(ApplyLightOrientation(GetDirectionalVector(light)));
                    if (lightDir.LengthSquared() > 1e-8f)
                    {
                        lightDir = Vector3.Normalize(lightDir);
                        directionTipPoint = new Point(
                            gizmoPoint.X + ((Vector3.Dot(lightDir, right) * 20f) / renderScale),
                            gizmoPoint.Y - ((Vector3.Dot(lightDir, up) * 20f) / renderScale));
                        hasDirectionTip = true;
                    }
                }

                _lightGizmoSnapshots.Add(new LightGizmoSnapshot(
                    positionDip: gizmoPoint,
                    originDip: originPoint,
                    directionTipDip: directionTipPoint,
                    hasDirectionTip: hasDirectionTip,
                    colorR: light.Color.Red,
                    colorG: light.Color.Green,
                    colorB: light.Color.Blue,
                    isSelected: isSelected,
                    radiusDip: radiusDip,
                    selectedRingRadiusDip: selectedRingRadiusDip,
                    directionTipRadiusDip: directionTipRadiusDip,
                    fillAlpha: fillAlpha,
                    lineAlpha: lineAlpha));
            }

            return _lightGizmoSnapshots;
        }

        public float GetCurrentSceneReferenceRadius()
        {
            if (_project is null)
            {
                return 1f;
            }

            ModelNode? modelNode = _project.SceneRoot.Children.OfType<ModelNode>().FirstOrDefault();
            if (modelNode is null)
            {
                return 1f;
            }

            if (_context is not null)
            {
                RefreshMeshResources(_project, modelNode);
            }

            float referenceRadius = MathF.Max(1f, modelNode.Radius);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _meshResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _collarResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _sliderBackplateResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _sliderThumbResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _toggleBaseResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _toggleLeverResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _toggleSleeveResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _pushButtonBaseResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _pushButtonCapResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _indicatorBaseResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _indicatorHousingResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _indicatorLensResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _indicatorReflectorResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _indicatorEmitterResources);
            referenceRadius = IncludeReferenceRadius(referenceRadius, _indicatorAuraResources);
            return referenceRadius;
        }

        private void PublishPaintHudSnapshot()
        {
            Action<PaintHudSnapshot>? handler = PaintHudUpdated;
            if (handler is null)
            {
                return;
            }

            if (_project is null)
            {
                handler(new PaintHudSnapshot(
                    paintEnabled: false,
                    isPainting: false,
                    channel: PaintChannel.Rust,
                    brushType: PaintBrushType.Spray,
                    abrasionType: ScratchAbrasionType.Needle,
                    activeSizePx: 0f,
                    activeOpacity: 0f,
                    liveScratchDepth: 0f,
                    optionDepthRampActive: false,
                    hitMode: PaintHitMode.Idle));
                return;
            }

            bool scratchChannel = _project.BrushChannel == PaintChannel.Scratch;
            float activeSizePx = scratchChannel
                ? Math.Clamp(_project.ScratchWidthPx, 1f, 320f)
                : Math.Clamp(_project.BrushSizePx, 1f, 320f);
            float liveScratchDepth = scratchChannel
                ? Math.Clamp(_isPainting ? _scratchCurrentDepth : _project.ScratchDepth, 0f, 1f)
                : 0f;

            handler(new PaintHudSnapshot(
                paintEnabled: _project.BrushPaintingEnabled,
                isPainting: _isPainting,
                channel: _project.BrushChannel,
                brushType: _project.BrushType,
                abrasionType: _project.ScratchAbrasionType,
                activeSizePx: activeSizePx,
                activeOpacity: Math.Clamp(_project.BrushOpacity, 0f, 1f),
                liveScratchDepth: liveScratchDepth,
                optionDepthRampActive: _optionDepthRampActive,
                hitMode: _lastPaintHitMode));
        }
    }
}
