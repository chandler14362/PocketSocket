using System;
using System.Collections.Generic;
using System.Linq;
using PocketSocket.Abstractions;
using PocketSocket.Abstractions.Extensions;
using PocketSocket.Abstractions.Models;
using PocketSocket.Models;

namespace PocketSocket.Extensions
{
    public static class TypedSocketModelExtensions
    {
        public static IEnumerable<T> GetModelsApplicableToType<T>(this IEnumerable<T> models, Type type)
            where T: TypedSocketModel =>
            models.Where(m => m.TypeIsApplicable(type));

        public static IEnumerable<MessageModel> GetMessageModels(this IEnumerable<TypedSocketModel> models) =>
            models.SelectMany(m => m.GetMessageModels());
    }
}