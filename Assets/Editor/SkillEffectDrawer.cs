using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Skill ve Event editorlerinde effect listesi çizmek için ortak yardımcı sınıf.
/// </summary>
public static class SkillEffectDrawer
{
    private static Type[] effectTypes;
    private static string[] effectTypeNames;

    private static void CacheTypes()
    {
        if (effectTypes != null) return;

        effectTypes = TypeCache.GetTypesDerivedFrom<SkillEffect>()
            .Where(t => !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToArray();

        effectTypeNames = effectTypes.Select(t => FormatTypeName(t.Name)).ToArray();
    }

    private static string FormatTypeName(string name)
    {
        // "PassiveIncomeEffect" → "Passive Income"
        name = name.Replace("Effect", "");
        var result = "";
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                result += " ";
            result += name[i];
        }
        return result;
    }

    /// <summary>
    /// [SerializeReference] List<SkillEffect> alanını dropdown ile çizer.
    /// </summary>
    public static void DrawEffectList(SerializedProperty listProperty)
    {
        CacheTypes();

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            var element = listProperty.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Başlık ve sil butonu
            EditorGUILayout.BeginHorizontal();
            string typeName = element.managedReferenceValue != null
                ? FormatTypeName(element.managedReferenceValue.GetType().Name)
                : "Null";
            EditorGUILayout.LabelField(typeName, EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                listProperty.DeleteArrayElementAtIndex(i);
                listProperty.serializedObject.ApplyModifiedProperties();
                break;
            }
            EditorGUILayout.EndHorizontal();

            // Effect alanlarını çiz
            if (element.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                var iter = element.Copy();
                var end = element.GetEndProperty();
                if (iter.NextVisible(true))
                {
                    do
                    {
                        EditorGUILayout.PropertyField(iter, true);
                    }
                    while (iter.NextVisible(false) && !SerializedProperty.EqualContents(iter, end));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        // Effect ekle dropdown
        if (EditorGUILayout.DropdownButton(new GUIContent("Effect Ekle"), FocusType.Keyboard))
        {
            var menu = new GenericMenu();
            for (int i = 0; i < effectTypes.Length; i++)
            {
                var type = effectTypes[i];
                var displayName = effectTypeNames[i];
                menu.AddItem(new GUIContent(displayName), false, () =>
                {
                    listProperty.serializedObject.Update();
                    listProperty.arraySize++;
                    var newElement = listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1);
                    newElement.managedReferenceValue = Activator.CreateInstance(type);
                    listProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }
    }
}
