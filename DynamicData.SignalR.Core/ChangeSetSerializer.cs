using DynamicData.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace DynamicData.SignalR.Core
{
    public class CustomContractResolver : DefaultContractResolver
    {
        public override JsonContract ResolveContract(Type type)
        {
            return base.ResolveContract(type);
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            JsonContract contract = base.CreateContract(objectType);


            if (objectType.IsGenericType)
            {
                if (objectType.GetGenericTypeDefinition() == typeof(Change<>))
                {
                    var types = objectType.GetGenericArguments();
                    var changeConverterType = typeof(ChangeConverter<,>).MakeGenericType(types);
                    var changeConverter = Activator.CreateInstance(changeConverterType);
                    contract.Converter = (JsonConverter)changeConverter;
                }
                else if (objectType.GetGenericTypeDefinition() == typeof(ChangeSet<>))
                {
                    var types = objectType.GetGenericArguments();
                    var changeSetConverterType = typeof(ChangeSetConverter<,>).MakeGenericType(types);
                    var changeSetConverter = Activator.CreateInstance(changeSetConverterType);
                    contract.Converter = (JsonConverter)changeSetConverter;
                }
            }


            return contract;
        }
    }


    public class ChangeSetConverter<TObject, TKey> : JsonConverter<ChangeSet<TObject, TKey>>
    {
        public override void WriteJson(JsonWriter writer, ChangeSet<TObject, TKey> value, JsonSerializer serializer)
        {
            
            writer.WriteStartObject();

            writer.WritePropertyName("ChangeSetContents");
            var changeConverter = new ChangeConverter<TObject, TKey>();
            var serializedChanges = JsonConvert.SerializeObject(value.ToList(), changeConverter);

            writer.WriteRawValue(serializedChanges);

            writer.WriteEndObject();
        }

        public override ChangeSet<TObject, TKey> ReadJson(JsonReader reader, Type objectType, ChangeSet<TObject, TKey> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);

            serializer.Converters.Add(new ChangeConverter<TObject, TKey>());
            var changeSetContents = jsonObject["ChangeSetContents"].ToObject<List<Change<TObject, TKey>>>(serializer);

            var changeSet = new ChangeSet<TObject, TKey>(changeSetContents);
            return changeSet;
        }

        public override bool CanRead => true;

        public override bool CanWrite => true;
    }


    public class ChangeConverter<TObject, TKey> : JsonConverter<Change<TObject, TKey>>
    {
        public override void WriteJson(JsonWriter writer, Change<TObject, TKey> value, JsonSerializer serializer)
        {
            var genArgs = value.GetType().GetGenericArguments();
            writer.WriteStartObject();

            writer.WritePropertyName("Current");
            serializer.Serialize(writer, value.Current);
            writer.WritePropertyName("CurrentIndex");
            serializer.Serialize(writer, value.CurrentIndex);
            writer.WritePropertyName("Key");
            serializer.Serialize(writer, value.Key);

            if (value.Previous.HasValue)
            {
                writer.WritePropertyName("Previous");
                serializer.Serialize(writer, value.Previous.Value);
            }

            writer.WritePropertyName("PreviousIndex");
            serializer.Serialize(writer, value.PreviousIndex);
            writer.WritePropertyName("Reason");
            serializer.Serialize(writer, value.Reason);
            writer.WriteEndObject();
        }

        public override Change<TObject, TKey> ReadJson(JsonReader reader, Type objectType, Change<TObject, TKey> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var changeReason = JsonConvert.DeserializeObject<ChangeReason>((string)jsonObject["Reason"]);
            if (changeReason == ChangeReason.Moved)
            {
                return new Change<TObject, TKey>(
                  JsonConvert.DeserializeObject<TKey>((string)jsonObject["Key"]),
                  jsonObject["Current"].ToObject<TObject>(serializer),
                  //JsonConvert.DeserializeObject<TObject>((string)jsonObject["Current"]),
                  JsonConvert.DeserializeObject<int>((string)jsonObject["CurrentIndex"]),
                  JsonConvert.DeserializeObject<int>((string)jsonObject["PreviousIndex"])
                  );
            }
            else if (changeReason == ChangeReason.Update)
            {
                return new Change<TObject, TKey>(
                  changeReason,
                  jsonObject["Key"].ToObject<TKey>(serializer),
                  jsonObject["Current"].ToObject<TObject>(serializer),
                  Optional.Some(jsonObject["Previous"].ToObject<TObject>(serializer))
                  );
            }
            else
            {
                return new Change<TObject, TKey>(
                  changeReason,
                  jsonObject["Key"].ToObject<TKey>(serializer),
                  jsonObject["Current"].ToObject<TObject>(serializer),
                  (int)jsonObject["CurrentIndex"]
                  );
            }
        }

        public override bool CanRead => true;

        public override bool CanWrite => true;
    }
}
