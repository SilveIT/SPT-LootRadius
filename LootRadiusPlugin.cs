using BepInEx;
using DrakiaXYZ.LootRadius.Helpers;
using DrakiaXYZ.LootRadius.Patches;
using EFT.InventoryLogic;
using EFT;
using System;
using Comfort.Common;

namespace DrakiaXYZ.LootRadius
{
    [BepInPlugin("xyz.drakia.lootradius", "DrakiaXYZ-LootRadius", "1.1.0")]
    public class LootRadiusPlugin : BaseUnityPlugin
    {
        public static StashClass RadiusStash;

        private void Awake()
        {
            Settings.Init(Config);

            new GameStartedPatch().Enable();
            new LootPanelOpenPatch().Enable();
            new LootPanelClosePatch().Enable();
            new QuestItemDragPatch().Enable();
        }

        public static void InitFakeStash()
        {
            // Setup the radius stash on raid start
            if (RadiusStash == null)
            {
                // Create our fake stash, note we use "fake" here to have the label show as "LOOT"
                RadiusStash = Singleton<ItemFactory>.Instance.CreateFakeStash("fake");
                StashGrid stashGridClass = new StashGrid(RadiusStash.Grid.Id, 10, 10, true, false, Array.Empty<ItemFilter>(), RadiusStash);
                RadiusStash.Grids = new StashGrid[] { stashGridClass };
                var traderController = new TraderControllerClass(RadiusStash, "RadiusStash", "Nearby Items", false, EOwnerType.Profile, null, null);
                Singleton<GameWorld>.Instance.ItemOwners.Add(traderController, default);

                // Destroy the loot item from the world when we take it
                traderController.RemoveItemEvent += (GEventArgs3 args) => {
                    // Only trigger on Success
                    if (args.Status != CommandStatus.Succeed)
                    {
                        return;
                    }

                    // Only destroy if it exists, to avoid throwing errors
                    if (Helpers.Utils.FindLootById(args.Item.Id) != null)
                    {
                        Singleton<GameWorld>.Instance.DestroyLoot(args.Item.Id);
                    }
                };
            }
        }
    }
}
