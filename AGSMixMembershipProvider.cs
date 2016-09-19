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
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Web.Security;

    public class AGSMixMembershipProvider : MembershipProvider
    {
        private string userNameAD = null;
        private string passwordAD = null;
        private string connectionString = null;

        private string applicationName;
        private string currentDomainName;
        private string providerName;
        private Dictionary<string, string> dnsroot2NETBIOSName = new Dictionary<string, string>();
        private Dictionary<string, string> ncname2NETBIOSName = new Dictionary<string, string>();

        public override bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            throw new NotImplementedException();
        }

        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            throw new NotImplementedException();
        }

        public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            this.Log("username:" + username, MethodBase.GetCurrentMethod().Name);

            MembershipUser newUser = null;
            try
            {
                newUser = this.GetUser(username, false);
                if (newUser == null)
                {
                    using (SqlConnection connection = new SqlConnection(this.connectionString))
                    {
                        using (SqlCommand cmd = new SqlCommand("INSERT INTO Users (Username, Password) VALUES (@Username, @Password)", connection))
                        {
                            cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;
                            cmd.Parameters.Add("@Password", SqlDbType.NVarChar).Value = password;
                            connection.Open();

                            int recordAdded = cmd.ExecuteNonQuery();

                            if (recordAdded > 0)
                            {
                                status = MembershipCreateStatus.Success;
                                newUser = this.GetUser(username);
                            }
                            else
                            {
                                status = MembershipCreateStatus.UserRejected;
                            }
                        }
                    }
                }
                else
                {
                    status = MembershipCreateStatus.DuplicateUserName;
                }
            }
            catch
            {
                status = MembershipCreateStatus.ProviderError;
            }

            if (status != MembershipCreateStatus.Success)
            {
                this.Log(AGSMixMembershipProvider.GetErrorMessage(status), MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(AGSMixMembershipProvider.GetErrorMessage(status));
            }

            return newUser;
        }

        /// <summary>
        /// MembershipUser from username
        /// </summary>
        /// <param name="userName">value of username</param>
        /// <returns>object MembershipUser</returns>
        private MembershipUser GetUser(string userName)
        {
            this.Log("username:" + userName, MethodBase.GetCurrentMethod().Name);

            return new MembershipUser(
                this.providerName,
                userName,
                null,
                null,
                "Secret Question",
                userName,
                true,
                false,
                DateTime.Now,               // creationDate
                DateTime.Now,               // lastLoginDate
                DateTime.Now,               // lastActivityDate
                DateTime.Now,               // lastPasswordChangedDate
                new DateTime(2000, 1, 1));    // lastLockoutDate
        }

        /// <summary>
        /// error from status
        /// </summary>
        /// <param name="status">object MembershipCreateStatus</param>
        /// <returns>error from MembershipCreateStatus</returns>
        private static string GetErrorMessage(MembershipCreateStatus status)
        {
            switch (status)
            {
                case MembershipCreateStatus.InvalidUserName:
                    return "The user name provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidPassword:
                    return "The password provided is invalid. Please enter a valid password value.";

                case MembershipCreateStatus.InvalidQuestion:
                    return "The password retrieval question provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidAnswer:
                    return "The password retrieval answer provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.InvalidEmail:
                    return "The e-mail address provided is invalid. Please check the value and try again.";

                case MembershipCreateStatus.DuplicateUserName:
                    return "Username already exists. Please enter a different user name.";

                case MembershipCreateStatus.DuplicateEmail:
                    return "A username for that e-mail address already exists. Please enter a different e-mail address.";

                case MembershipCreateStatus.UserRejected:
                    return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

                case MembershipCreateStatus.ProviderError:
                    return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
            }

            return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
        }

        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            this.Log("username:" + username, MethodBase.GetCurrentMethod().Name);

            int rowsAffected = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM Users WHERE Username = @Username", connection))
                    {
                        cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;
                        connection.Open();

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

        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            throw new NotImplementedException();
        }

        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            this.Log("username:" + usernameToMatch, MethodBase.GetCurrentMethod().Name);

            if ((pageIndex < 0) || (pageSize < 0))
            {
                throw new ArgumentException("Parameters pageIndex and pageSize cannot be negative.");
            }
            if (pageSize < 1)
            {
                throw new ArgumentException("Parameter pageSize should be greater than or equal to 1.");
            }
            MembershipUserCollection users = new MembershipUserCollection();
            int num = 0;

            MembershipUser user = null;
            totalRecords = -1;
            int partialTotalRecords;
            string domain = null;
            string name = null;
            try
            {
                ADUtil.splitDomainAndName(usernameToMatch, out domain, out name);
                if ((domain == null) || domain.Equals(""))
                {
                    domain = this.dnsroot2NETBIOSName[this.currentDomainName];
                }
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, null, domain, this.userNameAD, this.passwordAD))
                {
                    using (UserPrincipal principal = new UserPrincipal(context))
                    {
                        principal.SamAccountName = name + "*";
                        using (PrincipalSearcher searcher = new PrincipalSearcher())
                        {
                            searcher.QueryFilter = principal;
                            using (PrincipalSearchResult<Principal> result = searcher.FindAll())
                            {
                                //users = new MembershipUserCollection();
                                
                                try
                                {
                                    foreach (UserPrincipal principal2 in result)
                                    {
                                        if (principal2 != null)
                                        {
                                            num++;
                                            if (num > ((pageIndex + 1) * pageSize))
                                            {
                                                totalRecords = result.Count<Principal>();
                                                return users;
                                            }
                                            if (num > (pageIndex * pageSize))
                                            {
                                                if ((principal2.DistinguishedName != null) && !principal2.DistinguishedName.Equals(""))
                                                {
                                                    user = new MembershipUser(this.providerName, this.ncname2NETBIOSName[ADUtil.getDCcomponent(principal2.DistinguishedName)] + @"\" + principal2.SamAccountName, null, principal2.EmailAddress, null, principal2.Name, false, false, DateTime.Now, DateTime.Now, DateTime.Now, DateTime.Now, DateTime.Now);
                                                    users.Add(user);
                                                }
                                                principal2.Dispose();
                                            }
                                        }
                                    }                                   
                                }
                                catch (Exception ex)
                                {
                                    throw new ProviderException("Finding users by name using filter '" + usernameToMatch + "' failed." + ex.Message, ex);
                                }

                                partialTotalRecords = result.Count<Principal>();
                                //return users;
                            }
                        }
                    }
                }
            }
            catch(Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw;
            }

            //sql server
            
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM Users WHERE Username LIKE @Username", connection))
                    {
                        cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = string.Format(CultureInfo.InvariantCulture, "%{0}%", usernameToMatch);
                        connection.Open();
                        int sqlRecords = (int)cmd.ExecuteScalar();

                        if (sqlRecords <= 0)
                        {
                            totalRecords = partialTotalRecords;
                            return users;
                        }
                        else
                        {
                            totalRecords = partialTotalRecords + sqlRecords;
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("SELECT Id, Username FROM Users WHERE Username LIKE @Username ORDER BY Username ASC", connection))
                    {
                        cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = string.Format(CultureInfo.InvariantCulture, "%{0}%", usernameToMatch);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    num++;
                                    if (num > ((pageIndex + 1) * pageSize))
                                    {
                                        break;
                                    }
                                    if (num > (pageIndex * pageSize))
                                    {
                                        MembershipUser u = this.GetUserByReader(reader);
                                        users.Add(u);
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

            return users;
        }

        /// <summary>
        /// MembershipUser from data reader
        /// </summary>
        /// <param name="reader">object SQL data reader</param>
        /// <returns>object MembershipUser</returns>
        private MembershipUser GetUserByReader(SqlDataReader reader)
        {
            string userName = reader.GetString(1);
            return this.GetUser(userName);
        }

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            this.Log("index " + pageIndex.ToString(), MethodBase.GetCurrentMethod().Name);

            MembershipUserCollection users = null;
            try
            {
                users = this.FindUsersByName("", pageIndex, pageSize, out totalRecords);
            }
            catch (Exception exception)
            {
                this.Log(exception.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException("Getting all users failed." + exception.Message, exception);
            }

            return users;
        }

        public override int GetNumberOfUsersOnline()
        {
            throw new NotImplementedException();
        }

        public override string GetPassword(string username, string answer)
        {
            throw new NotImplementedException();
        }

        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            throw new NotImplementedException();
        }

        public override MembershipUser GetUser(string username, bool userIsOnline)
        {

            this.Log("username " + username, MethodBase.GetCurrentMethod().Name);

            if (this.UserExistsSQLServer(username))
            {
                MembershipUser u = null;

                try
                {
                    using (SqlConnection connection = new SqlConnection(this.connectionString))
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT Id, Username FROM Users WHERE Username = @Username", connection))
                        {
                            cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;
                            connection.Open();

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    reader.Read();
                                    u = this.GetUserByReader(reader);
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

                return u;
            }


            string domain = null;
            string name = null;
            MembershipUser user = null;
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
                        if (principal != null)
                        {
                            user = new MembershipUser(this.providerName, this.ncname2NETBIOSName[ADUtil.getDCcomponent(principal.DistinguishedName)] + @"\" + principal.SamAccountName, null, principal.EmailAddress, null, principal.Name, false, false, DateTime.Now, DateTime.Now, DateTime.Now, DateTime.Now, DateTime.Now);
                        }
                    }
                    return user;
                }
            }
            catch (Exception exception)
            {
                this.Log(exception.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException("Getting user '" + username + "' failed." + exception.Message, exception);
            }

        }

        private bool UserExistsSQLServer(string username)
        {
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

            this.Log("username " + username + " - exists:" + exists.ToString(), MethodBase.GetCurrentMethod().Name);

            return exists;

        }

        public override string GetUserNameByEmail(string email)
        {
            throw new NotImplementedException();
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            this.Log("name " + name, MethodBase.GetCurrentMethod().Name);

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

            // test for connection
            try
            {
                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();
                }
            }
            catch
            {
                throw new ProviderException("Check your DB connection!");
            }

            try
            {
                base.Initialize(name, config);

                this.currentDomainName = ADUtil.getDomainName();
                this.populateDomainNamesConversionMaps();

                this.providerName = name;
            }
            catch(Exception ex)
            {
                this.Log(ex.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(ex.Message);
            }

           
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
            catch (Exception ex)
            {
                this.Log(ex.Message, MethodBase.GetCurrentMethod().Name);
                throw ex;
            }
        }

        public override string ResetPassword(string username, string answer)
        {
            throw new NotImplementedException();
        }

        public override bool UnlockUser(string userName)
        {
            throw new NotImplementedException();
        }

        public override void UpdateUser(MembershipUser user)
        {
            throw new NotImplementedException();
        }

        public override bool ValidateUser(string username, string password)
        {
            if (this.UserExistsSQLServer(username))
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(this.connectionString))
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT Count(*) FROM Users WHERE Username = @Username AND Password = @Password", connection))
                        {
                            cmd.Parameters.Add("@Username", SqlDbType.NVarChar).Value = username;
                            cmd.Parameters.Add("@Password", SqlDbType.NVarChar).Value = password;
                            connection.Open();

                            return ((int)cmd.ExecuteScalar()) > 0 ;
                        }
                    }
                }
                catch (Exception e)
                {
                    this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                    throw new ProviderException(e.Message);
                }

            }

            bool flag = false;
            string domain = null;
            string name = "";
            try
            {
                ADUtil.splitDomainAndName(username, out domain, out name);
                if ((domain == null) || domain.Equals(""))
                {
                    domain = this.dnsroot2NETBIOSName[this.currentDomainName];
                }
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, null, domain, this.userNameAD, this.passwordAD))
                {
                    flag = context.ValidateCredentials(name, password);
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, MethodBase.GetCurrentMethod().Name);
                throw new ProviderException(e.Message);
            }
            return flag;
        }

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

        public override bool EnablePasswordReset
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool EnablePasswordRetrieval
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override int MaxInvalidPasswordAttempts
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override int MinRequiredNonAlphanumericCharacters
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override int MinRequiredPasswordLength
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override int PasswordAttemptWindow
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override MembershipPasswordFormat PasswordFormat
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override string PasswordStrengthRegularExpression
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool RequiresQuestionAndAnswer
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override bool RequiresUniqueEmail
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        private void Log(string message, string method)
        {
            if (1 == 0)
            {
                StringBuilder log = new StringBuilder();
                log.AppendFormat("Membership Provider - {0} - {1} - {2}{3}", DateTime.Now.ToString(), message, method, Environment.NewLine);
                File.AppendAllText(@"F:\arcgisDev\ProviderAGS\AGSMixMembershipProvider\bin\Debug\log.txt", log.ToString());
            }
        }

    }
}

