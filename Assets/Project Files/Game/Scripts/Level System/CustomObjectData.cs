using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Watermelon.LevelSystem
{
    [System.Serializable]
    public class CustomObjectData : IEquatable<CustomObjectData>
    {
        public GameObject PrefabRef;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public CustomObjectData(GameObject prefabRef, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            PrefabRef = prefabRef;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CustomObjectData);
        }

        public bool Equals(CustomObjectData other)
        {
            return other is not null &&
                   EqualityComparer<GameObject>.Default.Equals(PrefabRef, other.PrefabRef) &&
                   Position.Equals(other.Position) &&
                   Rotation.Equals(other.Rotation) &&
                   Scale.Equals(other.Scale);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PrefabRef, Position, Rotation, Scale);
        }

        public static bool operator ==(CustomObjectData left, CustomObjectData right)
        {
            return EqualityComparer<CustomObjectData>.Default.Equals(left, right);
        }

        public static bool operator !=(CustomObjectData left, CustomObjectData right)
        {
            return !(left == right);
        }
    }
}
