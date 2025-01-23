
namespace QuickStore
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public float range = -1;

        public string storeKey = "q";
        public string modKey = "left alt";
        public string[] storeIgnore = { "resourceNameHere", "resourcePrefixHere*" };
        public string[] pullIgnore = { "resourceNameHere", "resourcePrefixHere*" };

    }
}