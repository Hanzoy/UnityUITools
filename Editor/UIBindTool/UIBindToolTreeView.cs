using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

public class UIBindToolTreeView : TreeView
{
    private List<KeyValuePair<int, (GameObject obj, GameObject objPrefab)>> m_data = new List<KeyValuePair<int, (GameObject, GameObject)>>();
    private GameObject m_rootPanel; // UI闈㈡澘鐨勬牴瀵硅薄
    private UIBindToolWindow m_parentWindow;
    private int m_PendingSelectionInstanceId;

    public UIBindToolTreeView(TreeViewState state, MultiColumnHeader header, UIBindToolWindow parentWindow, List<KeyValuePair<int, (GameObject, GameObject)>> data, GameObject rootPanel) : base(state, header)
    {
        m_data = data;
        m_rootPanel = rootPanel;
        m_parentWindow = parentWindow;
        rowHeight = 25;
        showAlternatingRowBackgrounds = true;
        showBorder = true;
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        TreeViewItem root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
        List<TreeViewItem> allItems = new List<TreeViewItem>();
        for (int i = 0; i < m_data.Count; i++)
        {
            UIBindToolTreeViewItem item = new UIBindToolTreeViewItem(i + 1, m_data[i].Key, m_data[i].Value.obj.name, m_data[i].Value.obj, m_data[i].Value.objPrefab);
            allItems.Add(item);
        }
        SetupParentsAndChildrenFromDepths(root, allItems);
        return root;
    }

    protected override void SingleClickedItem(int id)
    {
        base.SingleClickedItem(id);
        SyncUnitySelection(id);
    }

    protected override void SelectionChanged(IList<int> selectedIds)
    {
        base.SelectionChanged(selectedIds);
        if (selectedIds == null || selectedIds.Count == 0)
            return;

        SyncUnitySelection(selectedIds[0]);
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        UIBindToolTreeViewItem item = args.item as UIBindToolTreeViewItem;
        if (item == null)
            return;

        for (int i = 0; i < args.GetNumVisibleColumns(); i++)
        {
            Rect cellRect = args.GetCellRect(i);
            int columnIndex = args.GetColumn(i);
            if (columnIndex == 0)
            {
                float indent = GetContentIndent(item);
                cellRect.x += indent;
                cellRect.width -= indent;
                GameObject tempPrefab = PrefabUtility.GetNearestPrefabInstanceRoot(item.boundObject);
                if (tempPrefab != null && tempPrefab.name == item.boundObject.name)
                {
                    GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal = { textColor = new Color(0.5f, 0.8f, 1f, 1f) }
                    };
                    EditorGUI.LabelField(cellRect, args.label, labelStyle);
                }
                else
                {
                    EditorGUI.LabelField(cellRect, args.label);
                }
            }
            else if (columnIndex == 1)
            {
                DrawBindButton(cellRect, item, m_rootPanel);
            }
            else if (columnIndex == 2)
            {
                DrawBindComponents(cellRect, item);
            }
        }
    }

    /// <summary>
    /// 缁樺埗缁戝畾鎸夐挳锛堟樉绀哄姞鍙凤級
    /// </summary>
    private void DrawBindButton(Rect cellRect, UIBindToolTreeViewItem item, GameObject rootPanel)
    {
        float buttonSize = 20f;
        Rect buttonRect = new Rect(
            cellRect.x + (cellRect.width - buttonSize) * 0.5f,
            cellRect.y + (cellRect.height - buttonSize) * 0.5f,
            buttonSize,
            buttonSize
        );

        if (GUI.Button(buttonRect, "+"))
        {
            if (item.boundObject != null)
            {
                UIBindConfigWindow.ShowWindow(item.boundObject, item.boundObjectInPrefab, rootPanel, m_parentWindow);
            }
        }
    }

    /// <summary>
    /// 缁樺埗缁戝畾缁勪欢鏄剧ず
    /// </summary>
    private void DrawBindComponents(Rect cellRect, UIBindToolTreeViewItem item)
    {
        if (item.boundObject == null || m_rootPanel == null)
            return;

        UIPanelBindings bindings = m_parentWindow.CurrentBindings;
        if (bindings == null)
            return;

        List<UIBindItem> objectBindings = bindings.GetBindingsForObject(item.boundObject);
        if (objectBindings == null || objectBindings.Count == 0)
            return;

        List<UIBindItem> validBindings = objectBindings.FindAll(b => b.IsValidTarget(item.boundObject));
        if (validBindings.Count == 0)
            return;

        float currentX = cellRect.x;
        float componentHeight = 22f;
        float spacing = 6f;
        float padding = 4f;
        float startY = cellRect.y + (cellRect.height - componentHeight) * 0.5f;

        for (int i = 0; i < validBindings.Count; i++)
        {
            UIBindItem binding = validBindings[i];

            GUIStyle componentNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                clipping = TextClipping.Clip
            };

            GUIStyle variableNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 9,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(1f, 1f, 1f, 0.9f) },
                hover = { textColor = new Color(1f, 1f, 1f, 0.9f) },
                clipping = TextClipping.Clip
            };

            Vector2 componentNameSize = componentNameStyle.CalcSize(new GUIContent(binding.shortTypeName));
            Vector2 variableNameSize = variableNameStyle.CalcSize(new GUIContent(binding.variableName));

            float spacingBetween = 2f;
            float totalTextWidth = componentNameSize.x + spacingBetween + variableNameSize.x + padding * 2;
            float componentWidth = Mathf.Max(totalTextWidth, 60f);

            Rect componentRect = new Rect(currentX + padding, startY, componentWidth - padding, componentHeight);
            Color backgroundColor = GetColorForAccessModifier(binding.accessModifier);
            EditorGUI.DrawRect(componentRect, backgroundColor);

            float textStartX = componentRect.x + padding / 2;
            float textY = componentRect.y + (componentHeight - componentNameSize.y) * 0.5f;

            Rect componentNameRect = new Rect(textStartX, textY, componentNameSize.x, componentNameSize.y);
            GUI.Label(componentNameRect, binding.shortTypeName, componentNameStyle);

            textStartX += componentNameSize.x + spacingBetween;
            float variableNameY = componentRect.y + (componentHeight - variableNameSize.y) * 0.5f;
            Rect variableNameRect = new Rect(textStartX, variableNameY, variableNameSize.x, variableNameSize.y);
            GUI.Label(variableNameRect, binding.variableName, variableNameStyle);

            EditorGUIUtility.AddCursorRect(componentRect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && componentRect.Contains(Event.current.mousePosition))
            {
                EditDeleteBindingWindow.ShowWindow(item.boundObject, binding, m_rootPanel, m_parentWindow);
                Event.current.Use();
            }

            currentX += componentWidth + spacing;
        }
    }

    /// <summary>
    /// 鏍规嵁璁块棶淇グ绗﹁幏鍙栭鑹?
    /// </summary>
    private Color GetColorForAccessModifier(AccessModifier accessModifier)
    {
        switch (accessModifier)
        {
            case AccessModifier.Private:
                return new Color(0.2f, 0.6f, 0.2f, 0.9f);
            case AccessModifier.Protected:
                return new Color(0.2f, 0.4f, 0.8f, 0.9f);
            case AccessModifier.Public:
                return new Color(0.8f, 0.7f, 0.2f, 0.9f);
            default:
                return new Color(0.5f, 0.5f, 0.5f, 0.9f);
        }
    }

    private void SyncUnitySelection(int id)
    {
        TreeViewItem treeViewItem = FindItem(id, rootItem);
        UIBindToolTreeViewItem bindItem = treeViewItem as UIBindToolTreeViewItem;
        if (bindItem == null || bindItem.boundObject == null)
            return;

        int instanceId = bindItem.boundObject.GetInstanceID();
        if (m_PendingSelectionInstanceId == instanceId || Selection.activeGameObject == bindItem.boundObject)
            return;

        m_PendingSelectionInstanceId = instanceId;
        EditorApplication.delayCall += () =>
        {
            if (m_PendingSelectionInstanceId != instanceId)
                return;

            m_PendingSelectionInstanceId = 0;
            if (bindItem.boundObject == null)
                return;

            Selection.activeGameObject = bindItem.boundObject;
            EditorGUIUtility.PingObject(bindItem.boundObject);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            InternalEditorUtility.RepaintAllViews();
        };
    }
}
