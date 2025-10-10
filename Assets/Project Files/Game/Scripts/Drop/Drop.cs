using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Watermelon.SquadShooter;
using Random = UnityEngine.Random;

namespace Watermelon.LevelSystem
{
    public static class Drop
    {
        private static List<IDropItem> dropItems = new List<IDropItem>();
        private static DropAnimation[] dropAnimations;
        private static List<BaseDropBehavior> activeObjects = new List<BaseDropBehavior>();

        /// <summary>
        /// Initializes the drop system with the specified settings.
        /// </summary>
        /// <param name="dropSettings">The settings for the drop system.</param>
        public static void Init(DropableItemSettings dropSettings)
        {
            if (dropSettings == null)
            {
                Debug.LogError("DropableItemSettings cannot be null");

                return;
            }

            activeObjects = new List<BaseDropBehavior>();

            dropAnimations = dropSettings.DropAnimations;

            foreach (CustomDropItem customDropItem in dropSettings.CustomDropItems)
            {
                RegisterDropItem(customDropItem);
            }

            // Register currencies drop
            CurrencyDropItem currencyDropItem = new CurrencyDropItem();
            currencyDropItem.SetCurrencies(CurrencyController.Currencies);

            RegisterDropItem(currencyDropItem);

            // Register weapons drop
            RegisterDropItem(new WeaponDropItem());

            // Register characters drop
            RegisterDropItem(new CharacterDropItem());
        }

        /// <summary>
        /// Automatically collects all auto-pickable items.
        /// </summary>
        public static void AutoCollect()
        {
            if (!activeObjects.IsNullOrEmpty())
            {
                foreach (BaseDropBehavior item in activeObjects)
                {
                    if (!item.IsPicked && item.IsAutoPickable)
                    {
                        item.ApplyReward(true);
                    }
                }
            }
        }

        /// <summary>
        /// Unloads all registered drop items and clears the drop system.
        /// </summary>
        public static void Unload()
        {
            if (!dropItems.IsNullOrEmpty())
            {
                foreach (IDropItem item in dropItems)
                {
                    item.Unload();
                }

                dropItems.Clear();
            }

            dropAnimations = null;
        }

        /// <summary>
        /// Called when a room is loaded, destroys all active drop objects.
        /// </summary>
        public static void OnRoomLoaded()
        {
            if (activeObjects.Count > 0)
                DestroyActiveObjects();
        }

        /// <summary>
        /// Destroys all active drop objects.
        /// </summary>
        public static void DestroyActiveObjects()
        {
            if (activeObjects.IsNullOrEmpty()) return;

            foreach (BaseDropBehavior activeObject in activeObjects)
            {
                activeObject?.Unload();
            }

            activeObjects.Clear();
        }

        /// <summary>
        /// Removes a drop object from the list of active objects.
        /// </summary>
        /// <param name="dropObject">The drop object to remove.</param>
        public static void RemoveObject(BaseDropBehavior dropObject)
        {
            activeObjects.Remove(dropObject);
        }

        /// <summary>
        /// Registers a new drop item.
        /// </summary>
        /// <param name="dropItem">The drop item to register.</param>
        public static void RegisterDropItem(IDropItem dropItem)
        {
            if (dropItem == null)
            {
                Debug.LogError("Drop item cannot be null.");

                return;
            }

#if UNITY_EDITOR
            if (dropItems.Exists(item => item.DropItemType == dropItem.DropItemType))
            {
                Debug.LogError($"Drop item with type {dropItem.DropItemType} is already registered!");

                return;
            }
#endif

            dropItems.Add(dropItem);

            dropItem.Init();
        }

        /// <summary>
        /// Gets a drop item by its type.
        /// </summary>
        /// <param name="dropableItemType">The type of the drop item.</param>
        /// <returns>The drop item if found, otherwise null.</returns>
        public static IDropItem GetDropItem(DropableItemType dropableItemType)
        {
            return dropItems.Find(item => item.DropItemType == dropableItemType);
        }

        /// <summary>
        /// Gets a drop animation by its falling style.
        /// </summary>
        /// <param name="dropFallingStyle">The falling style of the drop animation.</param>
        /// <returns>The drop animation if found, otherwise null.</returns>
        public static DropAnimation GetAnimation(DropFallingStyle dropFallingStyle)
        {
            return dropAnimations?.FirstOrDefault(animation => animation.FallStyle == dropFallingStyle);
        }

        /// <summary>
        /// Drops an item at the specified position with the given parameters.
        /// </summary>
        /// <param name="dropData">The data for the drop.</param>
        /// <param name="spawnPosition">The position to spawn the drop.</param>
        /// <param name="rotation">The rotation of the drop.</param>
        /// <param name="fallingStyle">The falling style of the drop.</param>
        /// <param name="availableToPickDelay">The delay before the item can be picked up.</param>
        /// <param name="autoPickDelay">The delay before the item is automatically picked up.</param>
        /// <param name="rewarded">Whether the drop is rewarded.</param>
        /// <returns>The game object of the dropped item.</returns>
        private static GameObject DropItem(DropData dropData, Vector3 spawnPosition, Vector3 rotation, DropFallingStyle fallingStyle, float availableToPickDelay = -1f, float autoPickDelay = -1f, bool rewarded = false)
        {
            IDropItem dropItem = GetDropItem(dropData.DropType);
            if (dropItem == null) return null;

            GameObject dropPrefab = dropItem.GetDropObject(dropData);
            if (dropPrefab == null) return null;

            GameObject itemGameObject = GameObject.Instantiate(dropItem.GetDropObject(dropData));

            BaseDropBehavior item = itemGameObject.GetComponent<BaseDropBehavior>();
            item.Init(dropData, availableToPickDelay, autoPickDelay);
            item.IsRewarded = rewarded;

            itemGameObject.transform.position = spawnPosition + (Random.insideUnitSphere * 0.05f);
            itemGameObject.transform.localScale = Vector3.one;
            itemGameObject.transform.eulerAngles = rotation;
            itemGameObject.SetActive(true);

            activeObjects.Add(item);

            return itemGameObject;
        }

        public static void ThrowItem(BaseDropBehavior baseDropBehavior, DropFallingStyle fallingStyle)
        {
            DropAnimation dropAnimation = GetAnimation(fallingStyle);

            Transform itemTransform = baseDropBehavior.transform;
            Vector3 spawnPosition = itemTransform.position;

            itemTransform.position = itemTransform.position + new Vector3(0, dropAnimation.OffsetY, 0);

            Vector3 targetPosition = spawnPosition.GetRandomPositionAroundObject(dropAnimation.Radius * 0.9f, dropAnimation.Radius * 1.2f).AddToY(0.1f);

            baseDropBehavior.Throw(targetPosition, dropAnimation, dropAnimation.FallTime);
        }

        public static void SpawnDropItem(DropData dropData, Vector3 spawnPosition, Vector3 rotation, bool isRewarded, Action<BaseDropBehavior, DropFallingStyle> spawnCallback = null)
        {
            if (dropData.DropType == DropableItemType.Currency)
            {
                int itemsAmount = Mathf.Clamp(Random.Range(9, 11), 1, dropData.Amount);

                List<int> itemValues = LevelController.SplitIntEqually(dropData.Amount, itemsAmount);

                for (int j = 0; j < itemsAmount; j++)
                {
                    int tempIndex = j;
                    Tween.DelayedCall(j * 0.01f, () =>
                    {
                        DropData data = dropData.Clone();
                        data.Amount = itemValues[tempIndex];

                        GameObject dropObject = Drop.DropItem(data, spawnPosition, Vector3.zero.SetY(Random.Range(0f, 360f)), DropFallingStyle.Coin, itemValues[tempIndex], 0.5f, rewarded: isRewarded);
                        if (dropObject != null)
                        {
                            CurrencyDropBehavior dropBehavior = dropObject.GetComponent<CurrencyDropBehavior>();
                            if (dropBehavior != null)
                            {
                                dropBehavior.SetCurrencyData(data.CurrencyType, itemValues[tempIndex]);

                                spawnCallback?.Invoke(dropBehavior, DropFallingStyle.Coin);
                            }
                        }
                    });
                }
            }
            else if (dropData.DropType == DropableItemType.WeaponCard)
            {
                for (int j = 0; j < dropData.Amount; j++)
                {
                    GameObject dropObject = Drop.DropItem(dropData, spawnPosition, Vector3.zero, DropFallingStyle.Default, 1, 0.6f);
                    if (dropObject != null)
                    {
                        WeaponCardDropBehavior card = dropObject.GetComponent<WeaponCardDropBehavior>();
                        if(card != null)
                        {
                            card.SetCardData(dropData.Weapon);

                            spawnCallback?.Invoke(card, DropFallingStyle.Default);
                        }
                    }
                }
            }
            else if (dropData.DropType == DropableItemType.Character)
            {
                GameObject dropObject = Drop.DropItem(dropData, spawnPosition, Vector3.zero.SetY(Random.Range(0f, 360f)), DropFallingStyle.Default, 1);
                if (dropObject != null)
                {
                    CharacterDropBehavior characterDropBehavior = dropObject.GetComponent<CharacterDropBehavior>();
                    if(characterDropBehavior != null)
                    {
                        characterDropBehavior.SetCharacterData(dropData.Character, dropData.Level);

                        spawnCallback?.Invoke(characterDropBehavior, DropFallingStyle.Default);
                    }
                }
            }
            else if (dropData.DropType == DropableItemType.Weapon)
            {
                GameObject dropObject = Drop.DropItem(dropData, spawnPosition, Vector3.zero.SetY(Random.Range(0f, 360f)), DropFallingStyle.Default, 1);
                if (dropObject != null)
                {
                    WeaponDropBehavior weaponDropBehavior = dropObject.GetComponent<WeaponDropBehavior>();
                    if(weaponDropBehavior != null)
                    {
                        weaponDropBehavior.SetWeaponData(dropData.Weapon, dropData.Level);

                        spawnCallback?.Invoke(weaponDropBehavior, DropFallingStyle.Default);
                    }
                }
            }
            else if (dropData.DropType == DropableItemType.Heal)
            {
                GameObject dropObject = Drop.DropItem(dropData, spawnPosition, Vector3.zero.SetY(Random.Range(0f, 360f)), DropFallingStyle.Default, 1);
                if (dropObject != null)
                {
                    HealDropBehaviour healDropBehaviour = dropObject.GetComponent<HealDropBehaviour>();
                    if (healDropBehaviour != null)
                    {
                        healDropBehaviour.SetData(dropData.Amount);

                        spawnCallback?.Invoke(healDropBehaviour, DropFallingStyle.Default);
                    }
                }
            }
            else
            {
                GameObject dropObject = Drop.DropItem(dropData, spawnPosition, Vector3.zero.SetY(Random.Range(0f, 360f)), DropFallingStyle.Default, 1);
                if (dropObject != null)
                {
                    BaseDropBehavior dropBehavior = dropObject.GetComponent<BaseDropBehavior>();
                    if (dropBehavior != null)
                    {
                        spawnCallback?.Invoke(dropBehavior, DropFallingStyle.Default);
                    }
                }
            }
        }
    }
}