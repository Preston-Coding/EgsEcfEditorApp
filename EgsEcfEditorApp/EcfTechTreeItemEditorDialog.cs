﻿using EcfFileViews;
using EgsEcfEditorApp.Properties;
using EgsEcfParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using static EcfFileViews.ItemSelectorDialog;

namespace EgsEcfEditorApp
{
    public partial class EcfTechTreeItemEditorDialog : Form
    {
        private ItemSelectorDialog ElementPicker { get; } = new ItemSelectorDialog() 
        {
            Text = TitleRecources.EcfTechTreeItemEditorDialog_ElementPickerHeader,
            Icon = IconRecources.Icon_AppBranding,
            OkButtonText = TitleRecources.Generic_Ok,
            AbortButtonText = TitleRecources.Generic_Abort,
            SearchToolTipText = TextRecources.ItemSelectorDialog_ToolTip_SearchInfo,
            DefaultItemText = TitleRecources.Generic_Replacement_Empty,
        };
        private List<EcfBlock> AvailableElements { get; } = new List<EcfBlock>();
        private EcfBlock SelectedElement { get; set; } = null;

        public EcfTechTreeItemEditorDialog()
        {
            InitializeComponent();
            InitForm();
        }

        // event
        private void InitForm()
        {
            Icon = IconRecources.Icon_AppBranding;
            Text = TitleRecources.EcfTechTreeItemEditorDialog_Header;

            OkButton.Text = TitleRecources.Generic_Ok;
            AbortButton.Text = TitleRecources.Generic_Abort;

            ElementNameLabel.Text = TitleRecources.EcfTechTreeItemEditorDialog_ElementNameSetting;
            UnlockLevelLabel.Text = TitleRecources.EcfTechTreeItemEditorDialog_UnlockLevelSetting;
            UnlockCostLabel.Text = TitleRecources.EcfTechTreeItemEditorDialog_UnlockCostSetting;

            UnlockLevelNumUpDown.Minimum = InternalSettings.Default.EgsEcfEditorApp_ParameterHandling_UnlockLevelMinValue;
            UnlockLevelNumUpDown.Maximum = InternalSettings.Default.EgsEcfEditorApp_ParameterHandling_UnlockLevelMaxValue;
            UnlockCostNumUpDown.Minimum = InternalSettings.Default.EgsEcfEditorApp_ParameterHandling_UnlockCostMinValue;
            UnlockCostNumUpDown.Maximum = InternalSettings.Default.EgsEcfEditorApp_ParameterHandling_UnlockCostMaxValue;
        }

        // events
        private void OkButton_Click(object sender, EventArgs evt)
        {
            if (SelectedElement == null)
            {
                MessageBox.Show(this, TextRecources.EcfTechTreeItemEditorDialog_NoElementSelected, 
                    TitleRecources.Generic_Warning, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
        private void AbortButton_Click(object sender, EventArgs evt)
        {
            DialogResult = DialogResult.Abort;
            Close();
        }
        private void ElementNameTextBox_Click(object sender, EventArgs evt)
        {
            if (ElementPicker.ShowDialog(this, AvailableElements.Select(block => new SelectorItem(block, block.BuildRootId())).ToArray()) == DialogResult.OK)
            {
                if (ElementPicker.SelectedItem.Item is EcfBlock element)
                {
                    SetSelectedElement(element);
                }
            }
        }

        // public
        public DialogResult ShowDialog(IWin32Window parent, EcfBlock actualElement, List<EcfBlock> availableElements, int unlockLevel, int unlockCost)
        {
            SelectedElement = actualElement;
            ElementNameTextBox.Text = SelectedElement?.BuildRootId() ?? string.Empty;
            UnlockLevelNumUpDown.Value = unlockLevel;
            UnlockCostNumUpDown.Value = unlockCost;

            AvailableElements.Clear();
            AvailableElements.AddRange(availableElements);

            return ShowDialog(parent);
        }
        public EcfBlock GetSelectedElement()
        {
            return SelectedElement;
        }
        public int GetUnlockLevel()
        {
            return Convert.ToInt32(UnlockLevelNumUpDown.Value);
        }
        public int GetUnlockCost()
        {
            return Convert.ToInt32(UnlockCostNumUpDown.Value);
        }

        // private
        private void SetSelectedElement(EcfBlock element)
        {
            SelectedElement = element;
            ElementNameTextBox.Text = element?.BuildRootId() ?? string.Empty;
        }
    }
}
