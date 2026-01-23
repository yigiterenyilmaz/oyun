using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Event))]
public class EventEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Event Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isRepeatable"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("weight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minGamePhase"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxGamePhase"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("requiredSkills"),
            true
        );
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("statConditions"),
            true
        );

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("choices"),
            true
        );

        serializedObject.ApplyModifiedProperties();
    }
}
