namespace AGSMixMembershipProvider
{
    using System;
    using System.Collections.Specialized;
    using System.Web.Security;

    internal class AGSSqlRoleProvider : SqlRoleProvider
    {
        public readonly string CONN_STR_NAME_PROP = "connectionStringName";
        public readonly string CONN_STR_NAME_VAL = "SQLConnString";
        public readonly string CONN_STR_PROP = "connectionString";
        public readonly string SQL_CONN_STR_FIELD = "_sqlConnectionString";

        public override void Initialize(string name, NameValueCollection config)
        {
            string fieldValue = config[this.CONN_STR_PROP];
            config.Remove(this.CONN_STR_PROP);
            config.Add(this.CONN_STR_NAME_PROP, this.CONN_STR_NAME_VAL);
            base.Initialize(name, config);
            base.ApplicationName = "esriags";
            Type baseType = base.GetType().BaseType;
            Util.SetFieldValue(this, baseType, this.SQL_CONN_STR_FIELD, fieldValue);
            this.GetRolesForUser("dummy");
        }
    }
}

