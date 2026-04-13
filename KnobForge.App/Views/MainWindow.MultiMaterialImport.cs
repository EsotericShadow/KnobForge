using KnobForge.Core.Scene;
using KnobForge.Rendering.GPU;
using System;
using System.IO;
using System.Linq;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void SyncImportedCollarMaterialNodes()
        {
            ModelNode? model = GetModelNode();
            if (model == null)
            {
                return;
            }

            CollarNode? collar = model.Children.OfType<CollarNode>().FirstOrDefault();
            if (collar == null ||
                !collar.UsesImportedMaterialOwner)
            {
                SetProjectMaterialNodes(Array.Empty<MaterialNode>(), MaterialOwnerTarget.CollarImported);
                return;
            }

            string resolvedImportedMeshPath = CollarNode.ResolveImportedMeshPath(collar.Preset, collar.ImportedMeshPath);
            if (string.IsNullOrWhiteSpace(resolvedImportedMeshPath) ||
                !File.Exists(resolvedImportedMeshPath) ||
                !string.Equals(Path.GetExtension(resolvedImportedMeshPath), ".glb", StringComparison.OrdinalIgnoreCase))
            {
                SetProjectMaterialNodes(Array.Empty<MaterialNode>(), collar.ImportedMaterialOwnerTarget);
                return;
            }

            if (ImportedMeshMaterialBuilder.TryBuildMaterialNodesFromPath(resolvedImportedMeshPath, out MaterialNode[] materials) &&
                materials.Length > 0)
            {
                SetProjectMaterialNodes(materials, collar.ImportedMaterialOwnerTarget);
            }
            else
            {
                SetProjectMaterialNodes(Array.Empty<MaterialNode>(), collar.ImportedMaterialOwnerTarget);
            }
        }
    }
}
