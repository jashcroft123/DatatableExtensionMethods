using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RMP400S_SG_Placement.Database.ExtensionMethods
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class LocalAttribute : Attribute
    {
    }
}
