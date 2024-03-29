﻿using EcfFileViews;
using EcfToolBarControls;
using EgsEcfEditorApp.Properties;
using EgsEcfParser;
using GenericDialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static EcfFileViews.ItemSelectorDialog;

namespace EgsEcfEditorApp
{
    public partial class EcfTechTreeDialog : Form
    {
        public HashSet<EcfTabPage> ChangedFileTabs { get; } = new HashSet<EcfTabPage>();
        protected List<EcfTabPage> UniqueFileTabs { get; } = new List<EcfTabPage>();
        
        private ItemSelectorDialog FileTabSelector { get; } = new ItemSelectorDialog()
        {
            Icon = IconRecources.Icon_AppBranding,
            OkButtonText = TitleRecources.Generic_Ok,
            AbortButtonText = TitleRecources.Generic_Abort,
            SearchToolTipText = TextRecources.ItemSelectorDialog_ToolTip_SearchInfo,
            DefaultItemText = TitleRecources.Generic_Replacement_Empty,
        };
        private TextInputDialog TreeNameSelector { get; } = new TextInputDialog() { 
            Text= TitleRecources.EcfTechTreeDialog_TreeNameInputHeader, 
            Icon = IconRecources.Icon_AppBranding,
            OkButtonText = TitleRecources.Generic_Ok,
            AbortButtonText = TitleRecources.Generic_Abort,
        };
        private ErrorListingDialog ErrorDialog { get; } = new ErrorListingDialog()
        {
            Text = TitleRecources.Generic_Error,
            Icon = IconRecources.Icon_AppBranding,
            OkButtonText = TitleRecources.Generic_Ok,
            YesButtonText = TitleRecources.Generic_Yes,
            NoButtonText = TitleRecources.Generic_No,
            AbortButtonText = TitleRecources.Generic_Abort,
        };
        private TreeAlteratingTools TreeTools { get; } = new TreeAlteratingTools();
        protected EcfTechTreeItemEditorDialog TreeItemEditor { get; } = new EcfTechTreeItemEditorDialog();
        protected ContextMenuStrip TechTreeOperationMenu { get; } = new ContextMenuStrip();
        private ToolStripMenuItem NodeChangeItem { get; } = new ToolStripMenuItem(TitleRecources.Generic_Change, IconRecources.Icon_ChangeSimple);
        private ToolStripMenuItem NodeAddRootItem { get; } = new ToolStripMenuItem(TitleRecources.Generic_Add, IconRecources.Icon_Add);
        private ToolStripMenuItem NodeAddSubItem { get; } = new ToolStripMenuItem(TitleRecources.Generic_Add, IconRecources.Icon_Add);
        private ToolStripMenuItem NodeCopyItem { get; } = new ToolStripMenuItem(TitleRecources.Generic_Copying, IconRecources.Icon_Copy);
        private ToolStripMenuItem NodePasteRootItem { get; } = new ToolStripMenuItem(TitleRecources.Generic_Paste, IconRecources.Icon_Paste);
        private ToolStripMenuItem NodePasteSubItem { get; } = new ToolStripMenuItem(TitleRecources.Generic_Paste, IconRecources.Icon_Paste);
        private ToolStripMenuItem NodeRemoveFromThisItem { get; } = new ToolStripMenuItem(TitleRecources.EcfTechTreeDialog_RemoveFromThisMenuItem, IconRecources.Icon_Remove);
        private ToolStripMenuItem NodeRemoveFromAllItem { get; } = new ToolStripMenuItem(TitleRecources.EcfTechTreeDialog_RemoveFromAllMenuItem, IconRecources.Icon_Remove);

        protected List<EcfBlock> AvailableElements { get; } = new List<EcfBlock>();
        protected TechTreeDisplayNode LastCopiedNode { get; set; } = null;
        private List<TechTreeNode> TechTreeRootNodes { get; } = new List<TechTreeNode>();
        private EcfTechTree LastCopiedTree { get; set; } = null;

        public EcfTechTreeDialog()
        {
            InitializeComponent();
            InitForm();
        }

        // events
        private void InitForm()
        {
            Icon = IconRecources.Icon_AppBranding;
            Text = TitleRecources.EcfTechTreeDialog_Header;

            ToolContainer.Add(TreeTools);

            TreeTools.AddTreeClicked += TreeTools_AddTreeClicked;
            TreeTools.RemoveTreeClicked += TreeTools_RemoveTreeClicked;
            TreeTools.RenameTreeClicked += TreeTools_RenameTreeClicked;
            TreeTools.CopyTreeClicked += TreeTools_CopyTreeClicked;
            TreeTools.PasteTreeClicked += TreeTools_PasteTreeClicked;

            NodeChangeItem.Click += NodeChangeItem_Click;
            NodeAddRootItem.Click += NodeAddRootItem_Click;
            NodeAddSubItem.Click += NodeAddSubItem_Click;
            NodeCopyItem.Click += NodeCopyItem_Click;
            NodePasteRootItem.Click += NodePasteRootItem_Click;
            NodePasteSubItem.Click += NodePasteSubItem_Click;
            NodeRemoveFromThisItem.Click += NodeRemoveFromThisItem_Click;
            NodeRemoveFromAllItem.Click += NodeRemoveFromAllItem_Click;

            TechTreeOperationMenu.Items.Add(NodeChangeItem);
            TechTreeOperationMenu.Items.Add(NodeAddRootItem);
            TechTreeOperationMenu.Items.Add(NodeAddSubItem);
            TechTreeOperationMenu.Items.Add(NodeCopyItem);
            TechTreeOperationMenu.Items.Add(NodePasteRootItem);
            TechTreeOperationMenu.Items.Add(NodePasteSubItem);
            TechTreeOperationMenu.Items.Add(NodeRemoveFromThisItem);
            TechTreeOperationMenu.Items.Add(NodeRemoveFromAllItem);
        }
        private void TreeTools_AddTreeClicked(object sender, EventArgs evt)
        {
            string treeName = PromptTreeNameEdit(TextRecources.EcfTechTreeDialog_NewTreeName, null);
            if (treeName != null)
            {
                EcfTechTree newTree = new EcfTechTree(this, ErrorDialog, treeName);
                TechTreePageContainer.TabPages.Add(newTree);
                TechTreePageContainer.SelectedTab = newTree;
            }
        }
        private void TreeTools_RemoveTreeClicked(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree) 
            {
                if (MessageBox.Show(this, string.Format("{0}:{1}{1}{2}", TextRecources.EcfTechTreeDialog_ReallyRemoveTechTreeQuestion, Environment.NewLine, selectedTree.TreeName),
                    TitleRecources.Generic_Attention, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    selectedTree.ClearTreeNameFromAllNodes(true);
                    TechTreePageContainer.TabPages.Remove(selectedTree);
                }
            }
        }
        private void TreeTools_RenameTreeClicked(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                string treeName = PromptTreeNameEdit(selectedTree.TreeName, selectedTree);
                if (treeName != null)
                {
                    selectedTree.ResetTreeName(treeName);
                }
            }
        }
        private void TreeTools_CopyTreeClicked(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                LastCopiedTree = new EcfTechTree(selectedTree);
            }
        }
        private void TreeTools_PasteTreeClicked(object sender, EventArgs evt)
        {
            if (LastCopiedTree != null)
            {
                string treeName = PromptTreeNameEdit(string.Format("{0} - {1}", LastCopiedTree.TreeName, TitleRecources.Generic_Copy), null);
                if (treeName != null)
                {
                    EcfTechTree copiedTree = new EcfTechTree(LastCopiedTree);
                    copiedTree.SetTreeName(treeName);
                    TechTreePageContainer.TabPages.Add(copiedTree);
                    TechTreePageContainer.SelectedTab = copiedTree;
                }
            }
        }
        private void NodeChangeItem_Click(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                selectedTree.ChangeSelectedNode();
            }
        }
        private void NodeAddRootItem_Click(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                selectedTree.AddNodeToRoot();
            }
        }
        private void NodeAddSubItem_Click(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                selectedTree.AddNodeToSelectedNode();
            }
        }
        private void NodeCopyItem_Click(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                selectedTree.CopySelectedRootNode();
            }
        }
        private void NodePasteRootItem_Click(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                selectedTree.PasteNodeToRoot();
            }
        }
        private void NodePasteSubItem_Click(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                selectedTree.PasteNodeToSelectedNode();
            }
        }
        private void NodeRemoveFromThisItem_Click(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                selectedTree.RemoveSelectedNode(false);
            }
        }
        private void NodeRemoveFromAllItem_Click(object sender, EventArgs evt)
        {
            if (TechTreePageContainer.SelectedTab is EcfTechTree selectedTree)
            {
                selectedTree.RemoveSelectedNode(true);
            }
        }

        // public
        public DialogResult ShowDialog(IWin32Window parent, List<EcfTabPage> openedFileTabs)
        {
            ChangedFileTabs.Clear();
            DialogResult result = UpdateUniqueFileTabs(openedFileTabs);
            if (result != DialogResult.OK) { return result; }
            LoadTechTreeRootNodes();
            LoadTechTrees();
            return ShowDialog(parent);
        }

        // private
        private string PromptTreeNameEdit(string treeName, EcfTechTree editedTree)
        {
            bool treeNameValid = false;
            DialogResult result = DialogResult.OK;
            while (result == DialogResult.OK && !treeNameValid)
            {
                result = TreeNameSelector.ShowDialog(this, treeName);
                treeName = TreeNameSelector.InputText;
                treeNameValid = !string.IsNullOrEmpty(treeName) || 
                    !TechTreePageContainer.TabPages.Cast<EcfTechTree>().Where(tree => tree != editedTree).Any(tree => tree.TreeName.Equals(treeName));
                if (result == DialogResult.OK && !treeNameValid)
                {
                    MessageBox.Show(this, TextRecources.EcfTechTreeDialog_TechTreeNameAlreadyUsed,
                        TitleRecources.Generic_Error, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            if (result != DialogResult.OK)
            {
                treeName = null;
            }
            return treeName;
        }
        private DialogResult UpdateUniqueFileTabs(List<EcfTabPage> openedFileTabs)
        {
            UniqueFileTabs.Clear();
            AvailableElements.Clear();
            foreach (EcfTabPage openedTab in openedFileTabs)
            {
                string openedTabFileType = openedTab.File.Definition.FileType;
                if (!UniqueFileTabs.Any(uniqueTab => uniqueTab.File.Definition.FileType.Equals(openedTabFileType)))
                {
                    List<EcfTabPage> typeSpecificFileTabs = openedFileTabs.Where(tab => tab.File.Definition.FileType.Equals(openedTabFileType)).ToList();
                    if (typeSpecificFileTabs.Count > 1)
                    {
                        FileTabSelector.Text = string.Format("{0}: {1}", TextRecources.EcfTechTreeDialog_SelectFileForType, openedTabFileType);
                        DialogResult result = FileTabSelector.ShowDialog(this, typeSpecificFileTabs.Select(page => new SelectorItem(page, page.File.FileName)).ToArray());
                        if (result != DialogResult.OK) { return result; }
                        if (FileTabSelector.SelectedItem.Item is EcfTabPage selectedPage) { UniqueFileTabs.Add(selectedPage); }
                    }
                    else
                    {
                        UniqueFileTabs.Add(typeSpecificFileTabs.FirstOrDefault());
                    }
                }
            }
            AvailableElements.AddRange(UniqueFileTabs.SelectMany(tab => tab.File.ItemList.Where(item => item is EcfBlock).Cast<EcfBlock>()));
            return DialogResult.OK;
        }
        private void LoadTechTrees()
        {
            HashSet<string> techTreeNames = GetTechTreeNames(TechTreeRootNodes.AsReadOnly());
            TechTreePageContainer.SuspendLayout();
            TechTreePageContainer.TabPages.Clear();
            foreach (string treeName in techTreeNames)
            {
                if (!string.IsNullOrEmpty(treeName)) 
                {
                    EcfTechTree treePage = new EcfTechTree(this, ErrorDialog, treeName);
                    treePage.Reload();
                    TechTreePageContainer.TabPages.Add(treePage);
                }
            }
            TechTreePageContainer.ResumeLayout();
        }
        private void LoadTechTreeRootNodes()
        {
            TechTreeRootNodes.Clear();
            UniqueFileTabs.ForEach(tab =>
            {
                foreach (EcfBlock element in tab.File.ItemList.Where(item => item is EcfBlock))
                {
                    TechTreeNode newNode = new TechTreeNode(this, tab, element);
                    if (!newNode.IsTechTreeNamesEmpty())
                    {
                        AddTechTreeNode(newNode);
                    }
                }
            });
        }
        protected void AddTechTreeNode(TechTreeNode techTreeNode)
        {
            TechTreeRootNodes.ToList().ForEach(rootElement =>
            {
                if (!string.IsNullOrEmpty(rootElement.TechTreeParentName) && string.Equals(rootElement.TechTreeParentName, techTreeNode.ElementName))
                {
                    TechTreeRootNodes.Remove(rootElement);
                    techTreeNode.Add(rootElement);
                }
            });
            TechTreeNode parent = FindParentNode(techTreeNode.TechTreeParentName, TechTreeRootNodes.AsReadOnly());
            if (parent != null)
            {
                parent.Add(techTreeNode);
            }
            else
            {
                TechTreeRootNodes.Add(techTreeNode);
            }
        }
        protected void RemoveTechTreeNode(TechTreeNode techTreeNode)
        {
            if(techTreeNode.Parent != null)
            {
                techTreeNode.Parent.Remove(techTreeNode);
            }
            else
            {
                TechTreeRootNodes.Remove(techTreeNode);
            }
        }
        private TechTreeNode FindParentNode(string parentName, ReadOnlyCollection<TechTreeNode> nodes)
        {
            foreach (TechTreeNode node in nodes)
            {
                if (string.Equals(parentName, node.ElementName))
                {
                    return node;
                }
                else
                {
                    TechTreeNode subNode = FindParentNode(parentName, node.Nodes);
                    if (subNode != null)
                    {
                        return subNode;
                    }
                }
            }
            return null;
        }
        private HashSet<string> GetTechTreeNames(ReadOnlyCollection<TechTreeNode> nodes)
        {
            HashSet<string> treeNames = new HashSet<string>();
            foreach (TechTreeNode node in nodes)
            {
                foreach (string name in node.TechTreeNames) 
                {
                    treeNames.Add(name);
                }
                foreach (string name in GetTechTreeNames(node.Nodes))
                {
                    treeNames.Add(name);
                }
            }
            return treeNames;
        }
        protected List<TechTreeDisplayNode> GetTechTreeRootNodes(string treeName)
        {
            return TechTreeRootNodes.Where(node => ContainsTreeName(node, treeName)).Select(node => node.BuildNodeTree(treeName)).ToList();
        }
        private void ShowNodeSpecificMenuStripItems(bool show)
        {
            NodeChangeItem.Visible = show;
            NodeAddRootItem.Visible = !show;
            NodeAddSubItem.Visible = show;
            NodeCopyItem.Visible = show;
            NodePasteRootItem.Visible = !show;
            NodePasteSubItem.Visible = show;
            NodeRemoveFromThisItem.Visible = show;
            NodeRemoveFromAllItem.Visible = show;
        }
        protected void UpdateCoUseTechTrees(EcfTechTree changedTechTree, params TechTreeNode[] changedNodes)
        {
            TechTreePageContainer.TabPages.Cast<EcfTechTree>().Where(tree => tree != changedTechTree && tree.HasNode(changedNodes)).ToList().ForEach(tree => tree.Reload());
        }
        private List<EcfBlock> BuildElementList(ReadOnlyCollection<TechTreeNode> nodes)
        {
            List<EcfBlock> elements = new List<EcfBlock>();
            foreach (TechTreeNode node in nodes)
            {
                elements.Add(node.Element);
                elements.AddRange(BuildElementList(node.Nodes));
            }
            return elements;
        }
        protected List<EcfBlock> BuildUnusedElementsList()
        {
            return AvailableElements.Except(BuildElementList(TechTreeRootNodes.AsReadOnly())).ToList();
        }
        private bool ContainsTreeName(TechTreeNode node, string treeName)
        {
            if (node.TechTreeNames.Contains(treeName))
            {
                return true;
            }
            else
            {
                return node.Nodes.Cast<TechTreeNode>().Any(subNode => ContainsTreeName(subNode, treeName));
            }
        }

        // subclasses
        protected class EcfTechTree : TabPage
        {
            public string TreeName { get; private set; }
            private TreeView ElementTreeView { get; } = new TreeView();
            private EcfTechTreeDialog ParentDialog { get; }
            private ErrorListingDialog ErrorDialog { get; }

            public EcfTechTree(EcfTechTreeDialog parentDialog, ErrorListingDialog errorDialog, string name)
            {
                ParentDialog = parentDialog;
                ErrorDialog = errorDialog;
                SetTreeName(name);

                ElementTreeView.Dock = DockStyle.Fill;
                ElementTreeView.ShowNodeToolTips = true;

                ElementTreeView.KeyUp += ElementTreeView_KeyUp;
                ElementTreeView.KeyPress += ElementTreeView_KeyPress;
                ElementTreeView.MouseUp += ElementTreeView_MouseUp;
                ElementTreeView.NodeMouseDoubleClick += ElementTreeView_NodeMouseDoubleClick;

                Controls.Add(ElementTreeView);
            }
            public EcfTechTree(EcfTechTree template) : this(template.ParentDialog, template.ErrorDialog, template.TreeName)
            {
                ElementTreeView.Nodes.AddRange(template.ElementTreeView.Nodes.Cast<TechTreeDisplayNode>().Select(node => new TechTreeDisplayNode(node)).ToArray());
            }

            // events
            private void ElementTreeView_KeyUp(object sender, KeyEventArgs evt)
            {
                if (evt.KeyCode == Keys.Delete) { RemoveNode(ElementTreeView.SelectedNode, false); evt.Handled = true; }
                else if (evt.Control && evt.KeyCode == Keys.C) { CopySelectedRootNode(); evt.Handled = true; }
                else if (evt.Control && evt.KeyCode == Keys.V) { PasteNodeToSelectedNode(); evt.Handled = true; }
            }
            private void ElementTreeView_KeyPress(object sender, KeyPressEventArgs evt)
            {
                // hack for sqirky "ding"
                evt.Handled = true;
            }
            private void ElementTreeView_MouseUp(object sender, MouseEventArgs evt)
            {
                if (evt.Button == MouseButtons.Right)
                {
                    Point clickPosition = PointToClient(Cursor.Position);
                    TreeNode node = ElementTreeView.GetNodeAt(clickPosition);
                    ElementTreeView.SelectedNode = node;
                    if (node != null && node.Bounds.Contains(clickPosition))
                    {
                        ParentDialog.ShowNodeSpecificMenuStripItems(true);
                        ParentDialog.TechTreeOperationMenu.Show(this, clickPosition);
                    }
                    else
                    {
                        ParentDialog.ShowNodeSpecificMenuStripItems(false);
                        ParentDialog.TechTreeOperationMenu.Show(this, clickPosition);
                    }
                }
            }
            private void ElementTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs evt)
            {
                if (evt.Button == MouseButtons.Left)
                {
                    ChangeNode(evt.Node);
                }
            }

            // public
            public void ClearTreeNameFromAllNodes(bool checkUnattached)
            {
                foreach (TechTreeDisplayNode node in ElementTreeView.Nodes.Cast<TechTreeDisplayNode>())
                {
                    RemoveTechTreeName(node, checkUnattached);
                }
            }
            public void ResetTreeName(string treeName)
            {
                ClearTreeNameFromAllNodes(false);
                SetTreeName(treeName);
            }
            public void SetTreeName(string treeName)
            {
                TreeName = string.IsNullOrEmpty(treeName) ? TitleRecources.Generic_Replacement_Empty : treeName;
                Text = TreeName;
                try
                {
                    AddTreeNameToSubStruct(ElementTreeView.Nodes, treeName);
                }
                catch (Exception ex)
                {
                    ErrorDialog.ShowDialog(this, TextRecources.EcfTechTreeDialog_ElementSettingsUpdateError, ex);
                }
            }
            public void ChangeSelectedNode()
            {
                ChangeNode(ElementTreeView.SelectedNode);
            }
            public void AddNodeToRoot()
            {
                AddNode(null);
            }
            public void AddNodeToSelectedNode()
            {
                AddNode(ElementTreeView.SelectedNode);
            }
            public void CopySelectedRootNode()
            {
                if (ElementTreeView.SelectedNode is TechTreeDisplayNode selectedNode)
                {
                    ParentDialog.LastCopiedNode = selectedNode;
                }
            }
            public void PasteNodeToRoot()
            {
                PasteNode(null);
            }
            public void PasteNodeToSelectedNode()
            {
                PasteNode(ElementTreeView.SelectedNode);
            }
            public void RemoveSelectedNode(bool removeFromAllTrees)
            {
                RemoveNode(ElementTreeView.SelectedNode, removeFromAllTrees);
            }
            public bool HasNode(TechTreeNode[] nodes)
            {
                if (nodes == null) { return false; }
                return BuildElementList(ElementTreeView.Nodes).Any(element => nodes.Contains(element));
            }
            public void Reload()
            {
                ElementTreeView.SuspendLayout();
                
                List<TechTreeNode> expandedNodes = BuildExpandedNodeList(ElementTreeView.Nodes);
                ElementTreeView.Nodes.Clear();
                ElementTreeView.Nodes.AddRange(ParentDialog.GetTechTreeRootNodes(TreeName).ToArray());
                ExpandListedNodes(ElementTreeView.Nodes, expandedNodes);
                
                ElementTreeView.ResumeLayout();
            }

            // private
            private void AddTreeNameToSubStruct(TechTreeDisplayNode targetNode, string treeName)
            {
                targetNode.SourceNode.AddTechTreeName(treeName);
                AddTreeNameToSubStruct(targetNode.Nodes, treeName);
            }
            private void AddTreeNameToSubStruct(TreeNodeCollection nodes, string treeName)
            {
                foreach (TechTreeDisplayNode node in nodes.Cast<TechTreeDisplayNode>())
                {
                    node.SourceNode.AddTechTreeName(treeName);
                    AddTreeNameToSubStruct(node.Nodes, treeName);
                }
            }
            private void AddTreeNamesToParentStruct(TechTreeDisplayNode targetNode, ReadOnlyCollection<string> treeNames)
            {
                if (targetNode != null)
                {
                    targetNode.SourceNode.AddTechTreeNames(treeNames.ToList());
                    AddTreeNamesToParentStruct(targetNode.Parent as TechTreeDisplayNode, treeNames);
                }
            }
            private bool TrySetStructureData(TechTreeNode targetNode, string treeName, string techTreeParentName)
            {
                try
                {
                    targetNode.AddTechTreeName(treeName);
                    targetNode.SetTechTreeParent(techTreeParentName);
                    return true;
                }
                catch (Exception ex)
                {
                    ErrorDialog.ShowDialog(this, TextRecources.EcfTechTreeDialog_ElementSettingsUpdateError, ex);
                }
                return false;
            }
            private bool TrySetStructureData(TechTreeDisplayNode targetNode, string treeName, TechTreeDisplayNode techTreeDisplayParent)
            {
                try
                {
                    AddTreeNamesToParentStruct(techTreeDisplayParent, targetNode.SourceNode.TechTreeNames);
                    AddTreeNameToSubStruct(targetNode, treeName);
                    targetNode.SourceNode.SetTechTreeParent(techTreeDisplayParent?.SourceNode.ElementName);
                    return true;
                }
                catch (Exception ex)
                {
                    ErrorDialog.ShowDialog(this, TextRecources.EcfTechTreeDialog_ElementSettingsUpdateError, ex);
                }
                return false;
            }
            private bool TrySetUnlockData(TechTreeNode targetNode, int unlockLevel, int unlockCost)
            {
                try
                {
                    targetNode.SetUnlockLevel(unlockLevel);
                    targetNode.SetUnlockCost(unlockCost);
                    return true;
                }
                catch (Exception ex)
                {
                    ErrorDialog.ShowDialog(this, TextRecources.EcfTechTreeDialog_ElementSettingsUpdateError, ex);
                }
                return false;
            }
            private bool TrySwapElement(TechTreeNode targetNode, EcfBlock element)
            {
                if (element != targetNode.Element)
                {
                    try
                    {
                        EcfTabPage tab = ParentDialog.UniqueFileTabs.FirstOrDefault(page => page.File.ItemList.Contains(element));
                        targetNode.SwapElement(tab, element);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        ErrorDialog.ShowDialog(this, TextRecources.EcfTechTreeDialog_ElementSettingsUpdateError, ex);
                    }
                }
                return false;
            }
            private List<TechTreeNode> BuildElementList(TechTreeNode node)
            {
                List<TechTreeNode> elements = new List<TechTreeNode> { node };
                foreach (TechTreeNode subNode in node.Nodes)
                {
                    elements.AddRange(BuildElementList(subNode));
                }
                return elements;
            }
            private List<TechTreeNode> BuildElementList(TreeNodeCollection nodes)
            {
                List<TechTreeNode> elements = new List<TechTreeNode>();
                foreach (TechTreeDisplayNode node in nodes.Cast<TechTreeDisplayNode>())
                {
                    elements.Add(node.SourceNode);
                    elements.AddRange(BuildElementList(node.Nodes));
                }
                return elements;
            }
            private void ChangeNode(TreeNode targetNode)
            {
                if (targetNode is TechTreeDisplayNode node)
                {
                    TechTreeNode changedNode = node.SourceNode;
                    EcfBlock element = changedNode.Element;
                    int unlockLevel = changedNode.UnlockLevel;
                    int unlockCost = changedNode.UnlockCost;

                    List<EcfBlock> availableElements = ParentDialog.BuildUnusedElementsList();
                    if (!availableElements.Contains(element)) { availableElements.Insert(0, element); }

                    if (ParentDialog.TreeItemEditor.ShowDialog(this, element, availableElements, unlockLevel, unlockCost) == DialogResult.OK)
                    {
                        element = ParentDialog.TreeItemEditor.GetSelectedElement();
                        unlockLevel = ParentDialog.TreeItemEditor.GetUnlockLevel();
                        unlockCost = ParentDialog.TreeItemEditor.GetUnlockCost();
                        TrySwapElement(changedNode, element);
                        TrySetUnlockData(changedNode, unlockLevel, unlockCost);
                        ParentDialog.UpdateCoUseTechTrees(this, changedNode);
                        Reload();
                    }
                }
            }
            private void AddNode(TreeNode targetNode)
            {
                TechTreeDisplayNode targetDisplayNode = targetNode as TechTreeDisplayNode;
                int unlockLevel = UserSettings.Default.EcfTechTreeDialog_DefVal_UnlockLevel;
                int unlockCost = UserSettings.Default.EcfTechTreeDialog_DefVal_UnlockCost;
                List<EcfBlock> availableElements = ParentDialog.BuildUnusedElementsList();

                if (ParentDialog.TreeItemEditor.ShowDialog(this, null, availableElements, unlockLevel, unlockCost) == DialogResult.OK)
                {
                    EcfBlock element = ParentDialog.TreeItemEditor.GetSelectedElement();
                    EcfTabPage tab = ParentDialog.UniqueFileTabs.FirstOrDefault(page => page.File == element.EcfFile);
                    TechTreeNode newNode = new TechTreeNode(ParentDialog, tab, element);
                    TrySetUnlockData(newNode, ParentDialog.TreeItemEditor.GetUnlockLevel(), ParentDialog.TreeItemEditor.GetUnlockCost());
                    TrySetStructureData(newNode, TreeName, targetDisplayNode?.SourceNode.ElementName);
                    ParentDialog.AddTechTreeNode(newNode);
                    ParentDialog.UpdateCoUseTechTrees(this, targetDisplayNode?.SourceNode);
                    Reload();
                }
            }
            private void PasteNode(TreeNode targetNode)
            {
                TechTreeDisplayNode targetDisplayNode = targetNode as TechTreeDisplayNode;
                TechTreeDisplayNode pastingNode = ParentDialog.LastCopiedNode;
                if (pastingNode != null)
                {
                    TrySetStructureData(pastingNode, TreeName, targetDisplayNode);

                    List<TechTreeNode> changedElements = BuildElementList(pastingNode.SourceNode);
                    changedElements.ForEach(element =>
                    {
                        ParentDialog.RemoveTechTreeNode(element);
                        ParentDialog.AddTechTreeNode(element);
                    });

                    changedElements.Add(targetDisplayNode?.SourceNode);
                    ParentDialog.UpdateCoUseTechTrees(this, changedElements.ToArray());

                    Reload();
                }
            }
            private void RemoveNode(TreeNode targetNode, bool removeFromAllTrees)
            {
                if (targetNode is TechTreeDisplayNode node)
                {
                    if (removeFromAllTrees)
                    {
                        RemoveNodeFromAllTechTrees(node.SourceNode);
                    }
                    else
                    {
                        RemoveTechTreeName(node, true);
                    }
                    ParentDialog.UpdateCoUseTechTrees(this, node.SourceNode);
                    Reload();
                }
            }
            private void RemoveNodeFromAllTechTrees(TechTreeNode targetNode)
            {
                targetNode.Nodes.ToList().ForEach(node =>
                {
                    RemoveNodeFromAllTechTrees(node);
                });
                targetNode.RemoveTechTreeNames();
                ParentDialog.RemoveTechTreeNode(targetNode);
            }
            private void RemoveTechTreeName(TechTreeDisplayNode targetNode, bool checkUnattached)
            {
                foreach(TechTreeDisplayNode node in targetNode.Nodes.Cast<TechTreeDisplayNode>())
                {
                    RemoveTechTreeName(node, checkUnattached);
                }
                TechTreeNode techNode = targetNode.SourceNode;
                techNode.RemoveTechTreeName(TreeName);
                if (checkUnattached && techNode.IsTechTreeNamesEmpty())
                {
                    ParentDialog.RemoveTechTreeNode(techNode);
                }
            }
            private List<TechTreeNode> BuildExpandedNodeList(TreeNodeCollection nodes)
            {
                List<TechTreeNode> expanededNodes = new List<TechTreeNode>();
                foreach(TechTreeDisplayNode node in nodes.Cast<TechTreeDisplayNode>())
                {
                    if (node.IsExpanded) 
                    { 
                        expanededNodes.Add(node.SourceNode); 
                    }
                    expanededNodes.AddRange(BuildExpandedNodeList(node.Nodes));
                }
                return expanededNodes;
            }
            private void ExpandListedNodes(TreeNodeCollection nodes, List<TechTreeNode> expandedNodes)
            {
                foreach(TechTreeDisplayNode node in nodes)
                {
                    if (expandedNodes.Contains(node.SourceNode))
                    {
                        node.Expand();
                    }
                    ExpandListedNodes(node.Nodes, expandedNodes);
                }
            }
        }
        public class TechTreeNode
        {
            public string ElementName { get; private set; } = null;
            public ReadOnlyCollection<string> TechTreeNames { get; }
            private List<string> InternalTechTreeNames { get; } = new List<string>();
            public string TechTreeParentName { get; private set; } = null;
            public int UnlockLevel { get; private set; } = UserSettings.Default.EcfTechTreeDialog_DefVal_UnlockLevel;
            public int UnlockCost { get; private set; } = UserSettings.Default.EcfTechTreeDialog_DefVal_UnlockCost;
            public string ToolTip { get; private set; } = null;

            public TechTreeNode Parent { get; set; } = null;
            public ReadOnlyCollection<TechTreeNode> Nodes { get; }
            private List<TechTreeNode> InternalNodes { get; } = new List<TechTreeNode>();

            private EcfTechTreeDialog Dialog { get; }
            public EcfTabPage Tab { get; private set; }
            public EcfBlock Element { get; private set; }

            public TechTreeNode(EcfTechTreeDialog dialog, EcfTabPage tab, EcfBlock element)
            {
                Dialog = dialog;
                Tab = tab;
                Element = element;

                TechTreeNames = InternalTechTreeNames.AsReadOnly();
                Nodes = InternalNodes.AsReadOnly();

                UpdateFromElement();
            }

            // public
            public void Add(TechTreeNode node)
            {
                node.Parent = this;
                InternalNodes.Add(node);
            }
            public void Remove(TechTreeNode node)
            {
                InternalNodes.Remove(node);
                node.Parent = null;
            }
            public void RemoveTechTreeNames()
            {
                InternalTechTreeNames.Clear();
                Element.HasParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeNames, out EcfParameter treeNameParameter);
                treeNameParameter.ClearValues();
                treeNameParameter.AddValue(string.Empty);
                Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeParentName);
                Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockLevel);
                Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockCost);
                Dialog.ChangedFileTabs.Add(Tab);
            }
            public void RemoveTechTreeName(string treeName)
            {
                if (Element.HasParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeNames, out EcfParameter treeNameParameter))
                {
                    InternalTechTreeNames.Remove(treeName);
                    treeNameParameter.RemoveValue(treeName);
                    if (!treeNameParameter.HasValue())
                    {
                        treeNameParameter.AddValue(string.Empty);
                    }
                    if (IsTechTreeNamesEmpty())
                    {
                        Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeParentName);
                        Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockLevel);
                        Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockCost);
                    }
                    Dialog.ChangedFileTabs.Add(Tab);
                }
            }
            public bool IsTechTreeNamesEmpty()
            {
                return TechTreeNames.Count == 0 || (TechTreeNames.Contains(string.Empty) && TechTreeNames.Count == 1);
            }
            public void AddTechTreeName(string treeName)
            {
                InternalTechTreeNames.RemoveAll(phrase => string.IsNullOrEmpty(phrase));
                if (!InternalTechTreeNames.Contains(treeName))
                {
                    InternalTechTreeNames.Add(treeName);
                }
                SetParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeNames, InternalTechTreeNames);
            }
            public void AddTechTreeNames(List<string> treeNames)
            {
                InternalTechTreeNames.RemoveAll(phrase => string.IsNullOrEmpty(phrase));
                foreach (string treeName in treeNames)
                {
                    if (!InternalTechTreeNames.Contains(treeName))
                    {
                        InternalTechTreeNames.Add(treeName);
                    }
                }
                if (InternalTechTreeNames.Count == 0)
                {
                    InternalTechTreeNames.Add(string.Empty);
                }
                SetParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeNames, InternalTechTreeNames);
            }
            public void SetTechTreeNames(List<string> treeNames)
            {
                InternalTechTreeNames.Clear();
                foreach (string treeName in treeNames)
                {
                    if (!InternalTechTreeNames.Contains(treeName))
                    {
                        InternalTechTreeNames.Add(treeName);
                    }
                }
                if (InternalTechTreeNames.Count == 0)
                {
                    InternalTechTreeNames.Add(string.Empty);
                }
                SetParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeNames, treeNames);
            }
            public void SetTechTreeParent(string parentName)
            {
                TechTreeParentName = parentName;
                SetParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeParentName, parentName ?? string.Empty);
            }
            public void SetUnlockLevel(int level)
            {
                UnlockLevel = level;
                SetParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockLevel, level.ToString());
            }
            public void SetUnlockCost(int cost)
            {
                UnlockCost = cost;
                SetParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockCost, cost.ToString());
            }
            public void SwapElement(EcfTabPage tab, EcfBlock element)
            {
                Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeNames);
                Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeParentName);
                Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockLevel);
                Element.RemoveParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockCost);
                Dialog.ChangedFileTabs.Add(Tab);

                Tab = tab;
                Element = element;
                UpdateElementName();

                foreach(TechTreeNode node in Nodes)
                {
                    node.SetTechTreeParent(ElementName);
                }

                SetTechTreeNames(TechTreeNames.ToList());
                SetTechTreeParent(TechTreeParentName);
                SetUnlockLevel(UnlockLevel);
                SetUnlockCost(UnlockCost);
                Dialog.ChangedFileTabs.Add(tab);
            }
            public void UpdateFromElement()
            {
                UpdateElementName();
                UpdateTechTreeSettings();
            }
            public TechTreeNode FindRoot()
            {
                return FindRoot(this);
            }
            public TechTreeDisplayNode BuildNodeTree(string treeName)
            {
                return BuildNodeTree(FindRoot(), treeName);
            }

            // private
            private TechTreeNode FindRoot(TechTreeNode techNode)
            {
                return techNode.Parent == null ? techNode : FindRoot(techNode.Parent);
            }
            private TechTreeDisplayNode BuildNodeTree(TechTreeNode techNode, string treeName)
            {
                TechTreeDisplayNode dispNode = techNode.BuildTreeNode();
                foreach (TechTreeNode subTechNode in techNode.Nodes)
                {
                    TechTreeDisplayNode subDispNode = BuildNodeTree(subTechNode, treeName);
                    if (subDispNode != null)
                    {
                        dispNode.Nodes.Add(subDispNode);
                    }
                }
                if (techNode.TechTreeNames.Contains(treeName) || dispNode.Nodes.Count != 0)
                {
                    return dispNode;
                }
                else
                {
                    return null;
                }
            }
            private TechTreeDisplayNode BuildTreeNode()
            {
                return new TechTreeDisplayNode(this, string.Format("{0} // {1}: {2} // {3}: {4}",
                    ElementName, TitleRecources.Generic_Level, UnlockLevel, TitleRecources.Generic_Cost, UnlockCost), ToolTip);
            }
            private void SetParameter(string paramKey, string value)
            {
                EcfParameter parameter = Element.FindOrAddParameter(paramKey);
                parameter.ClearValues();
                parameter.AddValue(value);
                Dialog.ChangedFileTabs.Add(Tab);
            }
            private void SetParameter(string paramKey, List<string> values)
            {
                EcfParameter parameter = Element.FindOrAddParameter(paramKey);
                parameter.ClearValues();
                parameter.AddValue(values);
                Dialog.ChangedFileTabs.Add(Tab);
            }
            private void UpdateElementName()
            {
                string elementName = Element.GetName();
                ElementName = elementName ?? TitleRecources.Generic_Replacement_Empty;
                ToolTip = Element?.BuildRootId() ?? TitleRecources.Generic_Replacement_Empty;
            }
            private void UpdateTechTreeSettings()
            {
                Element.HasParameter(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeNames, true, false, out EcfParameter techTreeNames);
                string techTreeParentName = Element.GetParameterFirstValue(UserSettings.Default.EcfTechTreeDialog_ParamKey_TechTreeParentName, true, false);
                string unlockLevel = Element.GetParameterFirstValue(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockLevel, true, false);
                string unlockCost = Element.GetParameterFirstValue(UserSettings.Default.EcfTechTreeDialog_ParamKey_UnlockCost, true, false);

                if (!int.TryParse(unlockLevel, out int unlockLevelValue))
                {
                    unlockLevelValue = UserSettings.Default.EcfTechTreeDialog_DefVal_UnlockLevel;
                }
                if (!int.TryParse(unlockCost, out int unlockCostValue))
                {
                    unlockCostValue = UserSettings.Default.EcfTechTreeDialog_DefVal_UnlockCost;
                }

                InternalTechTreeNames.Clear();
                EcfValueGroup group = techTreeNames?.ValueGroups.FirstOrDefault();
                if (group != null) { InternalTechTreeNames.AddRange(group.Values); }
                TechTreeParentName = techTreeParentName;
                UnlockLevel = unlockLevelValue;
                UnlockCost = unlockCostValue;
            }
        }
        public class TechTreeDisplayNode : TreeNode
        {
            public TechTreeNode SourceNode { get; }

            public TechTreeDisplayNode(TechTreeNode sourceNode, string text, string toolTip)
            {
                SourceNode = sourceNode;
                Text = text;
                ToolTipText = toolTip;
            }
            public TechTreeDisplayNode(TechTreeDisplayNode template) : this(template.SourceNode, template.Text, template.ToolTipText)
            {
                Nodes.AddRange(template.Nodes.Cast<TechTreeDisplayNode>().Select(node => new TechTreeDisplayNode(node)).ToArray());
            }
        }
        private class TreeAlteratingTools : EcfToolBox
        {
            public event EventHandler AddTreeClicked;
            public event EventHandler RemoveTreeClicked;
            public event EventHandler RenameTreeClicked;
            public event EventHandler CopyTreeClicked;
            public event EventHandler PasteTreeClicked;

            private EcfToolBarButton AddTreeButton { get; } = new EcfToolBarButton(TextRecources.EcfTechTreeDialog_ToolTip_AddTree, IconRecources.Icon_Add, null);
            private EcfToolBarButton RemoveTreeButton { get; } = new EcfToolBarButton(TextRecources.EcfTechTreeDialog_ToolTip_RemoveTree, IconRecources.Icon_Remove, null);
            private EcfToolBarButton RenameTreeButton { get; } = new EcfToolBarButton(TextRecources.EcfTechTreeDialog_ToolTip_RenameTree, IconRecources.Icon_ChangeSimple, null);
            private EcfToolBarButton CopyTreeButton { get; } = new EcfToolBarButton(TextRecources.EcfTechTreeDialog_ToolTip_CopyTree, IconRecources.Icon_Copy, null);
            private EcfToolBarButton PasteTreeButton { get; } = new EcfToolBarButton(TextRecources.EcfTechTreeDialog_ToolTip_PasteTree, IconRecources.Icon_Paste, null);

            public TreeAlteratingTools() : base()
            {
                Add(AddTreeButton);
                Add(RemoveTreeButton);
                Add(RenameTreeButton);
                Add(CopyTreeButton);
                Add(PasteTreeButton);

                AddTreeButton.Click += (sender, evt) => AddTreeClicked?.Invoke(sender, evt);
                RenameTreeButton.Click += (sender, evt) => RenameTreeClicked?.Invoke(sender, evt);
                RemoveTreeButton.Click += (sender, evt) => RemoveTreeClicked?.Invoke(sender, evt);
                CopyTreeButton.Click += (sender, evt) => CopyTreeClicked?.Invoke(sender, evt);
                PasteTreeButton.Click += (sender, evt) => PasteTreeClicked?.Invoke(sender, evt);
            }
        }
    }
}
