using Avalonia;
using Avalonia.Controls;
using KnobForge.App.Controls;
using KnobForge.Core;
using System;

namespace KnobForge.App.Views
{
    public partial class MainWindow
    {
        private void OnPushButtonAssemblySettingsChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_updatingUi ||
                _pushButtonBaseMeshCombo == null ||
                _pushButtonCapMeshCombo == null ||
                _pushButtonCapProfileCombo == null ||
                _pushButtonBezelProfileCombo == null ||
                _pushButtonSkirtStyleCombo == null ||
                _pushButtonPressAmountInput == null ||
                _pushButtonBezelChamferSizeInput == null ||
                _pushButtonCapOverhangInput == null ||
                _pushButtonCapSegmentsInput == null ||
                _pushButtonBezelSegmentsInput == null ||
                _pushButtonSkirtHeightInput == null ||
                _pushButtonSkirtRadiusInput == null)
            {
                return;
            }

            if (ReferenceEquals(sender, _pushButtonBaseMeshCombo) ||
                ReferenceEquals(sender, _pushButtonCapMeshCombo) ||
                ReferenceEquals(sender, _pushButtonCapProfileCombo) ||
                ReferenceEquals(sender, _pushButtonBezelProfileCombo) ||
                ReferenceEquals(sender, _pushButtonSkirtStyleCombo))
            {
                if (e.Property != ComboBox.SelectedItemProperty)
                {
                    return;
                }
            }
            else if (e.Property != ValueInput.ValueProperty)
            {
                return;
            }

            if (GetModelNode() == null)
            {
                return;
            }

            ApplyPushButtonAssemblyUiToProject(requestHeavyRefresh: true);
        }

        private void ApplyPushButtonAssemblyUiToProject(bool requestHeavyRefresh)
        {
            if (_pushButtonBaseMeshCombo == null ||
                _pushButtonCapMeshCombo == null ||
                _pushButtonCapProfileCombo == null ||
                _pushButtonBezelProfileCombo == null ||
                _pushButtonSkirtStyleCombo == null ||
                _pushButtonPressAmountInput == null ||
                _pushButtonBezelChamferSizeInput == null ||
                _pushButtonCapOverhangInput == null ||
                _pushButtonCapSegmentsInput == null ||
                _pushButtonBezelSegmentsInput == null ||
                _pushButtonSkirtHeightInput == null ||
                _pushButtonSkirtRadiusInput == null)
            {
                return;
            }

            _project.PushButtonBaseImportedMeshPath = ResolveSelectedPushButtonMeshPath(_pushButtonBaseMeshCombo.SelectedItem);
            _project.PushButtonCapImportedMeshPath = ResolveSelectedPushButtonMeshPath(_pushButtonCapMeshCombo.SelectedItem);
            _project.PushButtonCapProfile = _pushButtonCapProfileCombo.SelectedItem is PushButtonCapProfile capProfile
                ? capProfile
                : PushButtonCapProfile.Flat;
            _project.PushButtonBezelProfile = _pushButtonBezelProfileCombo.SelectedItem is PushButtonBezelProfile bezelProfile
                ? bezelProfile
                : PushButtonBezelProfile.Straight;
            _project.PushButtonSkirtStyle = _pushButtonSkirtStyleCombo.SelectedItem is PushButtonSkirtStyle skirtStyle
                ? skirtStyle
                : PushButtonSkirtStyle.None;
            _project.PushButtonPressAmountNormalized = (float)_pushButtonPressAmountInput.Value;
            _project.PushButtonBezelChamferSize = (float)_pushButtonBezelChamferSizeInput.Value;
            _project.PushButtonCapOverhang = (float)_pushButtonCapOverhangInput.Value;
            _project.PushButtonCapSegments = (int)Math.Round(_pushButtonCapSegmentsInput.Value);
            _project.PushButtonBezelSegments = (int)Math.Round(_pushButtonBezelSegmentsInput.Value);
            _project.PushButtonSkirtHeight = (float)_pushButtonSkirtHeightInput.Value;
            _project.PushButtonSkirtRadius = (float)_pushButtonSkirtRadiusInput.Value;
            UpdateReadouts();
            if (requestHeavyRefresh)
            {
                RequestHeavyGeometryRefresh();
            }
            else
            {
                NotifyProjectStateChanged();
            }
        }
    }
}
