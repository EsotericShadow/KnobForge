using KnobForge.Core.MaterialGraph;
using KnobForge.Core.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private static MaterialNodeSnapshot CaptureMaterialNodeSnapshot(MaterialNode material)
        {
            return new MaterialNodeSnapshot
            {
                Name = material.Name,
                BaseColorX = material.BaseColor.X,
                BaseColorY = material.BaseColor.Y,
                BaseColorZ = material.BaseColor.Z,
                Metallic = material.Metallic,
                Roughness = material.Roughness,
                Pearlescence = material.Pearlescence,
                RustAmount = material.RustAmount,
                WearAmount = material.WearAmount,
                GunkAmount = material.GunkAmount,
                RadialBrushStrength = material.RadialBrushStrength,
                RadialBrushDensity = material.RadialBrushDensity,
                SurfaceCharacter = material.SurfaceCharacter,
                SpecularPower = material.SpecularPower,
                DiffuseStrength = material.DiffuseStrength,
                SpecularStrength = material.SpecularStrength,
                PartMaterialsEnabled = material.PartMaterialsEnabled,
                TopBaseColorX = material.TopBaseColor.X,
                TopBaseColorY = material.TopBaseColor.Y,
                TopBaseColorZ = material.TopBaseColor.Z,
                TopMetallic = material.TopMetallic,
                TopRoughness = material.TopRoughness,
                BevelBaseColorX = material.BevelBaseColor.X,
                BevelBaseColorY = material.BevelBaseColor.Y,
                BevelBaseColorZ = material.BevelBaseColor.Z,
                BevelMetallic = material.BevelMetallic,
                BevelRoughness = material.BevelRoughness,
                SideBaseColorX = material.SideBaseColor.X,
                SideBaseColorY = material.SideBaseColor.Y,
                SideBaseColorZ = material.SideBaseColor.Z,
                SideMetallic = material.SideMetallic,
                SideRoughness = material.SideRoughness,
                AlbedoMapPath = material.AlbedoMapPath,
                NormalMapPath = material.NormalMapPath,
                RoughnessMapPath = material.RoughnessMapPath,
                MetallicMapPath = material.MetallicMapPath,
                NormalMapStrength = material.NormalMapStrength,
                Graph = MaterialGraphSerialization.Clone(material.Graph)
            };
        }

        private static MaterialNodeSnapshot CloneMaterialNodeSnapshot(MaterialNodeSnapshot snapshot)
        {
            return new MaterialNodeSnapshot
            {
                Name = snapshot.Name,
                BaseColorX = snapshot.BaseColorX,
                BaseColorY = snapshot.BaseColorY,
                BaseColorZ = snapshot.BaseColorZ,
                Metallic = snapshot.Metallic,
                Roughness = snapshot.Roughness,
                Pearlescence = snapshot.Pearlescence,
                RustAmount = snapshot.RustAmount,
                WearAmount = snapshot.WearAmount,
                GunkAmount = snapshot.GunkAmount,
                RadialBrushStrength = snapshot.RadialBrushStrength,
                RadialBrushDensity = snapshot.RadialBrushDensity,
                SurfaceCharacter = snapshot.SurfaceCharacter,
                SpecularPower = snapshot.SpecularPower,
                DiffuseStrength = snapshot.DiffuseStrength,
                SpecularStrength = snapshot.SpecularStrength,
                PartMaterialsEnabled = snapshot.PartMaterialsEnabled,
                TopBaseColorX = snapshot.TopBaseColorX,
                TopBaseColorY = snapshot.TopBaseColorY,
                TopBaseColorZ = snapshot.TopBaseColorZ,
                TopMetallic = snapshot.TopMetallic,
                TopRoughness = snapshot.TopRoughness,
                BevelBaseColorX = snapshot.BevelBaseColorX,
                BevelBaseColorY = snapshot.BevelBaseColorY,
                BevelBaseColorZ = snapshot.BevelBaseColorZ,
                BevelMetallic = snapshot.BevelMetallic,
                BevelRoughness = snapshot.BevelRoughness,
                SideBaseColorX = snapshot.SideBaseColorX,
                SideBaseColorY = snapshot.SideBaseColorY,
                SideBaseColorZ = snapshot.SideBaseColorZ,
                SideMetallic = snapshot.SideMetallic,
                SideRoughness = snapshot.SideRoughness,
                AlbedoMapPath = snapshot.AlbedoMapPath,
                NormalMapPath = snapshot.NormalMapPath,
                RoughnessMapPath = snapshot.RoughnessMapPath,
                MetallicMapPath = snapshot.MetallicMapPath,
                NormalMapStrength = snapshot.NormalMapStrength,
                Graph = MaterialGraphSerialization.Clone(snapshot.Graph)
            };
        }

        private static MaterialNode CreateMaterialNodeFromSnapshot(MaterialNodeSnapshot snapshot)
        {
            var material = new MaterialNode(string.IsNullOrWhiteSpace(snapshot.Name) ? "Material" : snapshot.Name);
            ApplyMaterialNodeSnapshot(material, snapshot);
            return material;
        }

        private static void ApplyMaterialNodeSnapshot(MaterialNode material, MaterialNodeSnapshot snapshot)
        {
            material.Name = string.IsNullOrWhiteSpace(snapshot.Name) ? material.Name : snapshot.Name;
            material.BaseColor = new Vector3(snapshot.BaseColorX, snapshot.BaseColorY, snapshot.BaseColorZ);
            material.Metallic = snapshot.Metallic;
            material.Roughness = snapshot.Roughness;
            material.Pearlescence = snapshot.Pearlescence;
            material.RustAmount = snapshot.RustAmount;
            material.WearAmount = snapshot.WearAmount;
            material.GunkAmount = snapshot.GunkAmount;
            material.RadialBrushStrength = snapshot.RadialBrushStrength;
            material.RadialBrushDensity = snapshot.RadialBrushDensity;
            material.SurfaceCharacter = snapshot.SurfaceCharacter;
            material.SpecularPower = snapshot.SpecularPower;
            material.DiffuseStrength = snapshot.DiffuseStrength;
            material.SpecularStrength = snapshot.SpecularStrength;
            material.PartMaterialsEnabled = snapshot.PartMaterialsEnabled;
            material.TopBaseColor = new Vector3(snapshot.TopBaseColorX, snapshot.TopBaseColorY, snapshot.TopBaseColorZ);
            material.TopMetallic = snapshot.TopMetallic;
            material.TopRoughness = snapshot.TopRoughness;
            material.BevelBaseColor = new Vector3(snapshot.BevelBaseColorX, snapshot.BevelBaseColorY, snapshot.BevelBaseColorZ);
            material.BevelMetallic = snapshot.BevelMetallic;
            material.BevelRoughness = snapshot.BevelRoughness;
            material.SideBaseColor = new Vector3(snapshot.SideBaseColorX, snapshot.SideBaseColorY, snapshot.SideBaseColorZ);
            material.SideMetallic = snapshot.SideMetallic;
            material.SideRoughness = snapshot.SideRoughness;
            material.AlbedoMapPath = snapshot.AlbedoMapPath;
            material.NormalMapPath = snapshot.NormalMapPath;
            material.RoughnessMapPath = snapshot.RoughnessMapPath;
            material.MetallicMapPath = snapshot.MetallicMapPath;
            material.NormalMapStrength = snapshot.NormalMapStrength;
            material.Graph = MaterialGraphSerialization.Clone(snapshot.Graph);
        }

        private static MaterialNode CloneMaterialNode(MaterialNode material)
        {
            return CreateMaterialNodeFromSnapshot(CaptureMaterialNodeSnapshot(material));
        }

        private void SetProjectMaterialNodes(IReadOnlyList<MaterialNode> materials)
        {
            _project.SetMaterialNodes(materials);
            if (_project.SelectedNode is MaterialNode)
            {
                MaterialNode[] available = _project.GetMaterialNodes().ToArray();
                if (available.Length > 0)
                {
                    int clampedIndex = Math.Clamp(_selectedMaterialIndex, 0, available.Length - 1);
                    _selectedMaterialIndex = clampedIndex;
                    _project.SetSelectedNode(available[clampedIndex]);
                }
                else
                {
                    _project.SetSelectedNode(_project.EnsureMaterialNode());
                }
            }
        }

        private void CollapseProjectMaterialsToSingleNode()
        {
            ModelNode? model = GetModelNode();
            MaterialNode? primaryMaterial = model?.GetMaterialByIndex(0);
            SetProjectMaterialNodes(new[]
            {
                primaryMaterial is not null ? CloneMaterialNode(primaryMaterial) : new MaterialNode("DefaultMaterial")
            });
        }
    }
}
