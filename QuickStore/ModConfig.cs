
namespace QuickStorage
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public float range = -1;
        public int skipSlots = 0;

        public string storeKey = "q";
        public string storeModKey = "";
        public string pullKey = "q";
        public string pullModKey = "left alt";
        public string[] storeIgnore = { "resourceNameHere", "resourcePrefixHere*" };
        public string[] pullIgnore = { "resourceNameHere", "resourcePrefixHere*" };

    }
}