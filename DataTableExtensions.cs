using Newtonsoft.Json;
using RMP400S_SG_Placement.Process.Engineering;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Asn1;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RMP400S_SG_Placement.Database.ExtensionMethods
{
    public static class DataTableExtensions
    {
        #region Public Properties
        /// <summary>
        /// Fill a class' public properties 
        /// Any properties with the 'Local' attribute will be ignored
        /// The Deserilise attribute should be used for JSON properties or classes
        /// This Method will create a new class of the specified type
        /// </summary>
        /// <typeparam name="TClass"></typeparam>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        public static async Task<TClass> ToClass<TClass>(this DataTable dataTable) where TClass : class
        {
            TClass classInstance = (TClass)Activator.CreateInstance(typeof(TClass));
            await ToClassRecursive(dataTable, classInstance);

            return classInstance;
        }

        /// <summary>
        /// Fill a class' public properties 
        /// Any properties with the 'Local' attribute will be ignored
        /// The Deserilise attribute should be used for JSON properties or classes
        /// </summary>
        /// <param name="dataTable"></param>
        /// <param name="classInstance"></param>
        /// <param name="ParentClassNames">Should be unset externally as used for recursion in the class</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<TClass> ToClass<TClass>(this DataTable dataTable, TClass classInstance)
        {
            await ToClassRecursive(dataTable, classInstance);
            return classInstance;
        }

        public static async Task<Dictionary<TKey, ObservableCollection<TValue>>> ToDictionary<TKey, TValue>(this DataTable dataTable, string keyName) where TValue : class
        {
            Dictionary<TKey, ObservableCollection<TValue>> dict = new Dictionary<TKey, ObservableCollection<TValue>>();
            TValue classInstance;

            DataTable singleRowTable = dataTable.Clone();

            foreach (DataRow row in dataTable.Rows)
            {
                singleRowTable.ImportRow(row);
                classInstance = await singleRowTable.ToClass<TValue>();
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
                singleRowTable.ImportRow(row);
                classInstance = await singleRowTable.ToClass<TValue>();
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
