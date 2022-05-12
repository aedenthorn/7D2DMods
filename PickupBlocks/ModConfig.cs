namespace PickupBlocks
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public bool RestrictBlocksToLandClaimArea = true;
        public bool AllowToggleLandClaimRestriction = true;
        public string ToggleModKey = "p";
        public string EmptyFirstMessage = "You must empty the container before picking it up.";
        public string DisabledText = "Pickup Blocks disabled.";
        public string RestrictionEnabledText = "Pickup Blocks enabled within land claim area.";
        public string RestrictionDisabledText = "Pickup Blocks enabled everywhere.";
    }
}