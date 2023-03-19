using RoR2;
using BepInEx;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System;

namespace ListItemLocations
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "zinq7";
        public const string PluginName = "ListItemLocations";
        public const string PluginVersion = "1.0.1";
        public static readonly string path = $"{Assembly.GetExecutingAssembly().Location}/../../../ItemLogs.log";

        public static BepInEx.Configuration.ConfigEntry<LogLevel> logLevel;
        public static BepInEx.Configuration.ConfigEntry<bool> saveToFile;

        private static List<LocationInfo> locations = new();
        HashSet<UnityEngine.GameObject> alreadyLoggedObjs = new();
        private static int stagesLogged = -1;

        public void Awake()
        {
            Log.Init(Logger);

            logLevel = Config.Bind<LogLevel>(
                "Functionality",
                "Log Level",
                LogLevel.ItemsAndSources,
                "How much information the game will provide to the console. JSON doesn't work rn"
            );

            saveToFile = Config.Bind<bool>(
                "Functionality",
                "Save to File?",
                true,
                "Whether or not to also save the data to a log file"
            );

            File.WriteAllText(path, "Start Instance of new Game"); // "clear" text file

            On.RoR2.Run.Start += AppendNewRun;
            On.RoR2.Run.AdvanceStage += LogAndUnlog;
            On.RoR2.ChestBehavior.Roll += LogChestRoll;
            On.RoR2.ShopTerminalBehavior.SetPickupIndex += SetPrinterIndex;
            On.RoR2.ShopTerminalBehavior.GenerateNewPickupServer += GetMultishopItems;
            On.RoR2.OptionChestBehavior.Roll += LogAllLoot;
            On.RoR2.ShrineChanceBehavior.Start += ChanceShenanigans;
            On.RoR2.RouletteChestController.OnEnable += ListItems;

            Log.LogDebug(PluginGUID + " Awake Done");
        }

        private void AppendNewRun(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);

            if (saveToFile.Value) File.AppendAllText(path, $"\n\nNew Run Started at {DateTime.Now}");

            stagesLogged = -1;
        }

        private void GetMultishopItems(On.RoR2.ShopTerminalBehavior.orig_GenerateNewPickupServer orig, ShopTerminalBehavior self)
        {
            orig(self);

            if (alreadyLoggedObjs.Contains(self.gameObject)) return;
            else alreadyLoggedObjs.Add(self.gameObject);

            if (self.pickupIndex.value == -1) return; // BAD_PICKUP_INDEX

            Append(self.gameObject.name, self.pickupIndex, self.transform.position.x, self.transform.position.y);
        }

        public static void LogSomeData()
        {
            locations.Sort((x, y) => {
                if (!x.isItem || !y.isItem)
                {
                    return 0;
                }
                else
                {
                    return ItemCatalog.GetItemDef(PickupCatalog.GetPickupDef(x.item).itemIndex).tier.CompareTo(ItemCatalog.GetItemDef(PickupCatalog.GetPickupDef(y.item).itemIndex).tier);
                }

            });

            string file = "";

            if (Run.instance is not null)
            {
                if (Run.instance.stageClearCount > stagesLogged)
                {
                    file += "\n\n\nStage " + (Run.instance.stageClearCount + 1);
                    stagesLogged = Run.instance.stageClearCount;
                }
            }



            foreach (var loc in locations)
            {
                file += "\n" + loc.AsString();
                UnityEngine.Debug.Log(loc.AsString());
            }

            if (saveToFile.Value) File.AppendAllText(path, file);
            //Log.LogDebug("APPENDED FILE");

            locations.Clear();
        }

        private void LogAllLoot(On.RoR2.OptionChestBehavior.orig_Roll orig, OptionChestBehavior self)
        {
            orig(self);
            int count = 1;
            foreach (var itemPickup in self.generatedDrops)
            {
                Append(self.gameObject.name + $" #{count}", itemPickup, self.gameObject.transform.position.x, self.gameObject.transform.position.y);
                count++;
            }
        }

        private void LogAndUnlog(On.RoR2.Run.orig_AdvanceStage orig, Run self, SceneDef nextScene)
        {
            alreadyLoggedObjs.Clear();
            orig(self, nextScene);
        }
        private void ChanceShenanigans(On.RoR2.ShrineChanceBehavior.orig_Start orig, ShrineChanceBehavior self)
        {
            const int SHRINE_MAX = 2;
            orig(self);

            ulong state0 = self.rng.state0, state1 = self.rng.state1;
            int hits = 0, succcs = 0;
            while (succcs < SHRINE_MAX)
            {
                hits++;
                if (self.rng.nextNormalizedFloat > self.failureChance)
                {
                    Append(self.gameObject.name + " #" + hits, self.dropTable.GenerateDrop(self.rng), self.transform.position.x, self.transform.position.y);
                    succcs++;
                }
            }
            self.rng.state0 = state0;
            self.rng.state1 = state1; // reset RNG
        }

        private void ListItems(On.RoR2.RouletteChestController.orig_OnEnable orig, RouletteChestController self)
        {
            orig(self);
            foreach (var entry in self.entries)
            {
                Append(self.gameObject.name, entry.pickupIndex, self.transform.position.x, self.transform.position.y);
            }
        }

        private void SetPrinterIndex(On.RoR2.ShopTerminalBehavior.orig_SetPickupIndex orig, ShopTerminalBehavior self, PickupIndex newPickupIndex, bool newHidden)
        {
            orig(self, newPickupIndex, newHidden);

            if (alreadyLoggedObjs.Contains(self.gameObject)) return;
            else if (newPickupIndex.isValid) alreadyLoggedObjs.Add(self.gameObject);

            if (self.pickupIndex == PickupCatalog.FindPickupIndex(ItemIndex.None)) return;

            Append(self.gameObject.name, self.pickupIndex, self.transform.position.x, self.transform.position.y);
        }

        private void LogChestRoll(On.RoR2.ChestBehavior.orig_Roll orig, ChestBehavior self)
        {
            orig(self);
            Append(self.gameObject.name, self.dropPickup, self.transform.position.x, self.transform.position.y);
        }

        private void Append(string name, PickupIndex item, float x, float y)
        {
            locations.Add(new LocationInfo(name, item, x, y));
            LogSomeData(); // force update
        }

        private class LocationInfo
        {
            public string objectType;
            public PickupIndex item;
            public float x, y;
            public bool isItem;
            private string? nameToken = null;
            
            public LocationInfo(string obj, PickupIndex item, float x, float y)
            {
                this.objectType = obj;
                this.item = item;
                this.x = x;
                this.y = y;
                isItem = ItemCatalog.GetItemDef(item.itemIndex) is not null;
                nameToken = PickupCatalog.GetPickupDef(item)?.nameToken;
            }

            public string AsString()
            {
                string ret = (nameToken is not null) ? $"{Language.GetString(nameToken)}" : "NON_ITEM"; // string format just name :racesR:

                if (logLevel.Value == LogLevel.OnlyItems)
                {
                    return ret;
                }

                if (logLevel.Value >= LogLevel.ItemsAndSources)
                {
                    ret += $"           in {objectType}";
                }

                if (logLevel.Value >= LogLevel.AllInfo)
                {
                    ret += $"           at ({x}, {y})";
                }

                if (logLevel.Value == LogLevel.FuckMeJSON)
                {
                    UnityEngine.Debug.Log("TBI"); // not yet
                }

                return ret;
            }
        }

        public enum LogLevel
        {
            NoLogging = -1,
            OnlyItems = 0,
            ItemsAndSources = 1,
            AllInfo = 2,
            FuckMeJSON = 3
        }
    }
}