using System;
using System.Collections.Generic;
using UnityEngine;

namespace Watermelon.LevelSystem
{
    [System.Serializable]
    public class ChestEntityData : IEquatable<ChestEntityData>
    {
        public bool IsInited = false;
        public LevelChestType ChestType;
        public CurrencyType RewardCurrency;
        public int RewardValue = 5;
        public int DroppedCurrencyItemsAmount = 1;

        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public ChestEntityData(LevelChestType chestType, Vector3 position, Quaternion rotation, Vector3 scale, CurrencyType rewardCurrency, int rewardValue, int droppedCurrencyItemsAmount)
        {
            ChestType = chestType;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            RewardCurrency = rewardCurrency;
            RewardValue = rewardValue;
            DroppedCurrencyItemsAmount = droppedCurrencyItemsAmount;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ChestEntityData);
        }

        public bool Equals(ChestEntityData other)
        {
            return other is not null &&
                   ChestType == other.ChestType &&
                   RewardCurrency == other.RewardCurrency &&
                   RewardValue == other.RewardValue &&
                   DroppedCurrencyItemsAmount == other.DroppedCurrencyItemsAmount &&
                   Position.Equals(other.Position) &&
                   Rotation.Equals(other.Rotation) &&
                   Scale.Equals(other.Scale);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ChestType, RewardCurrency, RewardValue, DroppedCurrencyItemsAmount, Position, Rotation, Scale);
        }

        public static bool operator ==(ChestEntityData left, ChestEntityData right)
        {
            return EqualityComparer<ChestEntityData>.Default.Equals(left, right);
        }

        public static bool operator !=(ChestEntityData left, ChestEntityData right)
        {
            return !(left == right);
        }
    }
}