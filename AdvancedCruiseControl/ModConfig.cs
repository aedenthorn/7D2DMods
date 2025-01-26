
namespace AdvancedCruiseControl
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool cruiseEnabled = true;
        public bool isDebug = true;

        public string toggleKey = "c";
        public string accelKey = "up";
        public string decelKey = "down";
        public string toggleText = "Cruise enabled: {0}";
        public string speedText = "Cruise speed: {0}x";
        public double maxSpeedMult = 2.0;
        public float fuelConsumeMult = 1.0f;
        public bool showFuelEconomy = false;

    }
}