using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ClipPeek
{
    internal sealed class Settings
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly string _path;

        internal int IconX = int.MinValue;
        internal int IconY = int.MinValue;
        internal bool HasPosition;
        internal bool AutoStart = true;
        internal bool Hidden;
        internal bool Paused;

        internal string Path_
        {
            get { return _path; }
        }

        internal Settings(string file)
        {
            _path = file;
            Serializer.MaxJsonLength = 4 * 1024 * 1024;
            Load();
        }

        internal void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;

                Dictionary<string, object> map =
                    Serializer.DeserializeObject(File.ReadAllText(_path)) as Dictionary<string, object>;
                if (map == null) return;

                IconX = Int(map, "iconX", int.MinValue);
                IconY = Int(map, "iconY", int.MinValue);
                HasPosition = Bool(map, "hasPosition", false);
                AutoStart = Bool(map, "autoStart", true);
                Hidden = Bool(map, "hidden", false);
                Paused = Bool(map, "paused", false);
            }
            catch (Exception ex)
            {
                Log.Error("Settings.Load", ex);
            }
        }

        internal void Save()
        {
            try
            {
                Dictionary<string, object> map = new Dictionary<string, object>();
                map["iconX"] = IconX;
                map["iconY"] = IconY;
                map["hasPosition"] = HasPosition;
                map["autoStart"] = AutoStart;
                map["hidden"] = Hidden;
                map["paused"] = Paused;

                File.WriteAllText(_path, Serializer.Serialize(map));
            }
            catch (Exception ex)
            {
                Log.Error("Settings.Save", ex);
            }
        }

        private static int Int(Dictionary<string, object> map, string key, int fallback)
        {
            object value;
            if (!map.TryGetValue(key, out value) || value == null) return fallback;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool Bool(Dictionary<string, object> map, string key, bool fallback)
        {
            object value;
            if (!map.TryGetValue(key, out value) || value == null) return fallback;
            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
