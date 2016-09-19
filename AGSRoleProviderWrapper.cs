namespace AGSMixMembershipProvider
{
    using ESRI.ArcGIS.esriSystem;
    using ESRI.ArcGIS.Server;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Web.Security;
    using System.IO;
    using System.Text;
    using System.Reflection;

    [Guid("70C70AAE-7BAC-4d53-95A0-2AEBAFC03E7F"), ComVisible(true)]
    public class AGSRoleProviderWrapper : IRoleStore
    {
        private RoleProvider _innerRoleProvider;
        private RoleProviderEx _innerRoleProviderEx;
        public readonly string AGS_AD_ROLE_PROVIDER_CLASS = "AGSMixMembershipProvider.AGSMixRoleProvider";
        public readonly string AGS_AD_ROLE_PROVIDER_SHORTNAME = "AGSMixRoleProvider";
        public readonly string CLASS_PROP = "class";
        public readonly string SQL_ROLE_PROVIDER_CLASS = "System.Web.Security.SqlRoleProvider";
        public readonly string SQL_ROLE_PROVIDER_SHORTNAME = "SqlRoleProvider";

        private void assertInitialized()
        {
            this.Log(MethodBase.GetCurrentMethod().Name, MethodBase.GetCurrentMethod().Name);

            if (this._innerRoleProvider == null)
            {
                throw new Exception("Initialize function has not been invoked on this object.");
            }
        }

        void IRoleStore.AddRole(IRole pRole)
        {
            this.Log("Role: " + pRole.GetRolename(), MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("pRole", pRole);
            this._innerRoleProvider.CreateRole(pRole.GetRolename());
        }

        void IRoleStore.AddUsersToRole(string rolename, ref string[] usernames)
        {
            this.Log("Role: " + rolename, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("rolename", rolename);
            Util.CheckForNull("usernames", usernames);
            this._innerRoleProvider.AddUsersToRoles(usernames, new string[] { rolename });
        }

        void IRoleStore.AssignRoles(string userName, ref string[] rolenames)
        {
            this.Log("userName: " + userName, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("username", userName);
            Util.CheckForNull("rolenames", rolenames);
            string[] usernames = new string[] { userName };
            foreach (string str in rolenames)
            {
                this._innerRoleProvider.AddUsersToRoles(usernames, new string[] { str });
            }
        }

        void IRoleStore.DeleteRole(string rolename)
        {
            this.Log("rolename: " + rolename, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("rolename", rolename);
            string[] usersInRole = this._innerRoleProvider.GetUsersInRole(rolename);
            if ((usersInRole != null) && (usersInRole.Length > 0))
            {
                this._innerRoleProvider.RemoveUsersFromRoles(usersInRole, new string[] { rolename });
            }
            this._innerRoleProvider.DeleteRole(rolename, false);
        }

        bool IRoleStore.GetAllRoles(string filter, int maxCount, out IRole[] roles)
        {
            this.Log("filter: " + filter, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            if (filter == null)
            {
                filter = "";
            }
            string[] allRoles = null;
            if (this._innerRoleProviderEx != null)
            {
                allRoles = this._innerRoleProviderEx.GetAllRoles(filter);
            }
            else
            {
                allRoles = this._innerRoleProvider.GetAllRoles();
            }
            List<string> list = new List<string>();
            for (int i = 0; i < allRoles.Length; i++)
            {
                string source = allRoles[i];
                string str2 = source;
                if (source.Contains<char>('\\'))
                {
                    str2 = source.Substring(source.IndexOf('\\') + 1);
                }
                if (source.ToLower().StartsWith(filter.ToLower()) || str2.ToLower().StartsWith(filter.ToLower()))
                {
                    list.Add(source);
                }
            }
            int num2 = ((maxCount >= 0) && (maxCount <= list.Count)) ? maxCount : list.Count;
            roles = new AGSRole[num2];
            for (int j = 0; j < num2; j++)
            {
                IRole role = new AGSRole();
                role.SetRolename(list[j]);
                roles[j] = role;
            }
            return (num2 < list.Count);
        }

        bool IRoleStore.GetAllRolesPaged(int StartIndex, int pageSize, out IRole[] roles)
        {
            this.Log("StartIndex: " + StartIndex, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            if (StartIndex < 0)
            {
                throw new Exception("startIndex cannot be negative.");
            }
            string[] allRoles = this._innerRoleProvider.GetAllRoles();
            if (StartIndex >= allRoles.Length)
            {
                roles = new AGSRole[0];
                return false;
            }
            int num = (StartIndex + pageSize) - 1;
            if (num >= allRoles.Length)
            {
                num = allRoles.Length - 1;
            }
            roles = new AGSRole[(num - StartIndex) + 1];
            int num2 = 0;
            for (int i = StartIndex; i <= num; i++)
            {
                IRole role = new AGSRole();
                role.SetRolename(allRoles[i]);
                roles[num2++] = role;
            }
            return (num < (allRoles.Length - 1));
        }

        IRole IRoleStore.GetRole(string rolename)
        {
            this.Log("rolename: " + rolename, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("rolename", rolename);
            if (this._innerRoleProviderEx != null)
            {
                string fullyQualifiedRolename = this._innerRoleProviderEx.GetFullyQualifiedRolename(rolename);
                if ((fullyQualifiedRolename != null) && !fullyQualifiedRolename.Equals(""))
                {
                    IRole role = new AGSRole();
                    role.SetRolename(fullyQualifiedRolename);
                    return role;
                }
                return null;
            }
            if (this._innerRoleProvider.RoleExists(rolename))
            {
                IRole role2 = new AGSRole();
                role2.SetRolename(rolename);
                return role2;
            }
            return null;
        }

        bool IRoleStore.GetRolesForUser(string userName, string filter, int maxCount, out string[] rolenames)
        {
            this.Log("userName: " + userName, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            if (filter == null)
            {
                filter = "";
            }

            
            

            string[] rolesForUser = this._innerRoleProvider.GetRolesForUser(userName);


            
            if (rolesForUser.Length > 0)
            {
                this.Log("userName: " + userName + "roles:" + string.Join(",", rolesForUser), MethodBase.GetCurrentMethod().Name);
            }


            List<string> list = new List<string>();
            for (int i = 0; i < rolesForUser.Length; i++)
            {
                string source = rolesForUser[i];
                string str2 = source;
                if (source.Contains<char>('\\'))
                {
                    str2 = source.Substring(source.IndexOf('\\') + 1);
                }
                if (source.ToLower().StartsWith(filter.ToLower()) || str2.ToLower().StartsWith(filter.ToLower()))
                {
                    list.Add(source);
                }
            }
            int num2 = ((maxCount >= 0) && (maxCount <= list.Count)) ? maxCount : list.Count;
            rolenames = new string[num2];
            for (int j = 0; j < num2; j++)
            {
                rolenames[j] = list[j];
            }
            return (num2 < list.Count);
        }

        int IRoleStore.GetTotalRoles() => 
            this._innerRoleProvider.GetAllRoles().Length;

        bool IRoleStore.GetUsersWithinRole(string rolename, string filter, int maxCount, out string[] usernames)
        {
            this.Log("rolename: " + rolename, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            if (filter == null)
            {
                filter = "";
            }

            string[] usersInRole = this._innerRoleProvider.GetUsersInRole(rolename);

            if (usersInRole.Length > 0)
            {
                this.Log("roles: " + rolename + "users:" + string.Join(",", usersInRole), MethodBase.GetCurrentMethod().Name);
            }


            List<string> list = new List<string>();
            for (int i = 0; i < usersInRole.Length; i++)
            {
                string source = usersInRole[i];
                string str2 = source;
                if (source.Contains<char>('\\'))
                {
                    str2 = source.Substring(source.IndexOf('\\') + 1);
                }
                if (source.ToLower().StartsWith(filter.ToLower()) || str2.ToLower().StartsWith(filter.ToLower()))
                {
                    list.Add(source);
                }
            }
            int num2 = ((maxCount >= 0) && (maxCount <= list.Count)) ? maxCount : list.Count;
            usernames = new string[num2];
            for (int j = 0; j < num2; j++)
            {
                usernames[j] = list[j];
            }
            return (num2 < list.Count);
        }

        void IRoleStore.Initialize(IPropertySet pProps)
        {
            this.Log("property:" + (string)pProps.GetProperty(this.CLASS_PROP) ?? "", MethodBase.GetCurrentMethod().Name);

            string property = (string) pProps.GetProperty(this.CLASS_PROP);
            if (property == null)
            {
                throw new Exception("Could not find required property '" + this.CLASS_PROP + "' in the input properties.");
            }
            pProps.RemoveProperty(this.CLASS_PROP);
            NameValueCollection config = Util.Convert(pProps);
            this.Initialize(property, config);
        }

        bool IRoleStore.IsReadOnly() => 
            false;

        void IRoleStore.RemoveRoles(string userName, ref string[] rolenames)
        {
            this.Log("userName:" + userName, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("userName", userName);
            Util.CheckForNull("rolenames", rolenames);
            this._innerRoleProvider.RemoveUsersFromRoles(new string[] { userName }, rolenames);
        }

        void IRoleStore.RemoveUsersFromRole(string rolename, ref string[] usernames)
        {
            this.Log("rolename:" + rolename, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("roleName", rolename);
            Util.CheckForNull("usernames", usernames);
            this._innerRoleProvider.RemoveUsersFromRoles(usernames, new string[] { rolename });
        }

        void IRoleStore.TestConnection(IPropertySet pProps)
        {
            IRoleStore store = new AGSRoleProviderWrapper();
            store.Initialize(pProps);
        }

        void IRoleStore.UpdateRole(IRole pRole)
        {
        }

        public void Initialize(string name, NameValueCollection config)
        {

            this.Log("name:" + name, MethodBase.GetCurrentMethod().Name);

            object obj2 = null;
            if (name.Equals(this.SQL_ROLE_PROVIDER_CLASS) || name.Equals(this.SQL_ROLE_PROVIDER_SHORTNAME))
            {
                Console.WriteLine("Creating an instance of SqlRoleProvider");
                obj2 = new AGSSqlRoleProvider();
                Console.WriteLine("SqlRoleProvider instantiated successfully.");
            }
            else if (name.Equals(this.AGS_AD_ROLE_PROVIDER_CLASS) || name.Equals(this.AGS_AD_ROLE_PROVIDER_SHORTNAME))
            {
                Console.WriteLine("Creating an instance of AGSMixRoleProvider");
                obj2 = new AGSMixRoleProvider();
                Console.WriteLine("AGSMixRoleProvider instantiated successfully.");
            }
            else
            {
                Type type = Type.GetType(name);
                if (type == null)
                {
                    string str = "Could not create an instance of class '" + name + "'. Type not found in default assembly.";
                    Console.WriteLine(str);
                    throw new Exception(str);
                }
                Console.WriteLine("Creating instance of RoleProvider class '" + type + "'.");
                object obj3 = Activator.CreateInstance(type);
                if ((obj3 == null) || !(obj3 is RoleProvider))
                {
                    string str2 = "Instance of class '" + name + "' could not be created or class does not extend RoleProvider.";
                    Console.WriteLine(str2);
                    throw new Exception(str2);
                }
                obj2 = obj3;
            }
            this._innerRoleProvider = obj2 as RoleProvider;
            if (this._innerRoleProvider != null)
            {
                Console.WriteLine("Initializing RoleProvider.");
                this._innerRoleProvider.Initialize(name, config);
                Console.WriteLine("RoleProvider initialized successfully.");
                this._innerRoleProvider.ApplicationName = "esriags";
            }
            this._innerRoleProviderEx = obj2 as RoleProviderEx;
            if (this._innerRoleProviderEx != null)
            {
                Console.WriteLine("RoleProviderEx interface is implemented.");
            }
            else
            {
                Console.WriteLine("RoleProviderEx interface is NOT implemented.");
            }
        }

        private void Log(string message, string method)
        {
            if (1 == 0)
            {
                StringBuilder log = new StringBuilder();
                log.AppendFormat("Role Provider Wrapper - {0} - {1} - {2}{3}", DateTime.Now.ToString(), message, method, Environment.NewLine);
                File.AppendAllText(@"F:\arcgisDev\ProviderAGS\AGSMixMembershipProvider\bin\Debug\log.txt", log.ToString());
            }
        }

    }
}

