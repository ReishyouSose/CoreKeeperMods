namespace TomeOfMimicry
{
    public static class BlueprintWorkload
    {
        public const int MaxTileCommands = 24000;
        public const int MaxObjects = 512;

        public static bool TryValidate(Blueprint blueprint, BlueprintPasteMode mode, out string reason)
        {
            reason = null;
            if (blueprint == null)
            {
                reason = "Blueprint is empty.";
                return false;
            }
            if (blueprint.objects.Count > MaxObjects)
            {
                reason = $"Blueprint has {blueprint.objects.Count} objects; the safe limit is {MaxObjects}.";
                return false;
            }

            long commands = EstimateTileCommands(blueprint, mode);
            if (commands > MaxTileCommands)
            {
                reason = $"Paste needs about {commands} tile operations; the safe limit is {MaxTileCommands}. Split it into smaller blueprints.";
                return false;
            }
            return true;
        }

        public static long EstimateTileCommands(Blueprint blueprint, BlueprintPasteMode mode)
        {
            long commands = 0;
            foreach (var tile in blueprint.tiles)
            {
                if (mode == BlueprintPasteMode.Replace)
                    commands += 6 + BlueprintLayers.Extras.Length;

                bool bridge = tile.hasFloor && tile.floorTileKind == PugTilemap.TileType.bridge;
                if (tile.hasGround && !bridge) commands += 3;
                if (bridge) commands += 6;
                if (tile.hasWall) commands += 2;
                if (tile.hasFloor && !bridge) commands++;
                if (tile.hasRail) commands++;
                if (tile.hasWire) commands++;
                if (tile.extras != null) commands += tile.extras.Count * 2L;
            }
            return commands;
        }
    }
}
