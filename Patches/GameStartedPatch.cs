using EFT.InventoryLogic;
using EFT;
using System;
using System.Reflection;
using Aki.Reflection.Patching;
using Comfort.Common;

namespace DrakiaXYZ.LootRadius.Patches
{
    public class GameStartedPatch : ModulePatch
    {
        private static StashClass Stash => LootRadiusPlugin.RadiusStash;

            protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));
        }

        [PatchPostfix]
        public static void PatchPostfix()
        {
            LootRadiusPlugin.InitFakeStash();
        }
    }
}
