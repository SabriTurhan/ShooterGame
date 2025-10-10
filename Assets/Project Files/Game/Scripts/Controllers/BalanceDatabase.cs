using UnityEngine;

namespace Watermelon.SquadShooter
{
    [CreateAssetMenu(fileName = "Balance Database", menuName = "Data/Balance Database")]
    public class BalanceDatabase : ScriptableObject
    {
        [SerializeField] bool ignoreDifficulty = false;
        public bool IgnoreDifficulty => ignoreDifficulty;

        [SerializeField] bool showDebugText = false;
        public bool ShowDebugText => showDebugText;

        [SerializeField] DifficultySettings[] difficultyPresets;
        public DifficultySettings[] DifficultyPresets => difficultyPresets;
    }
}