namespace AGSMixMembershipProvider
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Configuration.Provider;
    using System.Data;
    using System.Data.SqlClient;
    using System.DirectoryServices;
    using System.DirectoryServices.AccountManagement;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    public class AGSMixRoleProvider : RoleProviderEx
    {
        private string userNameAD = null;
        private string passwordAD = null;
        private string connectionString = null;
        private bool useRolesDBforAD = false;

        private string applicationName;
        private string currentDomainName;

        private Dictionary<string, string> dnsroot2NETBIOSName = new Dictionary<string, string>();
        private Dictionary<string, string> ncname2NETBIOSName = new Dictionary<string, string>();

        public override void AddUsersToRoles(string[] usernames, string[] roleNames)
        {

            this.Log("username:" + string.Join(",", usernames) + " roles:" + string.Join(",", roleNames), MethodBase.GetCurrentMethod().Name);

            try
            {
                bool existUsersSQL = usernames.Any(s => !this.UserExistsSQLServer(s));
                
                if (!this.useRolesDBforAD)
                {
                    if (!existUsersSQL)
                    {
                        throw new ProviderException("Users not found!");
                    }
                }

                //roles used from sql user and ad user
                if (roleNames.Any(s => !this.RoleExistsSQLServer(s)))
                {
                    throw new ProviderException("Roles not found!");
                }

                int[] idRoles = this.Roles(roleNames);

                int[] idUsers = this.Users(usernames);
                
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();

                    SqlCommand cmd = connection.CreateCommand();
                    SqlTransaction transaction = connection.BeginTransaction(MethodBase.GetCurrentMethod().Name);
                    
                    cmd.Connection = connection;
                    cmd.Transaction = transaction;
                    try
                    {
                        foreach (int u in idUsers)
                        {
                            foreach (int r in idRoles)
                            {
                                
                                cmd.CommandText = "SELECT Count(*) FROM UsersRoles WHERE IdRole=@IdRole AND IdUser=@IdUser";
                                cmd.Parameters.Clear();
                                cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = r;
                                cmd.Parameters.Add("@IdUser", SqlDbType.Int).Value = u;

                                int count = (int)cmd.ExecuteScalar();
                                if (count > 0)
                                {
                                    continue;
                                }
                               

                                cmd.CommandText = "INSERT INTO UsersRoles" +
                                        " (IdRole,IdUser)" +
                                        " VALUES (@IdRole, @IdUser)";

                                cmd.Parameters.Clear();
                                cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = r;
                                cmd.Parameters.Add("@IdUser", SqlDbType.Int).Value = u;
                                cmd.ExecuteNonQuery();
                            }
                        }

                        if (this.useRolesDBforAD)
                        {
                            foreach (string u in usernames)
                            {
                                if (this.UserExistsSQLServer(u))
                                { 
                                    continue;
                                }

                                foreach (int r in idRoles)
                                {

                                    cmd.CommandText = "SELECT Count(*) FROM UsersRolesAD WHERE IdRole=@IdRole AND Username=@Username";
                                    cmd.Parameters.Clear();
                                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = r;
                                    cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = u;

                                    int count = (int)cmd.ExecuteScalar();
                                    if (count > 0)
                                    {
                                        continue;
                                    }


                                    cmd.CommandText = "INSERT INTO UsersRolesAD" +
                                            " (IdRole,Username)" +
                                            " VALUES (@IdRole, @Username)";

                                    cmd.Parameters.Clear();
                                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = r;
                                    cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = u;
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        if (transaction != null)
                        {
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception e2)
                            {
                                throw new ProviderException(e2.Message);
                            }
                        }

                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }
        }

        public override void CreateRole(string roleName)
        {
            this.Log("roleName:" + roleName, MethodBase.GetCurrentMethod().Name);

            try
            {
                if (this.RoleExists(roleName))
                {
                    throw new ProviderException("Role exists!");
                }

                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO Roles (Rolename) VALUES (@Rolename)", connection))
                    {
                        cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = roleName;

                        connection.Open();

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }
        }

        public override bool DeleteRole(string roleName, bool throwOnPopulatedRole)
        {

            this.Log("roleName:" + roleName, MethodBase.GetCurrentMethod().Name);

            int rowsAffected = 0;

            try
            {
                
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();

                    //there is the cascade delete in db sql server
                    //using (SqlCommand cmd = new SqlCommand("SELECT Users.Id FROM Roles INNER JOIN UsersRoles ON Roles.Id = UsersRoles.IdRolename INNER JOIN Users ON UsersRoles.IdUsername = Users.Id WHERE Roles.Rolename = @Role", connection))
                    //{
                    //    cmd.Parameters.Add("@Role", SqlDbType.NVarChar).Value = roleName;
                    //    object id = cmd.ExecuteScalar();

                    //    if (id != null)
                    //    {
                    //        throw new ProviderException("Cannot delete a populated role.");
                    //    }
                    //}

                    using (SqlCommand cmd = new SqlCommand("DELETE FROM Roles WHERE Rolename= @Rolename", connection))
                    {
                        cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = roleName;

                        rowsAffected = cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
               
            }

            return rowsAffected > 0;

        }

        public override string[] FindUsersInRole(string roleName, string usernameToMatch)
        {
            this.Log("roleName:" + roleName, MethodBase.GetCurrentMethod().Name);
            throw new NotImplementedException();
        }

        public override string[] GetAllRoles() => 
            this.GetAllRoles("");

        public override string[] GetAllRoles(string rolenameToMatch)
        {
            this.Log("rolenameToMatch:" + rolenameToMatch, MethodBase.GetCurrentMethod().Name);

            string domain = null;
            string name = null;
            LinkedList<string> source = new LinkedList<string>();
            try
            {
                ADUtil.splitDomainAndName(rolenameToMatch, out domain, out name);
                if ((domain == null) || domain.Equals(""))
                {
                    domain = this.dnsroot2NETBIOSName[this.currentDomainName];
                }
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, null, domain, this.userNameAD, this.passwordAD))
                {
                    using (GroupPrincipal principal = new GroupPrincipal(context))
                    {
                        principal.SamAccountName = name + "*";
                        principal.IsSecurityGroup = true;
                        using (PrincipalSearcher searcher = new PrincipalSearcher())
                        {
                            searcher.QueryFilter = principal;
                            using (PrincipalSearchResult<Principal> result = searcher.FindAll())
                            {
                                try
                                {
                                    foreach (GroupPrincipal principal2 in result)
                                    {
                                        if (principal2 != null)
                                        {
                                            if ((principal2.DistinguishedName != null) && !principal2.DistinguishedName.Equals(""))
                                            {
                                                source.AddLast(this.ncname2NETBIOSName[ADUtil.getDCcomponent(principal2.DistinguishedName)] + @"\" + principal2.SamAccountName);
                                            }
                                            principal2.Dispose();
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                this.Log(exception.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException("Getting all roles with filter '" + rolenameToMatch + "' failed.", exception);
            }

            //add roles sql server
            List<string> roles = new List<string>();
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Rolename FROM Roles ORDER BY Rolename", connection))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    roles.Add(reader.GetString(0));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }

            string[] rolesSQL =  roles.ToArray();

            return source.Concat(roles).ToArray();
        }

        public override string GetFullyQualifiedRolename(string roleName)
        {
            this.Log("roleName:" + roleName, MethodBase.GetCurrentMethod().Name);

            if (this.RoleExistsSQLServer(roleName))
            {
                return roleName;
            }

            string domain = null;
            string name = null;
            string str3 = null;
            try
            {
                ADUtil.splitDomainAndName(roleName, out domain, out name);
                if ((domain == null) || domain.Equals(""))
                {
                    domain = this.dnsroot2NETBIOSName[this.currentDomainName];
                }
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, null, domain, this.userNameAD, this.passwordAD))
                {
                    using (GroupPrincipal principal = GroupPrincipal.FindByIdentity(context, name))
                    {
                        if ((principal != null) && (principal.DistinguishedName != null))
                        {
                            str3 = this.ncname2NETBIOSName[ADUtil.getDCcomponent(principal.DistinguishedName)] + @"\" + principal.SamAccountName;
                        }
                    }
                    return str3;
                }
            }
            catch (Exception exception)
            {
                this.Log(exception.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException("Getting fully qualified name for role '" + roleName + "' failed.", exception);
            }
        }

        public override string[] GetRolesForUser(string username)
        {
            this.Log("username:" + username, MethodBase.GetCurrentMethod().Name);


            if (this.UserExistsSQLServer(username))
            {
                List<string> roles = new List<string>();
                try
                {
                    using (SqlConnection connection = new SqlConnection(this.connectionString))
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT Roles.Rolename FROM Roles INNER JOIN UsersRoles ON Roles.Id = UsersRoles.IdRole INNER JOIN Users ON UsersRoles.IdUser = Users.Id WHERE Users.Username = @Username ORDER BY Roles.Rolename", connection))
                        {
                            cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;
                            connection.Open();
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        roles.Add(reader.GetString(0));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                    throw new ProviderException(e.Message);
                }

                this.Log("Roles:" + string.Join(",", roles.ToArray()), MethodBase.GetCurrentMethod().Name);
                return roles.ToArray();
            }

            string domain = null;
            string name = null;
            LinkedList<string> source = new LinkedList<string>();
            try
            {
                ADUtil.splitDomainAndName(username, out domain, out name);
                if ((domain == null) || domain.Equals(""))
                {
                    domain = this.dnsroot2NETBIOSName[this.currentDomainName];
                }
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, null, domain, this.userNameAD, this.passwordAD))
                {
                    using (UserPrincipal principal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, name))
                    {
                        if (principal == null)
                        {
                            throw new ProviderException("Unable to find user '" + username + "'.");
                        }
                        using (PrincipalSearchResult<Principal> result = principal.GetAuthorizationGroups())
                        {
                            using (IEnumerator<Principal> enumerator = result.GetEnumerator())
                            {
                                while (enumerator.MoveNext())
                                {
                                    try
                                    {
                                        using (Principal principal2 = enumerator.Current)
                                        {
                                            if ((principal2 != null) && (principal2 is GroupPrincipal))
                                            {
                                                using (GroupPrincipal principal3 = (GroupPrincipal) principal2)
                                                {
                                                    if ((principal3.DistinguishedName != null) && principal3.IsSecurityGroup.Value)
                                                    {
                                                        source.AddLast(this.ncname2NETBIOSName[ADUtil.getDCcomponent(principal3.DistinguishedName)] + @"\" + principal3.SamAccountName);
                                                    }
                                                }
                                            }
                                        }
                                        continue;
                                    }
                                    catch (Exception)
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                this.Log(exception.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException("Getting roles for user '" + username + "' failed.", exception);
            }

            if (this.useRolesDBforAD)
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(this.connectionString))
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT Roles.Rolename FROM Roles INNER JOIN UsersRolesAD ON Roles.Id = UsersRolesAD.IdRole WHERE UsersRolesAD.Username = @Username ORDER BY Roles.Rolename", connection))
                        {
                            cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;
                            connection.Open();
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        source.AddLast(reader.GetString(0));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                    throw new ProviderException("Getting roles for user '" + username + "' failed.", e);
                }
            }

            this.Log("Roles:" + string.Join(",", source.ToArray<string>()), MethodBase.GetCurrentMethod().Name);
            return source.ToArray<string>();
        }

        public override string[] GetUsersInRole(string roleName)
        {
            this.Log("roleName:" + roleName, MethodBase.GetCurrentMethod().Name);

            if (this.RoleExistsSQLServer(roleName))
            {
                List<string> users = new List<string>();
                try
                {
                    using (SqlConnection connection = new SqlConnection(this.connectionString))
                    {
                        connection.Open();
                        using (SqlCommand cmd = new SqlCommand("SELECT Users.Username FROM Roles INNER JOIN UsersRoles ON Roles.Id = UsersRoles.IdRole INNER JOIN Users ON UsersRoles.IdUser = Users.Id WHERE (Roles.Rolename = @Rolename) ORDER BY Users.Username", connection))
                        {
                            cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = roleName;
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        users.Add(reader.GetString(0));
                                    }
                                }
                            }
                        }

                        if (this.useRolesDBforAD)
                        {
                            using (SqlCommand cmd = new SqlCommand("SELECT UsersRolesAD.Username FROM Roles INNER JOIN UsersRolesAD ON Roles.Id = UsersRolesAD.IdRole WHERE Roles.Rolename = @Rolename ORDER BY UsersRolesAD.Username", connection))
                            {
                                cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = roleName;
                                using (SqlDataReader reader = cmd.ExecuteReader())
                                {
                                    if (reader.HasRows)
                                    {
                                        while (reader.Read())
                                        {
                                            users.Add(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                    throw new ProviderException(e.Message);
                }

                return users.ToArray();
            }


            string domain = null;
            string name = null;
            LinkedList<string> source = new LinkedList<string>();
            try
            {
                ADUtil.splitDomainAndName(roleName, out domain, out name);
                if ((domain == null) || domain.Equals(""))
                {
                    domain = this.dnsroot2NETBIOSName[this.currentDomainName];
                }
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, null, domain, this.userNameAD, this.passwordAD))
                {
                    using (GroupPrincipal principal = GroupPrincipal.FindByIdentity(context, name))
                    {
                        if (principal == null)
                        {
                            throw new ProviderException("Unable to find role '" + roleName + "'.");
                        }
                        using (PrincipalSearchResult<Principal> result = principal.GetMembers(true))
                        {
                            foreach (Principal principal2 in result)
                            {
                                if (principal2 != null)
                                {
                                    using (principal2)
                                    {
                                        if (principal2 is UserPrincipal)
                                        {
                                            using (UserPrincipal principal3 = (UserPrincipal) principal2)
                                            {
                                                if ((principal3.DistinguishedName != null) && !principal3.DistinguishedName.Equals(""))
                                                {
                                                    source.AddLast(this.ncname2NETBIOSName[ADUtil.getDCcomponent(principal3.DistinguishedName)] + @"\" + principal3.SamAccountName);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                this.Log(exception.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException("Getting users in role '" + roleName + "' failed.", exception);
            }

            return source.ToArray<string>();
        }

        public override void Initialize(string name, NameValueCollection config)
        {

            this.userNameAD = config["usernameAD"];
            
            if (this.userNameAD == null)
            {
                throw new ProviderException("Missing required property 'usernameAD'.");
            }

            config.Remove("usernameAD");

            this.passwordAD = config["passwordAD"];
            
            if (this.passwordAD == null)
            {
                throw new ProviderException("Missing required property 'passwordAD'.");
            }

            config.Remove("passwordAD");

            this.connectionString = config["connectionStringName"];
            
            if (this.connectionString == null)
            {
                throw new ProviderException("Missing required property 'connectionStringName'.");
            }


            // use Roles SQL Server for AD
            try
            {
                this.useRolesDBforAD = Convert.ToBoolean(config["useRolesDBforAD"]);
            }
            catch
            {
                this.useRolesDBforAD = false;
            }

            // test connection
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                }
            }
            catch
            {
                throw new ProviderException("Check your DB connection!" + this.connectionString);
            }


            try
            {
                base.Initialize(name, config);
                this.currentDomainName = ADUtil.getDomainName();
                this.populateDomainNamesConversionMaps();
            }
            catch(Exception ex)
            {
                this.Log(ex.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(ex.Message);
            }
        }

        public override bool IsUserInRole(string username, string rolename)
        {

            this.Log("username:" + username + " role:" + rolename, MethodBase.GetCurrentMethod().Name);

            //check if role is in sql server
            if (this.RoleExistsSQLServer(rolename))
            {
                bool exists = false;
                try
                {
                    if (this.UserExistsSQLServer(username))
                    {
                        using (SqlConnection connection = new SqlConnection(this.connectionString))
                        {
                            connection.Open();
                            using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM UsersRoles INNER JOIN Users ON UsersRoles.IdUser = Users.Id INNER JOIN Roles ON UsersRoles.IdRole = Roles.Id WHERE (Users.Username = @Username) AND (Roles.Rolename = @Rolename)", connection))
                            {
                                cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = rolename;
                                cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;

                                int count = (int)cmd.ExecuteScalar();
                                exists = count > 0;
                            }
                        }
                    }
                    else
                    {
                        if (this.useRolesDBforAD)
                        {
                            using (SqlConnection connection = new SqlConnection(this.connectionString))
                            {
                                connection.Open();
                                using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM Roles INNER JOIN UsersRolesAD ON Roles.Id = UsersRolesAD.IdRole WHERE (Roles.Rolename = @Rolename) AND (UsersRolesAD.Username = @Username)", connection))
                                {
                                    cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = rolename;
                                    cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;

                                    int count = (int)cmd.ExecuteScalar();
                                    exists = count > 0;
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                    throw new ProviderException(e.Message);
                }

                return exists;
            }

            foreach (string str in this.GetUsersInRole(rolename))
            {
                string str2 = str;
                if (str.Contains<char>('\\'))
                {
                    str2 = str.Substring(str.IndexOf('\\') + 1);
                }
                if (str.ToLower().Equals(username.ToLower()) || str2.ToLower().Equals(username.ToLower()))
                {
                    return true;
                }
            }

            return false;
        }

        private void populateDomainNamesConversionMaps()
        {
            this.Log(MethodBase.GetCurrentMethod().Name, MethodBase.GetCurrentMethod().Name);
            try
            {
                using (DirectoryEntry entry = new DirectoryEntry("LDAP://" + this.currentDomainName + "/rootDSE", this.userNameAD, this.passwordAD))
                {
                    string str = entry.Properties["configurationNamingContext"].Value.ToString();
                    using (DirectoryEntry entry2 = new DirectoryEntry("LDAP://" + this.currentDomainName + "/" + str, this.userNameAD, this.passwordAD))
                    {
                        using (DirectorySearcher searcher = new DirectorySearcher(entry2))
                        {
                            searcher.Filter = "(NETBIOSName=*)";
                            searcher.PropertiesToLoad.Add("dnsroot");
                            searcher.PropertiesToLoad.Add("ncname");
                            searcher.PropertiesToLoad.Add("NETBIOSName");
                            using (SearchResultCollection results = searcher.FindAll())
                            {
                                foreach (SearchResult result in results)
                                {
                                    string key = result.Properties["dnsroot"][0].ToString();
                                    string str3 = result.Properties["ncname"][0].ToString();
                                    string str4 = result.Properties["NETBIOSName"][0].ToString();
                                    this.dnsroot2NETBIOSName.Add(key, str4);
                                    this.ncname2NETBIOSName.Add(str3, str4);
                                }
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                this.Log(ex.Message, MethodBase.GetCurrentMethod().Name);
                throw ex;
            }
        }

        public override void RemoveUsersFromRoles(string[] usernames, string[] rolenames)
        {
            this.Log("username:" + string.Join(",", usernames) + " roles:" + string.Join(",", rolenames), MethodBase.GetCurrentMethod().Name);

            try
            {
                int[] idUsers = this.Users(usernames);
                int[] idRoles = this.Roles(rolenames);

                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();

                    SqlCommand cmd = connection.CreateCommand();
                    SqlTransaction transaction = connection.BeginTransaction(MethodBase.GetCurrentMethod().Name);

                    cmd.Connection = connection;
                    cmd.Transaction = transaction;
                    try
                    {
                        foreach (int u in idUsers)
                        {
                            foreach (int r in idRoles)
                            {
                                cmd.CommandText = "DELETE FROM UsersRoles" +
                                        " WHERE IdRole = @IdRole AND IdUser = @IdUser";

                                cmd.Parameters.Clear();
                                cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = r;
                                cmd.Parameters.Add("@IdUser", SqlDbType.Int).Value = u;
                                cmd.ExecuteNonQuery();
                            }
                        }

                        if (this.useRolesDBforAD)
                        {
                            foreach (string u in usernames.Distinct())
                            {
                                if (this.UserExistsSQLServer(u))
                                {
                                    continue;
                                }

                                if (!this.UserExistsSQLServerAD(u))
                                {
                                    continue;
                                }

                                foreach (int r in idRoles)
                                {
                                    cmd.CommandText = "DELETE FROM UsersRolesAD WHERE IdRole = @IdRole AND Username = @Username";

                                    cmd.Parameters.Clear();
                                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = r;
                                    cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = u;
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        if (transaction != null)
                        {
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception e2)
                            {
                                throw new ProviderException(e2.Message);
                            }
                        }

                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }
        }

        public override bool RoleExists(string roleName)
        {
            this.Log("role:" + roleName, MethodBase.GetCurrentMethod().Name);

            if (this.RoleExistsSQLServer(roleName))
            {
                return true;
            }

            string fullyQualifiedRolename = this.GetFullyQualifiedRolename(roleName);
            return ((fullyQualifiedRolename != null) && !fullyQualifiedRolename.Equals(""));
        }

        private string[] search(string ldapConnString, string filter, string attribute)
        {
            this.Log("ldapConnString:" + ldapConnString + " filter:" + filter + " attribute:" + attribute, MethodBase.GetCurrentMethod().Name);

            string[] strArray2;
            DirectorySearcher searcher = new DirectorySearcher {
                SearchRoot = new DirectoryEntry(ldapConnString),
                Filter = filter
            };
            searcher.PropertiesToLoad.Clear();
            searcher.PropertiesToLoad.Add(attribute);
            searcher.PageSize = 1000;
            SearchResultCollection results = null;
            try
            {
                results = searcher.FindAll();
                string[] strArray = new string[results.Count];
                int index = 0;
                foreach (SearchResult result in results)
                {
                    strArray[index] = result.Properties[attribute].ToString();
                }
                strArray2 = strArray;
            }
            catch (Exception exception)
            {
                this.Log(exception.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException("Unable to search '" + filter + " Active directory at '" + ldapConnString + "'.", exception);
            }
            finally
            {
                if (results != null)
                {
                    results.Dispose();
                }
            }
            return strArray2;
        }

        private bool RoleExistsSQLServer(string rolename)
        {
            this.Log("role:" + rolename, MethodBase.GetCurrentMethod().Name);

            bool exists = false;
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM Roles WHERE Rolename = @Rolename", connection))
                    {
                        cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = rolename;

                        int count = (int)cmd.ExecuteScalar();
                        exists = count > 0;
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }

            return exists;
        }

        /// <summary>
        /// check if exists user SQL in roles SQL
        /// </summary>
        /// <param name="username">user name</param>
        /// <returns>true if exists</returns>
        private bool UserExistsSQLServer(string username)
        {
            this.Log("username:" + username, MethodBase.GetCurrentMethod().Name);

            bool exists = false;
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM Users WHERE Username = @Username", connection))
                    {
                        cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;

                        int count = (int)cmd.ExecuteScalar();
                        exists = count > 0;
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }

            this.Log("User exists: " + exists.ToString() , MethodBase.GetCurrentMethod().Name);

            return exists;
        }

        /// <summary>
        /// check if exists user AD in roles SQL
        /// </summary>
        /// <param name="username">user name</param>
        /// <returns>true if exists</returns>
        private bool UserExistsSQLServerAD(string username)
        {
            this.Log("username:" + username, MethodBase.GetCurrentMethod().Name);

            bool exists = false;
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM UsersRolesAD where Username = @Username", connection))
                    {
                        cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;

                        int count = (int)cmd.ExecuteScalar();
                        exists = count > 0;
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }

            return exists;
        }

        /// <summary>
        /// Gets and sets Application Name
        /// </summary>
        public override string ApplicationName
        {
            get
            {
                return this.applicationName;
            }
            set
            {
                this.applicationName = value;
            }
        }


        /// <summary>
        /// return list id users sql existing from list of users
        /// </summary>
        /// <param name="usernames">list of users</param>
        /// <returns>list of id users</returns>
        private int[] Users(string[] usernames)
        {
            this.Log("username:" + string.Join(",", usernames ), MethodBase.GetCurrentMethod().Name);

            List<int> idUser = new List<int>();
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    foreach (string u in usernames.Distinct())
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM Users WHERE Username = @Username", connection))
                        {
                            cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = u;
                            if (((int)cmd.ExecuteScalar()) == 0)
                            {
                                continue;
                            }
                        }

                        using (SqlCommand cmd = new SqlCommand("SELECT Id FROM Users WHERE Username = @Username", connection))
                        {
                            cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = u;
                            idUser.Add((int)cmd.ExecuteScalar());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }

            return idUser.ToArray();
        }

        /// <summary>
        /// return list id roles existing from list of roles
        /// </summary>
        /// <param name="rolenames">list of roles</param>
        /// <returns>list of id roles</returns>
        private int[] Roles(string[] rolenames)
        {
            this.Log("username:" + string.Join(",", rolenames), MethodBase.GetCurrentMethod().Name);

            List<int> idRoles = new List<int>();
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                    foreach (string r in rolenames.Distinct())
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM Roles WHERE Rolename = @Rolename", connection))
                        {
                            cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = r;

                            if (((int)cmd.ExecuteScalar()) == 0)
                            {
                                continue;
                            }
                        }

                        using (SqlCommand cmd = new SqlCommand("SELECT Id FROM Roles WHERE Rolename = @Rolename", connection))
                        {
                            cmd.Parameters.Add("@Rolename", SqlDbType.NVarChar).Value = r;

                            idRoles.Add((int)cmd.ExecuteScalar());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }

            return idRoles.ToArray();
        }


        private void Log(string message, string method)
        {
            if (1 == 0)
            {
                StringBuilder log = new StringBuilder();
                log.AppendFormat("Role Provider - {0} - {1} - {2}{3}", DateTime.Now.ToString(), message, method, Environment.NewLine);
                File.AppendAllText(@"F:\arcgisDev\ProviderAGS\AGSMixMembershipProvider\bin\Debug\log.txt", log.ToString());
            }
        }

    }
}

