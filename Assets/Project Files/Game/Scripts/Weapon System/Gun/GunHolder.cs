using UnityEngine;

namespace Watermelon.SquadShooter
{
    [System.Serializable]
    public class GunHolder
    {
        [SerializeField] HolderData defaultHolderData;
        public HolderData DefaultHolderData => defaultHolderData;

        [SerializeField] CharacterHolderData[] holderDataOverrides;

        public HolderData GetHolderData(CharacterData character)
        {
            if(!holderDataOverrides.IsNullOrEmpty())
            {
                foreach(CharacterHolderData holderData in holderDataOverrides)
                {
                    if(holderData.Character == character)
                    {
                        return holderData;
                    }
                }
            }

            return defaultHolderData;
        }

        [System.Serializable]
        public class HolderData
        {
            public Transform LeftHandHolder;
            public Transform RightHandHolder;
        }

        [System.Serializable]
        public class CharacterHolderData : HolderData
        {
            [Space]
            [SerializeField] CharacterData character;
            public CharacterData Character => character;
        }
    }
}