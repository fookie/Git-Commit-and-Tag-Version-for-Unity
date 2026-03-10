#if UNITY_6000_3_OR_NEWER
// ──────────────────────────────────────────────────────────────────────────────
// Unity 6.3+ 实现：使用官方 MainToolbarElement API
// ──────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;

namespace CommitAndTagVersion.Editor
{
    /// <summary>
    /// Registers a "🏷️ Release" button on the main toolbar using the official
    /// MainToolbarElement API available in Unity 6000.3 (6.3) and later.
    /// </summary>
    public static class ToolbarExtension
    {
        private const string k_ElementId = "CommitAndTagVersion/Release";

        [MainToolbarElement(k_ElementId, defaultDockPosition = MainToolbarDockPosition.Right)]
        static IEnumerable<MainToolbarElement> CreateReleaseButton()
        {
            yield return new MainToolbarButton(
                new MainToolbarContent("🏷️ Release", "Open Commit and Tag Version"),
                () => CommitAndTagWindow.ShowWindow());
        }
    }
}

#else
// ──────────────────────────────────────────────────────────────────────────────
// Unity 6.3 以下版本回退实现：通过反射注入 UIElements 按钮到工具栏
// ──────────────────────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Reflection;

namespace CommitAndTagVersion.Editor
{
    /// <summary>
    /// For Unity versions prior to 6000.3, injects a toolbar button via reflection
    /// into the internal Toolbar VisualElement hierarchy.
    /// </summary>
    [InitializeOnLoad]
    public static class ToolbarExtension
    {
        static ToolbarExtension()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            EditorApplication.update -= WaitAndInject;
            EditorApplication.update += WaitAndInject;
        }

        private static void WaitAndInject()
        {
            var toolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null) return;

            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars.Length == 0) return;

            var toolbar = toolbars[0] as ScriptableObject;
            if (toolbar == null) return;

            var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            if (rootField == null) return;

            var root = rootField.GetValue(toolbar) as VisualElement;
            if (root == null) return;

            var rightZone = root.Q("ToolbarZoneRightAlign");
            if (rightZone == null) return;

            // Stop polling once we successfully locate the toolbar zone
            EditorApplication.update -= WaitAndInject;

            // Avoid duplicate injection
            if (rightZone.Q<Button>("CommitAndTagBtn") != null) return;

            var btn = new Button(CommitAndTagWindow.ShowWindow)
            {
                name = "CommitAndTagBtn",
                text = "🏷️ Release",
                tooltip = "Open Commit and Tag Version"
            };

            btn.style.marginTop = 2;
            btn.style.marginBottom = 2;
            btn.style.marginRight = 10;
            btn.style.marginLeft = 5;
            btn.style.height = 22;
            btn.style.alignSelf = Align.Center;

            rightZone.Insert(0, btn);
        }
    }
}
#endif
