using System;
using System.Collections.Generic;
using System.Text;
using RoR2;

namespace ListItemLocations
{
    public static class JsonTypes
    {
        public class LocationInfo
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
                this.isItem = ItemCatalog.GetItemDef(item.itemIndex) is not null;
                this.nameToken = PickupCatalog.GetPickupDef(item)?.nameToken;
            }

            public string AsString()
            {
                string ret = (nameToken is not null) ? $"{Language.GetString(nameToken)}" : "NON_ITEM"; // string format just name :racesR:

                if (Main.logLevel.Value == LogLevel.OnlyItems)
                {
                    return ret;
                }

                if (Main.logLevel.Value >= LogLevel.ItemsAndSources)
                {
                    ret += $"           in {objectType}";
                }

                if (Main.logLevel.Value >= LogLevel.AllInfo)
                {
                    ret += $"           at ({x}, {y})";
                }

                if (Main.logLevel.Value == LogLevel.FuckMeJSON)
                {
                    UnityEngine.Debug.Log("TBI"); // not yet
                }

                return ret;
            }
        }

        public class UsefulInfo
        {
            public float x, y;
            public ItemTier tier;
            public string englishName;
            public string source;
            public UsefulInfo(float x, float y, ItemTier tier, string englishName, string source)
            {
                this.x = x;
                this.y = y;
                this.tier = tier;
                this.englishName = englishName;
                this.source = source;
            }
        }

        public class StageLoot
        {
            public string stageName = "buff";
            public int stageNum = -1;
            public Dictionary<int, List<UsefulInfo>> stageLoot = new();
        }

        public class RunInfo
        {
            public long startTime = System.DateTime.Now.ToFileTimeUtc();
            public StageLoot loot;
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
