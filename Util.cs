namespace AGSMixMembershipProvider
{
    using ESRI.ArcGIS.esriSystem;
    using System;
    using System.Collections.Specialized;
    using System.Reflection;
    using System.Web.Security;

    internal class Util
    {
        public static void AddToProviders(MembershipProvider provider, string providerName)
        {
            SetFieldValue(Membership.Providers, Membership.Providers.GetType().BaseType, "_ReadOnly", false);
            Membership.Providers.Add(provider);
        }

        public static void CheckForNull(string paramName, object param)
        {
            if (param == null)
            {
                throw new Exception("Parameter '" + paramName + "' cannot be null.");
            }
        }

        public static NameValueCollection Convert(IPropertySet props)
        {
            NameValueCollection values = new NameValueCollection();
            object names = new object();
            object obj3 = new object();
            props.GetAllProperties(out names, out obj3);
            object[] objArray = (object[]) names;
            object[] objArray2 = (object[]) obj3;
            for (int i = 0; i < objArray.Length; i++)
            {
                values.Set(objArray[i].ToString(), objArray2[i].ToString());
            }
            return values;
        }

        public static void SetFieldValue(object obj, Type t, string fieldName, object fieldValue)
        {
            FieldInfo field = t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                throw new Exception("Could not find property '" + fieldName + "' in the class.");
            }
            field.SetValue(obj, fieldValue);
        }
    }
}

