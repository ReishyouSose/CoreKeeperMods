using Assets.BuildingBlueprint.Scripts.Core;
using Assets.BuildingBlueprint.Scripts.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    [Serializable]
    public struct PlaceCommand
    {
        public PlaceOperator Op;
        public int Num1;
        public int Num2;
        public int Num3;
        public int Num4;
        public class Converter : JsonConverter<PlaceCommand>
        {
            public override void WriteJson(JsonWriter writer, PlaceCommand value, JsonSerializer serializer)
            {
                var (n1, n2, n3, n4) = IPlaceCommand.GetNames(value.Op);
                writer.WriteStartObject();
                writer.WritePropertyName("op");
                writer.WriteValue(value.Op.ToString());
                if (!string.IsNullOrEmpty(n1))
                { writer.WritePropertyName(n1); writer.WriteValue(value.Num1); }
                if (!string.IsNullOrEmpty(n2))
                { writer.WritePropertyName(n2); writer.WriteValue(value.Num2); }
                if (!string.IsNullOrEmpty(n3))
                { writer.WritePropertyName(n3); writer.WriteValue(value.Num3); }
                if (!string.IsNullOrEmpty(n4))
                { writer.WritePropertyName(n4); writer.WriteValue(value.Num4); }
                writer.WriteEndObject();
            }

            public override PlaceCommand ReadJson(JsonReader reader, Type objectType, PlaceCommand existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                var jo = JObject.Load(reader);
                var opToken = jo["op"] ?? throw new JsonSerializationException("PlaceCommand missing 'op' field");
                var op = Enum.Parse<PlaceOperator>((string)opToken, true);
                var (n1, n2, n3, n4) = IPlaceCommand.GetNames(op);
                var rpc = new PlaceCommand { Op = op };
                if (n1 != null && jo.TryGetValue(n1, out var v1))
                    rpc.Num1 = (int)v1;
                if (n2 != null && jo.TryGetValue(n2, out var v2))
                    rpc.Num2 = (int)v2;
                if (n3 != null && jo.TryGetValue(n3, out var v3))
                    rpc.Num3 = (int)v3;
                if (n4 != null && jo.TryGetValue(n4, out var v4))
                    rpc.Num4 = (int)v4;
                return rpc;
            }
        }
        public readonly T ReadAs<T>() where T : IPlaceCommand => IPlaceCommand.ReadCommand<T>(this);
    }
}