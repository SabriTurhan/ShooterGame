using UnityEngine;

namespace Watermelon.SquadShooter
{
    [System.Serializable]
    public class DifficultySettings
    {
        [SerializeField] string note;
        public string Note => note;

        [Space]
        [SerializeField] float healthMult;
        public float HealthMult => healthMult;

        [SerializeField] float damageMult;
        public float DamageMult => damageMult;

        [SerializeField] float restoredHpMult;
        public float RestoredHpMult => restoredHpMult;

        [SerializeField] int upgradeDifference;
        public int UpgradeDifference => upgradeDifference;

        public DifficultySettings()
        {
            healthMult = 1.0f;
            damageMult = 1.0f;
            restoredHpMult = 1.0f;
            upgradeDifference = 1;
        }

        public DifficultySettings(string note)
        {
            this.note = note;

            healthMult = 1.0f;
            damageMult = 1.0f;
            restoredHpMult = 1.0f;
            upgradeDifference = 1;
        }
    }
}
