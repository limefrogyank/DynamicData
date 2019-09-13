using Newtonsoft.Json;
using Serialize.Linq.Interfaces;
using Serialize.Linq.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR.Core
{
    public class NSoftJsonSerializer : IJsonSerializer
    {
        private readonly HashSet<Type> _customKnownTypes;
        private bool _autoAddKnownTypesAsArrayTypes;
        private bool _autoAddKnownTypesAsListTypes;
        private IEnumerable<Type> _knownTypesExploded;

        public bool AutoAddKnownTypesAsArrayTypes
        {
            get => _autoAddKnownTypesAsArrayTypes;
            set
            {
                _autoAddKnownTypesAsArrayTypes = value;
                if (value)
                    _autoAddKnownTypesAsListTypes = false;
                _knownTypesExploded = null;
            }
        }

        public bool AutoAddKnownTypesAsListTypes
        {
            get => _autoAddKnownTypesAsListTypes;
            set
            {
                _autoAddKnownTypesAsListTypes = value;
                if (value)
                    _autoAddKnownTypesAsArrayTypes = false;
                _knownTypesExploded = null;
            }
        }

        public void AddKnownType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _customKnownTypes.Add(type);
            _knownTypesExploded = null;
        }

        public void AddKnownTypes(IEnumerable<Type> types)
        {
            if (types == null)
                throw new ArgumentNullException(nameof(types));

            foreach (var type in types)
                AddKnownType(type);
        }

        public T Deserialize<T>(string text) where T : Node
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new StreamWriter(ms))
                {
                    writer.Write(text);
                    writer.Flush();

                    ms.Position = 0;
                    return Deserialize<T>(ms);
                }
            }
        }

        private DataContractJsonSerializer CreateDataContractJsonSerializer(Type type)
        {
            return new DataContractJsonSerializer(type);//, this.GetKnownTypes());
        }

        public T Deserialize<T>(Stream stream) where T : Node
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var serializer = CreateDataContractJsonSerializer(typeof(T));
            return (T)serializer.ReadObject(stream);
        }

        public string Serialize<T>(T obj) where T : Node
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    Serialize(ms, obj);

                    ms.Position = 0;
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                        return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error converting type: " + ex.Message, ex);
            }
        }

        public void Serialize<T>(Stream stream, T obj) where T : Node
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var serializer = CreateDataContractJsonSerializer(typeof(T));
            serializer.WriteObject(stream, obj);
        }

        protected virtual IEnumerable<Type> GetKnownTypes()
        {
            if (_knownTypesExploded != null)
                return _knownTypesExploded;

            _knownTypesExploded = ExplodeKnownTypes(KnownTypes.All)
                .Concat(ExplodeKnownTypes(_customKnownTypes)).ToList();
            return _knownTypesExploded;
        }

        private IEnumerable<Type> ExplodeKnownTypes(IEnumerable<Type> types)
        {
            return KnownTypes.Explode(
                types, AutoAddKnownTypesAsArrayTypes, AutoAddKnownTypesAsListTypes);
        }
    }

    public class DataContractJsonSerializer
    {
        private Type type;
        private JsonSerializer js;

        public DataContractJsonSerializer(Type t)
        {
            type = t;
            js = new JsonSerializer();
            js.TypeNameHandling = TypeNameHandling.All;
            //js.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
        }

        public object ReadObject(Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            return js.Deserialize(reader, type);
        }

        public void WriteObject(Stream stream, object o)
        {
            StreamWriter writer = new StreamWriter(stream);
            js.Serialize(writer, o);
            writer.Flush();
        }
    }

    internal static class KnownTypes
    {
        public static readonly Type[] All =
        {
            typeof(bool),
            typeof(decimal), typeof(double),
            typeof(float),
            typeof(int), typeof(uint),
            typeof(short), typeof(ushort),
            typeof(long), typeof(ulong),
            typeof(string),
            typeof(DateTime), typeof(DateTimeOffset),
            typeof(TimeSpan), typeof(Guid),
            typeof(DayOfWeek), typeof(DateTimeKind),
            typeof(Enum)
        };

        private static readonly HashSet<Type> _allExploded = new HashSet<Type>(Explode(All, true, true));

        public static bool Match(Type type) =>
            type != null && (_allExploded.Contains(type) || _allExploded.Any(t => t.IsAssignableFrom(type)));

        public static IEnumerable<Type> Explode(IEnumerable<Type> types, bool includeArrayTypes, bool includeListTypes)
        {
            foreach (var type in types)
            {
                yield return type;
                if (includeArrayTypes)
                    yield return type.MakeArrayType();
                if (includeListTypes)
                    yield return typeof(List<>).MakeGenericType(type);

                if (type.GetTypeInfo().IsClass)
                    continue;

                var nullableType = typeof(Nullable<>).MakeGenericType(type);
                yield return nullableType;
                if (includeArrayTypes)
                    yield return nullableType.MakeArrayType();
                if (includeListTypes)
                    yield return typeof(List<>).MakeGenericType(nullableType);
            }
        }
    }
}
