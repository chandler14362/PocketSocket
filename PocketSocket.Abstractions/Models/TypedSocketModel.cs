using System;
using System.Collections.Generic;
using System.Linq;
using PocketSocket.Models;

namespace PocketSocket.Abstractions.Models
{
    public abstract record TypedSocketModel(Type InterfaceType)
    {
        public abstract IEnumerable<MessageModel> GetMessageModels();

        public virtual bool TypeIsApplicable(Type type)
        {
            return type == InterfaceType || type.GetInterfaces().Contains(InterfaceType);
        }
    }
}