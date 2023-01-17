using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMP400S_SG_Placement.Database.ExtensionMethods
{
    /// <summary>
    /// Used to define a object is to be deserialise from a JSON
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class DeserialiseAttribute : Attribute
    {
    }
}
