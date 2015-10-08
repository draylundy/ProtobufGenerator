﻿using System;
#if DNXCORE50
using System.Globalization;
#endif

namespace ProtobufCompiler.Types
{
    internal class Map : IEquatable<Map>
    {
        internal string Name { get; }
        internal int FieldNum { get; }
        internal string KeyType { get; }
        internal string ValueType { get; }

#if DNXCORE50
        internal StringComparer InvCultIc = CultureInfo.InvariantCulture.CompareInfo.GetStringComparer(CompareOptions.None);
#else
        internal StringComparer InvCultIc = StringComparer.InvariantCultureIgnoreCase;
#endif

        internal Map(string name, int fieldNum, string keyType, string valueType)
        {
            Name = name;
            FieldNum = fieldNum;
            KeyType = keyType;
            ValueType = valueType;
        }

        public bool Equals(Map other)
        {
            if (other == null) return false;
            return InvCultIc.Equals(Name, other.Name) &&
                   FieldNum.Equals(other.FieldNum) &&
                   InvCultIc.Equals(KeyType, other.KeyType) &&
                   InvCultIc.Equals(ValueType, other.ValueType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals(obj as Map);
        }

        public override int GetHashCode()
        {
            var hash = 13;
            hash = (hash*7) + Name.GetHashCode();
            hash = (hash*7) + FieldNum.GetHashCode();
            hash = (hash*7) + KeyType.GetHashCode();
            hash = (hash*7) + ValueType.GetHashCode();
            return hash;
        }
    }
}
