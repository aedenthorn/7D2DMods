
namespace KillNotification
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = false;
        public bool notifyAllKills = true;
        public bool chimeAllKills = false;
        public float chimeVolume = 0.9f;
        public float chimePitch = 0;
        public string notificationSound = "coins_grab";
        public string notificationIcon = "ui_game_symbol_death";
        public string notificationTextSingle = "{name}";
        public string notificationTextPlural = "{name} ({number})";
    }
}