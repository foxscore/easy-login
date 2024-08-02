using System;
using System.Diagnostics.CodeAnalysis;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509.Qualified;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Foxscore.EasyLogin.Extensions
{
    public static class JObjectExtensions
    {
        public static bool TryGetPropertyValue<T>(this JObject jObject, string name, [MaybeNullWhen(false)] out T value)
        {
            var property = jObject.Property(name);
            if (property == null)
            {
                value = default;
                return false;
            }

            JTokenType type;
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Object:
                    if (typeof(T) == typeof(JArray))
                    {
                        type = JTokenType.Array;
                    }
                    else
                    {
                        value = default;
                        return false;
                    }
                    break;
                case TypeCode.String:
                    type = JTokenType.String;
                    break;
                case TypeCode.Boolean:
                    type = JTokenType.Boolean;
                    break;
                default:
                    value = default;
                    return false;
            }

            if (property.Value.Type != type)
            {
                value = default;
                return false;
            }

            value = property.Value.Value<T>();
            return true;
        }
    }
}