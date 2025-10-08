namespace PickupBlocks
{
    public class ModConfig
    {
        public bool EnableMod = true;
        public bool isDebug = true;
        public bool EnablePickup = false;
        public bool EnableLandClaimRestrict = false;
        public bool EnableMenuRestrict = false;
        public string ToggleKey = "p";
        public string ToggleLandRestrictModKey = "left shift";
        public string ToggleMenuModKey = "right shift";
        public string EmptyFirstMessage = "You must empty the container before picking it up.";
        public string EnabledText = "Pickup Blocks enabled.";
        public string DisabledText = "Pickup Blocks disabled.";
        public string LandEnabledText = "Pickup Blocks allowed within land claim area.";
        public string MenuEnabledText = "Pickup Blocks enabled in context menu only.";
        public string LandDisabledText = "Pickup Blocks allowed everywhere.";
        public string MenuDisabledText = "Pickup Blocks not restriced to context menu.";
    }
}