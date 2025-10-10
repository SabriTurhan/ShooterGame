using UnityEngine;

namespace Watermelon
{
    [System.Serializable]
    public class ExperienceSave : ISaveObject
    {
        public int CurrentLevel = 1;
        public int CurrentExperiencePoints;
        public int CollectedExperiencePoints;        

        public void Flush()
        {
            
        }
    }
}
