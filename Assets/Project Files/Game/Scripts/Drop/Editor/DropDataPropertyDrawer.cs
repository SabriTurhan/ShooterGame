using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Watermelon.SquadShooter;

namespace Watermelon.LevelSystem
{
    [CustomPropertyDrawer(typeof(DropData))]
    public class DropDataPropertyDrawer : UnityEditor.PropertyDrawer
    {
        private const int ColumnCount = 3;
        private const int GapSize = 4;
        private const int GapCount = ColumnCount - 1;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var x = position.x;
            var y = position.y;
            var width = (position.width - GapCount * GapSize) / ColumnCount;
            var height = EditorGUIUtility.singleLineHeight;
            var offset = width + GapSize;

            var dropTypeProperty = property.FindPropertyRelative("DropType");
            DropableItemType dropType = (DropableItemType)dropTypeProperty.intValue;

            EditorGUI.PropertyField(new Rect(x, y, width, height), dropTypeProperty, GUIContent.none);

            if (dropType == DropableItemType.Currency)
            {
                EditorGUI.PropertyField(new Rect(x + offset, y, width, height), property.FindPropertyRelative("Amount"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(x + offset + offset, y, width, height), property.FindPropertyRelative("CurrencyType"), GUIContent.none);
            }
            else if (dropType == DropableItemType.WeaponCard)
            {
                EditorGUI.PropertyField(new Rect(x + offset, y, width, height), property.FindPropertyRelative("Amount"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(x + offset + offset, y, width, height), property.FindPropertyRelative("Weapon"), GUIContent.none);
            }
            else if (dropType == DropableItemType.Heal)
            {
                EditorGUI.PropertyField(new Rect(x + offset, y, width + width + GapSize, height), property.FindPropertyRelative("Amount"), GUIContent.none);
            }
            else if (dropType == DropableItemType.Weapon)
            {
                using (new LabelWidthScope(68))
                {
                    EditorGUI.PropertyField(new Rect(x + offset, y, width + width + GapSize, height), property.FindPropertyRelative("Weapon"), new GUIContent("Weapon"));
                    EditorGUI.PropertyField(new Rect(x + offset, y + EditorGUIUtility.singleLineHeight + 2, width + width + GapSize, height), property.FindPropertyRelative("Level"), new GUIContent("Level"));
                }
            }
            else if (dropType == DropableItemType.Character)
            {
                using (new LabelWidthScope(68))
                {
                    EditorGUI.PropertyField(new Rect(x + offset, y, width + width + GapSize, height), property.FindPropertyRelative("Character"), GUIContent.none);
                    EditorGUI.PropertyField(new Rect(x + offset, y + EditorGUIUtility.singleLineHeight + 2 + EditorGUIUtility.singleLineHeight + 2 + EditorGUIUtility.singleLineHeight + 2, width + width + GapSize, height), property.FindPropertyRelative("Level"), new GUIContent("Level"));
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var dropTypeProperty = property.FindPropertyRelative("DropType");
            DropableItemType dropType = (DropableItemType)dropTypeProperty.intValue;

            if(dropType == DropableItemType.Weapon) 
                return EditorGUIUtility.singleLineHeight * 2 + 2;

            if(dropType == DropableItemType.Character)
                return EditorGUIUtility.singleLineHeight * 4 + 6;

            return base.GetPropertyHeight(property, label);
        }
    }
}
