using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ClipPeek
{
    internal sealed class HistoryStore
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly string _path;

        internal readonly List<ClipItem> Items = new List<ClipItem>();
        internal int MaxItems = 200;

        internal string Path_
        {
            get { return _path; }
        }

        internal HistoryStore(string file)
        {
            _path = file;
            Serializer.MaxJsonLength = 64 * 1024 * 1024;
            Load();
        }

        internal bool Add(ClipItem item)
        {
            if (item == null) return false;

            string signature = item.Signature;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i].Signature == signature) Items.RemoveAt(i);
            }

            Items.Insert(0, item);

            while (Items.Count > MaxItems) Items.RemoveAt(Items.Count - 1);

            Save();
            return true;
        }

        internal void RemoveAt(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            Items.RemoveAt(index);
            Save();
        }

        internal void Clear()
        {
            Items.Clear();
            Save();
        }

        internal void Save()
        {
            try
            {
                List<object> rows = new List<object>();
                foreach (ClipItem item in Items)
                {
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    row["k"] = (int)item.Kind;
                    row["t"] = item.Text;
                    row["c"] = item.Chars;
                    row["w"] = item.ImgW;
                    row["h"] = item.ImgH;
                    row["b"] = item.ImgBytes;
                    row["f"] = item.Files;
                    row["ts"] = item.Time.ToString("o");
                    row["cut"] = item.Truncated;
                    rows.Add(row);
                }

                Dictionary<string, object> root = new Dictionary<string, object>();
                root["items"] = rows;
                File.WriteAllText(_path, Serializer.Serialize(root));
                Program.Trace("history saved: " + _path + " items=" + Items.Count);
            }
            catch (Exception ex)
            {
                Program.Trace("history save FAILED: " + _path + " " + ex.GetType().Name + ": " + ex.Message);
                Log.Error("HistoryStore.Save", ex);
            }
        }

        internal void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;

                Dictionary<string, object> root =
                    Serializer.DeserializeObject(File.ReadAllText(_path)) as Dictionary<string, object>;
                if (root == null) return;

                object rowsObject;
                if (!root.TryGetValue("items", out rowsObject)) return;

                object[] rows = rowsObject as object[];
                if (rows == null) return;

                foreach (object rowObject in rows)
                {
                    Dictionary<string, object> row = rowObject as Dictionary<string, object>;
                    if (row == null) continue;

                    ClipItem item = new ClipItem();
                    item.Kind = (ClipKind)ToInt(row, "k");
                    item.Text = Str(row, "t");
                    item.Chars = ToInt(row, "c");
                    item.ImgW = ToInt(row, "w");
                    item.ImgH = ToInt(row, "h");
                    item.ImgBytes = ToLong(row, "b");
                    item.Truncated = ToBool(row, "cut");

                    object filesObject;
                    if (row.TryGetValue("f", out filesObject) && filesObject is object[])
                    {
                        object[] raw = (object[])filesObject;
                        string[] files = new string[raw.Length];
                        for (int i = 0; i < raw.Length; i++) files[i] = Convert.ToString(raw[i]);
                        item.Files = files;
                    }

                    DateTime time;
                    if (!DateTime.TryParse(Str(row, "ts"), out time)) time = DateTime.Now;
                    item.Time = time;

                    Items.Add(item);
                }

                while (Items.Count > MaxItems) Items.RemoveAt(Items.Count - 1);
            }
            catch (Exception ex)
            {
                Log.Error("HistoryStore.Load", ex);
            }
        }

        private static int ToInt(Dictionary<string, object> map, string key)
        {
            object value;
            if (!map.TryGetValue(key, out value) || value == null) return 0;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static long ToLong(Dictionary<string, object> map, string key)
        {
            object value;
            if (!map.TryGetValue(key, out value) || value == null) return 0;
            try
            {
                return Convert.ToInt64(value);
            }
            catch
            {
                return 0;
            }
        }

        private static string Str(Dictionary<string, object> map, string key)
        {
            object value;
            if (!map.TryGetValue(key, out value) || value == null) return null;
            return Convert.ToString(value);
        }

        private static bool ToBool(Dictionary<string, object> map, string key)
        {
            object value;
            if (!map.TryGetValue(key, out value) || value == null) return false;
            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return false;
            }
        }
    }
}
