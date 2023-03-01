using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RMP400S_SG_Placement.Database.ExtensionMethods
{
    public static class DataTableExtensions
    {
        #region Methods Properties
        public static async Task<Dictionary<TKey, ObservableCollection<TValue>>> ToDictionary<TKey, TValue>(this DataTable dataTable, string keyName) where TValue : class
        {
            Dictionary<TKey, ObservableCollection<TValue>> dict = new Dictionary<TKey, ObservableCollection<TValue>>();
            TValue classInstance;

            DataTable singleRowTable = dataTable.Clone();

            foreach (DataRow row in dataTable.Rows)
            {
                classInstance = await row.ToClass<TValue>();
                singleRowTable.Clear();

                TKey key = (TKey)classInstance.GetType().GetProperty(keyName).GetValue(classInstance);

                ObservableCollection<TValue> enumerableValue;
                if (dict.ContainsKey(key))
                {
                    enumerableValue = dict.GetValueOrDefault(key);
                    dict.Remove(key);
                }
                else
                {
                    enumerableValue = new ObservableCollection<TValue>();
                }

                enumerableValue.Add(classInstance);
                dict.Add(key, enumerableValue);
            }

            return dict;
        }

        public static async Task<ObservableCollection<TValue>> ToObservableCollection<TValue>(this DataTable dataTable) where TValue : class
        {
            ObservableCollection<TValue> values = new ObservableCollection<TValue>();
            TValue classInstance;

            DataTable singleRowTable = dataTable.Clone();

            foreach (DataRow row in dataTable.Rows)
            {
                classInstance = await row.ToClass<TValue>();
                singleRowTable.Clear();
                values.Add(classInstance);
            }

            return values;
        }
        #endregion

        #region Private Methods
        private static async Task ToClassRecursive(DataTable dataTable, object classInstance, string ParentClassNames = null)
        {
            if (!(dataTable.Rows.Count == 1))
            {
                throw new Exception($"In Datatable.ToClass, the given had {dataTable.Rows.Count} rows, only one row allowed");
            }

            Type classType = classInstance.GetType();
            List<PropertyInfo> propertyInfos = classType.GetProperties().ToList();

            bool IsUserDefinedClass(PropertyInfo x) { return x.PropertyType.Assembly.FullName == classType.Assembly.FullName && x.PropertyType.IsClass; }
            bool IsUserDefinedEnumerable(PropertyInfo x) { return x.PropertyType.Assembly.FullName == classType.Assembly.FullName && x.PropertyType.IsCollectible; }
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
                    SetProperty(subClass, subClass.Name, dataTable, classInstance);
                }
                else
                {
                    object newSubClass = Activator.CreateInstance(subClass.PropertyType);
                    string newParentName;
                    if (ParentClassNames != null) newParentName = $"{ParentClassNames}_";
                    newParentName = subClass.Name; //use property name not class name

                    await ToClassRecursive(dataTable, newSubClass, newParentName);
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

                if (!dataTable.Columns.Contains(fieldName))
                {
                    throw new Exception($"Column with the name '{fieldName}' could not be found in {classType.Name}");
                }

                Type columnType = dataTable.Columns[fieldName].DataType;

                SetProperty(subProperty, fieldName, dataTable, classInstance);
            }
        }

        private static void SetProperty(PropertyInfo subProperty, string fieldName, DataTable sqlTable, object classInstance)
        {
            try
            {
                object value = sqlTable.Rows[0].Field<object>(fieldName);

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
                        subProperty.SetValue(classInstance, Convert.ChangeType(value, subProperty.PropertyType));
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
