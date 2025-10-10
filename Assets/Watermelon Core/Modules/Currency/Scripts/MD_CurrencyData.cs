using UnityEngine;

namespace Watermelon
{
    [System.Serializable]
    public class CurrencyData
    {
        [SerializeField] GameObject dropModel;
        public GameObject DropModel => dropModel;

        [SerializeField] AudioClip dropPickupSound;
        public AudioClip DropPickupSound => dropPickupSound;

        [Space]
        [SerializeField] bool displayAlways = false;
        public bool DisplayAlways => displayAlways;

        public void Init(Currency currency)
        {

        }
    }
}