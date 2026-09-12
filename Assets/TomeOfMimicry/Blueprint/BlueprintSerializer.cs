using System;
using System.Collections.Generic;
using System.Text;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace TomeOfMimicry
{
    public static class BlueprintSerializer
    {
        private const string IndexKey  = "BG_INDEX";
        private const string BpPrefix  = "BG_BP_";


        public static bool Save(Blueprint bp)
        {
            if (bp == null) return false;
            try
            {
                NormalizeMetadata(bp);
                bp.modifiedUtc = DateTime.UtcNow.ToString("O");
                string safeName = SanitizeName(bp.name);
                string json     = Serialize(bp);
                var roundTrip   = Deserialize(json);
                if (!Matches(bp, roundTrip))
                {
                    Debug.LogError($"[Tome of Mimicry] Save validation failed for '{safeName}'");
                    return false;
                }
                PlayerPrefs.SetString(BpPrefix + safeName, json);

                var names = ReadIndex();
                if (!names.Contains(safeName))
                {
                    names.Insert(0, safeName);
                    WriteIndex(names);
                }

                PlayerPrefs.Save();
                Debug.Log($"[Tome of Mimicry] Saved blueprint '{safeName}' to PlayerPrefs");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Tome of Mimicry] Save failed: {ex.Message}");
                return false;
            }
        }

        public static Blueprint Load(string name)
        {
            string safeName = SanitizeName(name);
            string json     = PlayerPrefs.GetString(BpPrefix + safeName, null);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"[Tome of Mimicry] Blueprint '{safeName}' not found in PlayerPrefs");
                return null;
            }
            try
            {
                var bp = Deserialize(json);
                NormalizeMetadata(bp);
                if (!IsValid(bp))
                {
                    Debug.LogError($"[Tome of Mimicry] Blueprint '{safeName}' contains invalid data");
                    return null;
                }
                GadgetLog.Trace($"[Tome of Mimicry] Loaded blueprint '{bp.name}' ({bp.tiles.Count} tiles)");
                return bp;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Tome of Mimicry] Load failed for '{safeName}': {ex.Message}");
                return null;
            }
        }

        public static bool Delete(string name)
        {
            string safeName = SanitizeName(name);
            if (!PlayerPrefs.HasKey(BpPrefix + safeName)) return false;
            PlayerPrefs.DeleteKey(BpPrefix + safeName);

            var names = ReadIndex();
            names.Remove(safeName);
            WriteIndex(names);

            PlayerPrefs.Save();
            return true;
        }

        public static List<string> ListSaved() => ReadIndex();

        public static string Export(Blueprint bp)
        {
            if (bp == null) return null;
            NormalizeMetadata(bp);
            return "CKBP1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialize(bp)));
        }

        public static Blueprint Import(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded) || !encoded.StartsWith("CKBP1:"))
                return null;

            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Substring(6)));
                var bp = Deserialize(json);
                NormalizeMetadata(bp);
                return IsValid(bp) ? bp : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tome of Mimicry] Blueprint import failed: {ex.Message}");
                return null;
            }
        }

        private static bool IsValid(Blueprint bp)
        {
            return bp != null && bp.width > 0 && bp.height > 0 &&
                   bp.tiles != null && bp.objects != null;
        }

        private static bool Matches(Blueprint source, Blueprint roundTrip)
        {
            return IsValid(roundTrip) &&
                   source.width == roundTrip.width &&
                   source.height == roundTrip.height &&
                   source.tiles.Count == roundTrip.tiles.Count &&
                   source.objects.Count == roundTrip.objects.Count;
        }

        private static void NormalizeMetadata(Blueprint bp)
        {
            if (bp == null) return;
            if (bp.schemaVersion <= 0) bp.schemaVersion = Blueprint.CurrentSchemaVersion;
            if (string.IsNullOrEmpty(bp.id)) bp.id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(bp.createdUtc)) bp.createdUtc = DateTime.UtcNow.ToString("O");
            if (string.IsNullOrEmpty(bp.modifiedUtc)) bp.modifiedUtc = bp.createdUtc;
            bp.description ??= "";
            bp.tags ??= new List<string>();
        }


        private static List<string> ReadIndex()
        {
            string raw = PlayerPrefs.GetString(IndexKey, "");
            var list   = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;

            foreach (var part in raw.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) list.Add(trimmed);
            }
            return list;
        }

        private static void WriteIndex(List<string> names)
        {
            PlayerPrefs.SetString(IndexKey, string.Join(",", names.ToArray()));
        }


        private static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "blueprint";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (c < 128 && (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.'))
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.Length > 0 ? sb.ToString() : "blueprint";
        }


        public static string Serialize(Blueprint bp)
        {
            var sb = new StringBuilder();
            sb.Append("{\"schema\":"); sb.Append(bp.schemaVersion);
            sb.Append(",\"id\":"); sb.Append(JsonStr(bp.id ?? ""));
            sb.Append(",\"name\":");  sb.Append(JsonStr(bp.name));
            sb.Append(",\"description\":"); sb.Append(JsonStr(bp.description ?? ""));
            sb.Append(",\"createdUtc\":"); sb.Append(JsonStr(bp.createdUtc ?? ""));
            sb.Append(",\"modifiedUtc\":"); sb.Append(JsonStr(bp.modifiedUtc ?? ""));
            sb.Append(",\"gameVersion\":"); sb.Append(JsonStr(bp.sourceGameVersion ?? ""));
            sb.Append(",\"tags\":"); sb.Append(JsonStr(string.Join("|", bp.tags ?? new List<string>())));
            sb.Append(",\"unsupported\":"); sb.Append(bp.unsupportedObjectCount);
            sb.Append(",\"width\":"); sb.Append(bp.width);
            sb.Append(",\"height\":"); sb.Append(bp.height);
            sb.Append(",\"tiles\":[");

            for (int i = 0; i < bp.tiles.Count; i++)
            {
                var t = bp.tiles[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"rx\":");    sb.Append(t.relativePos.x);
                sb.Append(",\"ry\":");   sb.Append(t.relativePos.y);
                sb.Append(",\"rz\":");   sb.Append(t.relativePos.z);
                if (t.hasWall)
                {
                    sb.Append(",\"hasWall\":true");
                    sb.Append(",\"wallTs\":"); sb.Append(t.wallTileType);
                }
                if (t.hasFloor)
                {
                    sb.Append(",\"hasFloor\":true");
                    sb.Append(",\"floorTs\":"); sb.Append(t.floorTileType);
                    sb.Append(",\"floorKind\":"); sb.Append((int)t.floorTileKind);
                }
                if (t.hasGround)
                {
                    sb.Append(",\"hasGround\":true");
                    sb.Append(",\"groundTs\":"); sb.Append(t.groundTileType);
                }
                if (t.hasWire)
                {
                    sb.Append(",\"hasWire\":true");
                    sb.Append(",\"wireTs\":"); sb.Append(t.wireTileset);
                }
                if (t.hasRail)
                {
                    sb.Append(",\"hasRail\":true");
                    sb.Append(",\"railTs\":"); sb.Append(t.railTileset);
                }
                if (t.extras != null && t.extras.Count > 0)
                {
                    sb.Append(",\"ex\":[");
                    for (int e = 0; e < t.extras.Count; e++)
                    {
                        if (e > 0) sb.Append(',');
                        sb.Append(t.extras[e].x); sb.Append(','); sb.Append(t.extras[e].y);
                    }
                    sb.Append(']');
                }
                sb.Append('}');
            }

            sb.Append("],\"objects\":[");
            for (int i = 0; i < bp.objects.Count; i++)
            {
                var o = bp.objects[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"rx\":"); sb.Append(o.relativePos.x);
                sb.Append(",\"ry\":"); sb.Append(o.relativePos.y);
                sb.Append(",\"oid\":"); sb.Append((int)o.objectId);
                sb.Append(",\"var\":"); sb.Append(o.variation);
                sb.Append(",\"tsx\":"); sb.Append(o.tileSize.x);
                sb.Append(",\"tsz\":"); sb.Append(o.tileSize.y);
                sb.Append(",\"dx\":"); sb.Append(o.direction.x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"dy\":"); sb.Append(o.direction.y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"dz\":"); sb.Append(o.direction.z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"dbv\":"); sb.Append(Bool(o.dirByVariation));
                sb.Append(",\"dvx\":"); sb.Append(o.dbvDirection.x);
                sb.Append(",\"dvz\":"); sb.Append(o.dbvDirection.y);
                if (o.hasPaint)
                {
                    sb.Append(",\"pc\":"); sb.Append(o.paintColor);
                }
                if (o.hasObjectFilter)
                {
                    sb.Append(",\"oft\":"); sb.Append(o.objectFilterType);
                    sb.Append(",\"ofo\":"); sb.Append((int)o.objectFilterObject);
                    sb.Append(",\"ofv\":"); sb.Append(o.objectFilterVariation);
                }
                if (o.hasMoverFilter)
                {
                    sb.Append(",\"mft\":"); sb.Append(o.moverFilterType);
                    sb.Append(",\"mfo\":"); sb.Append((int)o.moverFilterObject);
                    sb.Append(",\"mfv\":"); sb.Append(o.moverFilterVariation);
                    sb.Append(",\"mfc\":"); sb.Append(o.moverFilterCategory);
                }
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static Blueprint Deserialize(string json)
        {
            var p = new JsonParser(json);
            p.Expect('{');

            string id = null, name = "Blueprint", description = "";
            string createdUtc = null, modifiedUtc = null, gameVersion = null, tags = "";
            int schema = Blueprint.CurrentSchemaVersion, unsupported = 0;
            int width = 1, height = 1;
            var tiles   = new List<BlueprintTile>();
            var objects = new List<BlueprintObject>();

            while (!p.Peek('}'))
            {
                string key = p.ReadString();
                p.Expect(':');
                switch (key)
                {
                    case "schema": schema = p.ReadInt(); break;
                    case "id": id = p.ReadString(); break;
                    case "name":   name   = p.ReadString(); break;
                    case "description": description = p.ReadString(); break;
                    case "createdUtc": createdUtc = p.ReadString(); break;
                    case "modifiedUtc": modifiedUtc = p.ReadString(); break;
                    case "gameVersion": gameVersion = p.ReadString(); break;
                    case "tags": tags = p.ReadString(); break;
                    case "unsupported": unsupported = p.ReadInt(); break;
                    case "width":  width  = p.ReadInt();    break;
                    case "height": height = p.ReadInt();    break;
                    case "tiles":
                        p.Expect('[');
                        while (!p.Peek(']'))
                        {
                            tiles.Add(ReadTile(p));
                            p.TryConsume(',');
                        }
                        p.Expect(']');
                        break;
                    case "objects":
                        p.Expect('[');
                        while (!p.Peek(']'))
                        {
                            objects.Add(ReadObject(p));
                            p.TryConsume(',');
                        }
                        p.Expect(']');
                        break;
                    default: p.SkipValue(); break;
                }
                p.TryConsume(',');
            }
            p.Expect('}');

            var bp = new Blueprint(name, width, height);
            bp.schemaVersion = schema;
            bp.id = id;
            bp.description = description;
            bp.createdUtc = createdUtc;
            bp.modifiedUtc = modifiedUtc;
            bp.sourceGameVersion = gameVersion;
            if (!string.IsNullOrEmpty(tags))
                bp.tags.AddRange(tags.Split('|'));
            bp.unsupportedObjectCount = unsupported;
            bp.tiles.AddRange(tiles);
            bp.objects.AddRange(objects);
            NormalizeMetadata(bp);
            return bp;
        }

        private static BlueprintObject ReadObject(JsonParser p)
        {
            p.Expect('{');
            int rx = 0, ry = 0;
            ObjectID oid = ObjectID.None;
            int variation = 0;
            float dx = 0f, dy = 0f, dz = 0f;
            bool dbv = false;
            int dvx = 0, dvz = 0;
            bool hasPaint = false;
            int paintColor = 0;
            bool hasObjectFilter = false, hasMoverFilter = false;
            int objectFilterType = 0, objectFilterVariation = 0;
            ObjectID objectFilterObject = ObjectID.None;
            int moverFilterType = 0, moverFilterVariation = 0, moverFilterCategory = 0;
            ObjectID moverFilterObject = ObjectID.None;

            int tsx = 1, tsz = 1;
            while (!p.Peek('}'))
            {
                string key = p.ReadString();
                p.Expect(':');
                switch (key)
                {
                    case "rx":  rx        = p.ReadInt();           break;
                    case "ry":  ry        = p.ReadInt();           break;
                    case "oid": oid       = (ObjectID)p.ReadInt(); break;
                    case "var": variation = p.ReadInt();           break;
                    case "tsx": tsx       = p.ReadInt();           break;
                    case "tsz": tsz       = p.ReadInt();           break;
                    case "dx":  dx        = p.ReadFloat();         break;
                    case "dy":  dy        = p.ReadFloat();         break;
                    case "dz":  dz        = p.ReadFloat();         break;
                    case "dbv": dbv       = p.ReadBool();          break;
                    case "dvx": dvx       = p.ReadInt();           break;
                    case "dvz": dvz       = p.ReadInt();           break;
                    case "pc":  hasPaint  = true; paintColor = p.ReadInt(); break;
                    case "oft": hasObjectFilter = true; objectFilterType = p.ReadInt(); break;
                    case "ofo": hasObjectFilter = true; objectFilterObject = (ObjectID)p.ReadInt(); break;
                    case "ofv": hasObjectFilter = true; objectFilterVariation = p.ReadInt(); break;
                    case "mft": hasMoverFilter = true; moverFilterType = p.ReadInt(); break;
                    case "mfo": hasMoverFilter = true; moverFilterObject = (ObjectID)p.ReadInt(); break;
                    case "mfv": hasMoverFilter = true; moverFilterVariation = p.ReadInt(); break;
                    case "mfc": hasMoverFilter = true; moverFilterCategory = p.ReadInt(); break;
                    default:    p.SkipValue();                     break;
                }
                p.TryConsume(',');
            }
            p.Expect('}');

            return new BlueprintObject
            {
                relativePos    = new Unity.Mathematics.int2(rx, ry),
                objectId       = oid,
                variation      = variation,
                tileSize       = new Unity.Mathematics.int2(tsx, tsz),
                direction      = new Unity.Mathematics.float3(dx, dy, dz),
                dirByVariation = dbv,
                dbvDirection   = new Unity.Mathematics.int2(dvx, dvz),
                hasPaint       = hasPaint,
                paintColor     = paintColor,
                hasObjectFilter       = hasObjectFilter,
                objectFilterType      = objectFilterType,
                objectFilterObject    = objectFilterObject,
                objectFilterVariation = objectFilterVariation,
                hasMoverFilter        = hasMoverFilter,
                moverFilterType       = moverFilterType,
                moverFilterObject     = moverFilterObject,
                moverFilterVariation  = moverFilterVariation,
                moverFilterCategory   = moverFilterCategory,
            };
        }

        private static BlueprintTile ReadTile(JsonParser p)
        {
            p.Expect('{');
            var t = new BlueprintTile();
            int rx = 0, ry = 0, rz = 0;

            while (!p.Peek('}'))
            {
                string key = p.ReadString();
                p.Expect(':');
                switch (key)
                {
                    case "rx":        rx               = p.ReadInt();           break;
                    case "ry":        ry               = p.ReadInt();           break;
                    case "rz":        rz               = p.ReadInt();           break;
                    case "hasWall":   t.hasWall        = p.ReadBool();          break;
                    case "wallTs":    t.wallTileType   = p.ReadInt();           break;
                    case "hasFloor":  t.hasFloor       = p.ReadBool();          break;
                    case "floorTs":   t.floorTileType  = p.ReadInt();           break;
                    case "floorKind": t.floorTileKind  = (TileType)p.ReadInt(); break;
                    case "hasGround": t.hasGround      = p.ReadBool();          break;
                    case "groundTs":  t.groundTileType = p.ReadInt();           break;
                    case "hasWire":   t.hasWire        = p.ReadBool();          break;
                    case "wireTs":    t.wireTileset    = p.ReadInt();           break;
                    case "hasRail":   t.hasRail        = p.ReadBool();          break;
                    case "railTs":    t.railTileset    = p.ReadInt();           break;
                    case "ex":
                        p.Expect('[');
                        while (!p.Peek(']'))
                        {
                            int exType = p.ReadInt();
                            p.TryConsume(',');
                            int exTs = p.ReadInt();
                            p.TryConsume(',');
                            t.extras ??= new List<Unity.Mathematics.int2>();
                            t.extras.Add(new Unity.Mathematics.int2(exType, exTs));
                        }
                        p.Expect(']');
                        break;
                    default: p.SkipValue(); break;
                }
                p.TryConsume(',');
            }
            p.Expect('}');

            t.relativePos = new int3(rx, ry, rz);

            if (t.hasFloor && t.floorTileKind == TileType.rail)
            {
                t.hasFloor = false;
                if (!t.hasRail)
                {
                    t.hasRail = true;
                    t.railTileset = t.floorTileType;
                }
                t.floorTileKind = TileType.floor;
                t.floorTileType = 0;
            }
            return t;
        }

        private static string JsonStr(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                if      (c == '"')  sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else                sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string Bool(bool b) => b ? "true" : "false";


        private class JsonParser
        {
            private readonly string _s;
            private int _i;

            public JsonParser(string s) { _s = s; }

            private void Skip()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' ||
                                          _s[_i] == '\n' || _s[_i] == '\r'))
                    _i++;
            }

            public bool Peek(char c) { Skip(); return _i < _s.Length && _s[_i] == c; }

            public void Expect(char c)
            {
                Skip();
                if (_i >= _s.Length || _s[_i] != c)
                    throw new Exception($"Expected '{c}' at {_i}");
                _i++;
            }

            public bool TryConsume(char c)
            {
                Skip();
                if (_i < _s.Length && _s[_i] == c) { _i++; return true; }
                return false;
            }

            public string ReadString()
            {
                Skip(); Expect('"');
                var sb = new StringBuilder();
                while (_i < _s.Length && _s[_i] != '"')
                {
                    if (_s[_i] == '\\' && _i + 1 < _s.Length)
                    {
                        _i++;
                        sb.Append(_s[_i] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => _s[_i] });
                    }
                    else sb.Append(_s[_i]);
                    _i++;
                }
                Expect('"');
                return sb.ToString();
            }

            public int ReadInt()
            {
                Skip();
                int sign = 1;
                if (_i < _s.Length && _s[_i] == '-') { sign = -1; _i++; }
                int v = 0;
                while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9')
                { v = v * 10 + (_s[_i] - '0'); _i++; }
                return v * sign;
            }

            public bool ReadBool()
            {
                Skip();
                if (_s.Length - _i >= 4 && _s.Substring(_i, 4) == "true")  { _i += 4; return true; }
                if (_s.Length - _i >= 5 && _s.Substring(_i, 5) == "false") { _i += 5; return false; }
                throw new Exception($"Expected bool at {_i}");
            }

            public float ReadFloat()
            {
                Skip();
                float sign = 1f;
                if (_i < _s.Length && _s[_i] == '-') { sign = -1f; _i++; }
                float v = 0f;
                while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9')
                { v = v * 10f + (_s[_i] - '0'); _i++; }
                if (_i < _s.Length && _s[_i] == '.')
                {
                    _i++;
                    float scale = 0.1f;
                    while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9')
                    { v += (_s[_i] - '0') * scale; scale *= 0.1f; _i++; }
                }
                return v * sign;
            }

            public void SkipValue()
            {
                Skip();
                if (_i >= _s.Length) return;
                char c = _s[_i];
                if (c == '"') { ReadString(); return; }
                if (c == '{') { SkipObject(); return; }
                if (c == '[') { SkipArray();  return; }
                while (_i < _s.Length && _s[_i] != ',' && _s[_i] != '}' && _s[_i] != ']') _i++;
            }

            private void SkipObject()
            {
                Expect('{');
                while (!Peek('}')) { SkipValue(); TryConsume(':'); SkipValue(); TryConsume(','); }
                Expect('}');
            }

            private void SkipArray()
            {
                Expect('[');
                while (!Peek(']')) { SkipValue(); TryConsume(','); }
                Expect(']');
            }
        }
    }
}

