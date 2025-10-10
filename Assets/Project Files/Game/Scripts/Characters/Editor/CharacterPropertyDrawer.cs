using UnityEditor;
using UnityEngine;
using Watermelon.SquadShooter;

namespace Watermelon
{
    [CustomPropertyDrawer(typeof(CharacterData))]
    public class CharacterPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position.width -= 60;

            EditorGUI.LabelField(position, label);

            position.x += EditorGUIUtility.labelWidth;
            position.width -= EditorGUIUtility.labelWidth;

            Rect propertyPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            DrawBlock(propertyPosition, property);

            EditorGUI.EndProperty();
        }

        private void DrawBlock(Rect propertyPosition, SerializedProperty property)
        {
            float defaultYPosition = propertyPosition.y;

            EditorGUI.LabelField(propertyPosition, "Selected character:");

            propertyPosition.y += EditorGUIUtility.singleLineHeight + 2;

            property.objectReferenceValue = EditorGUI.ObjectField(propertyPosition, property.objectReferenceValue, typeof(CharacterData), false);

            propertyPosition.y += EditorGUIUtility.singleLineHeight + 2;

            Rect boxRect = new Rect(propertyPosition.x + propertyPosition.width + 2, defaultYPosition, 58, 58);

            GUI.Box(boxRect, GUIContent.none);

            if(property.objectReferenceValue != null)
            {
                CharacterData character = (CharacterData)property.objectReferenceValue;
                Texture2D previewTexture = AssetPreview.GetAssetPreview(character.PreviewSprite);

                if (character != null)
                {
                    GUI.DrawTexture(new Rect(boxRect.x + 2, boxRect.y + 2, 55, 55), previewTexture);
                }
                else
                {
                    GUI.DrawTexture(new Rect(boxRect.x + 2, boxRect.y + 2, 55, 55), EditorCustomStyles.GetMissingIcon());
                }
            }
            else
            {
                GUI.DrawTexture(new Rect(boxRect.x + 2, boxRect.y + 2, 55, 55), EditorCustomStyles.GetMissingIcon());
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Mathf.Clamp(EditorGUIUtility.singleLineHeight * 3 + 2, 58, float.MaxValue);
        }
    }
}
