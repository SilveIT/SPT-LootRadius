using EFT.Interactive;
using EFT.UI;
using EFT;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Aki.Reflection.Patching;
using UnityEngine;
using Comfort.Common;
using DrakiaXYZ.LootRadius.Helpers;
using EFT.InventoryLogic;

namespace DrakiaXYZ.LootRadius.Patches
{
    public class LootPanelOpenPatch : ModulePatch
    {
        private static FieldInfo _stashViewField;
        private static FieldInfo _rightPaneField;
        private static MethodInfo _addMethod;
        private static MethodInfo _removeMethod;
        private static LayerMask _interactiveLayerMask = 1 << LayerMask.NameToLayer("Interactive");

        private static StashClass Stash => LootRadiusPlugin.RadiusStash;

            protected override MethodBase GetTargetMethod()
        {
            _addMethod = AccessTools.Method(typeof(StashGrid), "Add", new Type[] { typeof(Item) });
            _removeMethod = AccessTools.Method(typeof(ItemAddress), "Remove");

            // Find the stash interface variable, based on the implemented types of the SimpleStashPanel
            Type stashInterfaceType = null;
            Type[] stashInterfaceTypes = typeof(SimpleStashPanel).GetInterfaces();
            foreach (Type type in stashInterfaceTypes)
            {
                if (type.Name.StartsWith("GInterface"))
                {
                    stashInterfaceType = type;
                    break;
                }
            }
            _stashViewField = AccessTools.GetDeclaredFields(typeof(ItemsPanel)).Single(x => x.FieldType == stashInterfaceType);

            // Find the variable that stores the right hand grid in the ItemUiContext, so we can Ctrl+Click
            _rightPaneField = AccessTools.GetDeclaredFields(typeof(ItemUiContext)).Single(x => x.FieldType == typeof(LootItemClass[]));

            return typeof(ItemsPanel).GetMethod(nameof(ItemsPanel.Show));
        }

        [PatchPostfix]
        public static async void PatchPostfix(
            ItemsPanel __instance,
            Task __result,
            AbstractItemContext sourceContext,
            LootItemClass lootItem,
            InventoryControllerClass inventoryController,
            ItemsPanel.EItemsTab currentTab,
            SimpleStashPanel ____simpleStashPanel
        )
        {
            // Wait for original to finish
            await __result;

            // If lootItem isn't null, don't do anything, it means there's a right hand panel already
            if (lootItem != null)
            {
                return;
            }

            if (____simpleStashPanel == null)
            {
                Logger.LogError("[LootPanelOpenPatch] ____simpleStashPanel == null");
                return;
            }

            if (inventoryController == null)
            {
                Logger.LogError("[LootPanelOpenPatch] inventoryController == null");
                return;
            }

            if (sourceContext == null)
            {
                Logger.LogError("[LootPanelOpenPatch] sourceContext == null");
                return;
            }
            LootRadiusPlugin.InitFakeStash();
            if (Stash == null)
            {
                Logger.LogError("[LootPanelOpenPatch] _stash == null");
                return;
            }
            if (Stash.Grids.Length == 0)
            {
                Logger.LogError("[LootPanelOpenPatch] _stash.Grids.Length == 0");
                return;
            }
            var grid = Stash.Grids[0];
            if (Stash.Grids[0] == null)
            {
                Logger.LogError("[LootPanelOpenPatch] _stash.Grids[0] == null");
                return;
            }

            if (Singleton<GameWorld>.Instance.MainPlayer == null)
            {
                Logger.LogError("[LootPanelOpenPatch] Singleton<GameWorld>.Instance.MainPlayer == null");
                return;
            }
            Vector3 playerPosition = Singleton<GameWorld>.Instance.MainPlayer.Position;

            // First find any items directly near the player's feet, to allow them to loot things like items slightly under the floor
            Collider[] floorItemColliders = Physics.OverlapSphere(playerPosition, 0.35f, _interactiveLayerMask);
            AddAllowedItems(grid, floorItemColliders, true);

            // Then collect items around the player body, based on the loot radius
            playerPosition += (Vector3.up * 0.5f);
            Collider[] nearbyItemColliders = Physics.OverlapSphere(playerPosition, Settings.LootRadius.Value, _interactiveLayerMask);
            AddAllowedItems(grid, nearbyItemColliders, false);

            // Show the stash in the inventory panel
            ____simpleStashPanel.Configure(Stash, inventoryController, sourceContext.CreateChild(Stash));
            _stashViewField.SetValue(__instance, ____simpleStashPanel);
            ____simpleStashPanel.Show(inventoryController, currentTab);

            _rightPaneField.SetValue(ItemUiContext.Instance, new LootItemClass[] { Stash });
        }

        private static void AddAllowedItems(StashGrid grid, Collider[] colliders, bool ignoreLineOfSight)
        {
            foreach (Collider collider in colliders)
            {
                var item = collider.gameObject.GetComponentInParent<LootItem>();
                if (item != null && item.Item.Parent.Container != grid && (ignoreLineOfSight || IsLineOfSight(item.transform.position)))
                {
                    item.Item.OriginalAddress = item.Item.CurrentAddress;
                    _removeMethod.Invoke(item.Item.CurrentAddress, new object[] { item.Item, string.Empty, false });
                    _addMethod.Invoke(grid, new object[] { item.Item });
                }
            }
        }

        /**
         * Return true if the end position is within line of sight of the player
         */
        private static bool IsLineOfSight(Vector3 endPos)
        {
            // Start at the player's head
            Vector3 startPos = Singleton<GameWorld>.Instance.MainPlayer.MainParts[BodyPartType.head].Position;

            // LineCast returns true if it hits a HighPolyCollider, indicating the item isn't within line of sight of the player's head
            if (Physics.Linecast(startPos, endPos, LayerMaskClass.HighPolyWithTerrainMask))
            {
                return false;
            }

            return true;
        }
    }
}
