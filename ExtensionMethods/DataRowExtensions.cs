using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RMP400S_SG_Placement.Database.ExtensionMethods
{
    public static class DataRowExtensions
    {
        #region public methods
        /// <summary>
        /// Fill a class' public properties 
        /// Any properties with the 'Local' attribute will be ignored
        /// The Deserilise attribute should be used for JSON properties or classes
        /// This Method will create a new class of the specified type
        /// </summary>
        /// <typeparam name="TClass"></typeparam>
        /// <param name="dataRow"></param>
        /// <returns></returns>
        public static async Task<TClass> ToClass<TClass>(this DataRow dataRow) where TClass : class
        {
            //create an instance of the class type
            TClass classInstance = (TClass)Activator.CreateInstance(typeof(TClass));
            await ToClassRecursive(dataRow, classInstance, typeof(TClass));

            return classInstance;
        }

        /// <summary>
        /// Fill a class' public properties 
        /// Any properties with the 'Local' attribute will be ignored
        /// The Deserilise attribute should be used for JSON properties or classes
        /// </summary>
        /// <param name="dataRow"></param>
        /// <param name="classInstance"></param>
        /// <param name="ParentClassNames">Should be unset externally as used for recursion in the class</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<TClass> ToClass<TClass>(this DataRow dataRow, TClass classInstance) where TClass : class
        {
            await ToClassRecursive(dataRow, classInstance, classInstance.GetType());
            return classInstance;
        }
        /// <summary>
        /// Fill a class' public properties 
        /// Any properties with the 'Local' attribute will be ignored
        /// The Deserilise attribute should be used for JSON properties or classes
        /// </summary>
        /// <param name="dataRow"></param>
        /// <param name="classType"></param>
        /// <param name="ParentClassNames">Should be unset externally as used for recursion in the class</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task ToClass(this DataRow dataRow, Type classType)
        {
            await ToClassRecursive(dataRow, classType, classType);
        }
        #endregion

        #region Private Methods
        private static async Task ToClassRecursive(DataRow datarow, object classInstance, Type classType, string ParentClassNames = null)
        {
            List<PropertyInfo> propertyInfos = classType.GetProperties().ToList();

            bool IsUserDefinedClass(PropertyInfo x) { return x.PropertyType.Assembly.FullName == classType.Assembly.FullName && x.PropertyType.IsClass; }

            List<PropertyInfo> subClasses = propertyInfos.FindAll(x => IsUserDefinedClass(x)).ToList();
            propertyInfos.RemoveAll(x => IsUserDefinedClass(x));

            foreach (PropertyInfo subClass in subClasses)
            {
                if (subClass.CustomAttributes.Any(x => x.AttributeType.Equals(typeof(LocalAttribute))))
                {
                    continue;
                }
                else if (subClass.CustomAttributes.Any(x => x.AttributeType.Equals(typeof(DeserialiseAttribute))))
                {
                    //JSON deserialise
                    object subClassInstance = Activator.CreateInstance(subClass.PropertyType);
                    SetProperty(subClass, subClass.Name, datarow, classInstance);
                }
                else
                {
                    object newSubClass = Activator.CreateInstance(subClass.PropertyType);
                    string newParentName;
                    if (ParentClassNames != null) newParentName = $"{ParentClassNames}_";
                    newParentName = subClass.Name; //use property name not class name

                    await ToClassRecursive(datarow, newSubClass, newSubClass.GetType(), newParentName);
                    subClass.SetValue(classInstance, newSubClass);
                }
            }

            foreach (PropertyInfo subProperty in propertyInfos)
            {
                if (subProperty.CustomAttributes.Any(x => x.AttributeType.Equals(typeof(LocalAttribute))))
                {
                    continue;
                }

                string fieldName = string.Empty;
                if (ParentClassNames != null)
                {
                    fieldName = $"{ParentClassNames}_";
                }
                fieldName += $"{subProperty.Name}";

                if (!datarow.Table.Columns.Contains(fieldName))
                {
                    throw new Exception($"Column with the name '{fieldName}' could not be found in {classType.Name}");
                }

                Type columnType = datarow.Table.Columns[fieldName].DataType;

                SetProperty(subProperty, fieldName, datarow, classInstance);
            }
        }

        private static void SetProperty(PropertyInfo subProperty, string fieldName, DataRow datarow, object classInstance)
        {
            try
            {
                object value = datarow.Field<object>(fieldName);

                if (value != null)
                {
                    if (subProperty.CustomAttributes.Any(x => x.AttributeType.Equals(typeof(DeserialiseAttribute))))
                    {
                        //JSON
                        string JSON = (string)value;
                        object deserialisedObject = JsonConvert.DeserializeObject(JSON, subProperty.PropertyType);
                        subProperty.SetValue(classInstance, deserialisedObject);
                    }
                    else if (subProperty.PropertyType.BaseType == typeof(Enum))
                    {
                        var enumValue = Enum.Parse(subProperty.PropertyType, (string)value);
                        subProperty.SetValue(classInstance, enumValue);
                    }
                    else
                    {
                        object val = Convert.ChangeType(value, subProperty.PropertyType);
                        subProperty.SetValue(classInstance, val);
                    }
                }
            }

            catch (Exception e)
            {
                throw new Exception($"Error converting {fieldName}:{e.Message}");
            }
        }
        #endregion
    }
}
