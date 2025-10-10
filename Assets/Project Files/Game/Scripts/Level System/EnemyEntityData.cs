using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Watermelon.SquadShooter;

namespace Watermelon.LevelSystem
{
    [System.Serializable]
    public class EnemyEntityData : IEquatable<EnemyEntityData>
    {
        public EnemyType EnemyType;

        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale = Vector3.one;

        public bool IsElite;

        public Vector3[] PathPoints;

        public EnemyEntityData(EnemyType enemyType, Vector3 position, Quaternion rotation, Vector3 scale, bool isElite, Vector3[] pathPoints)
        {
            EnemyType = enemyType;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            IsElite = isElite;
            PathPoints = pathPoints;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EnemyEntityData);
        }

        public bool Equals(EnemyEntityData other)
        {
            return other is not null &&
                   EnemyType == other.EnemyType &&
                   Position.Equals(other.Position) &&
                   Rotation.Equals(other.Rotation) &&
                   Scale.Equals(other.Scale) &&
                   IsElite == other.IsElite &&
                   PathPoints.SequenceEqual(other.PathPoints);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EnemyType, Position, Rotation, Scale, IsElite, PathPoints);
        }

        public static bool operator ==(EnemyEntityData left, EnemyEntityData right)
        {
            return EqualityComparer<EnemyEntityData>.Default.Equals(left, right);
        }

        public static bool operator !=(EnemyEntityData left, EnemyEntityData right)
        {
            return !(left == right);
        }
    }
}