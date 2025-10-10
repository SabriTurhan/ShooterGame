using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Watermelon.LevelSystem
{
    [System.Serializable]
    public class ItemEntityData : IEquatable<ItemEntityData>
    {
        public int Hash;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public ItemEntityData(int hash, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Hash = hash;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ItemEntityData);
        }

        public bool Equals(ItemEntityData other)
        {
            return other is not null &&
                   Hash == other.Hash &&
                   Position.Equals(other.Position) &&
                   Rotation.Equals(other.Rotation) &&
                   Scale.Equals(other.Scale);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Hash, Position, Rotation, Scale);
        }

        public static bool operator ==(ItemEntityData left, ItemEntityData right)
        {
            return EqualityComparer<ItemEntityData>.Default.Equals(left, right);
        }

        public static bool operator !=(ItemEntityData left, ItemEntityData right)
        {
            return !(left == right);
        }
    }
}
