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
    using System.Text;
    using System.IO;
    using System.Reflection;

    [Guid("61BF53AC-6AD1-4430-A325-DC8A2219F5E1"), ComVisible(true)]
    public class AGSMembershipProviderWrapper : IUserStore
    {
        private MembershipProvider _innerMemProvider;
        public readonly string AD_MEM_PROVIDER_CLASS = "AGSMixMembershipProvider.AGSMixMembershipProvider";
        public readonly string AD_MEM_PROVIDER_SHORTNAME = "AGSMixMembershipProvider";
        public readonly string CLASS_PROP = "class";
        public readonly string SQL_MEM_PROVIDER_CLASS = "System.Web.Security.SqlMembershipProvider";
        public readonly string SQL_MEM_PROVIDER_SHORTNAME = "SqlMembershipProvider";

        private void assertInitialized()
        {
            this.Log(MethodBase.GetCurrentMethod().Name, MethodBase.GetCurrentMethod().Name);

            if (this._innerMemProvider == null)
            {
                throw new Exception("Initialize function has not been invoked on this object.");
            }
        }

        private IUser CreateIUserFromMembershipUser(MembershipUser user)
        {
            this.Log(user.UserName, MethodBase.GetCurrentMethod().Name);

            Util.CheckForNull("user", user);
            IUser user2 = new AGSUser();
            user2.SetUsername(user.UserName);
            user2.SetFullname(user.Comment);
            user2.SetEmail(user.Email);
            user2.SetSecretQuestion(user.PasswordQuestion);
            return user2;
        }

        void IUserStore.AddUser(IUser pUser)
        {
            this.Log(pUser.GetUsername(), MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("pUser", pUser);
            MembershipCreateStatus success = MembershipCreateStatus.Success;
            if (this._innerMemProvider.CreateUser(pUser.GetUsername(), pUser.GetPassword(), pUser.GetEmail(), pUser.GetSecretQuestion(), pUser.GetSecretAnswer(), true, Guid.NewGuid(), out success) == null)
            {
                throw new Exception(this.GetErrorMessage(success));
            }
        }

        void IUserStore.DeleteUser(string userName)
        {
            this.Log(userName, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("userName", userName);
            this._innerMemProvider.DeleteUser(userName, true);
        }

        bool IUserStore.GetAllUsers(string filter, int maxCount, out IUser[] users)
        {
            this.Log("filter" + filter, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            int totalRecords = -1;
            MembershipUserCollection users2 = this._innerMemProvider.FindUsersByName(filter, 0, maxCount, out totalRecords);
            users = new AGSUser[users2.Count];
            int num2 = 0;
            foreach (MembershipUser user in users2)
            {
                users[num2++] = this.CreateIUserFromMembershipUser(user);
            }
            return (totalRecords > users2.Count);
        }

        bool IUserStore.GetAllUsersPaged(int startIndex, int pageSize, out IUser[] users)
        {
            this.Log("startIndex" + startIndex, MethodBase.GetCurrentMethod().Name);

            if (pageSize <= 0)
            {
                throw new Exception("Page size cannot be zero.");
            }
            this.assertInitialized();
            int pageIndex = startIndex / pageSize;
            int totalRecords = 0;
            LinkedList<IUser> source = new LinkedList<IUser>();
            int num3 = startIndex % pageSize;
            MembershipUserCollection users2 = this._innerMemProvider.GetAllUsers(pageIndex, pageSize, out totalRecords);
            int num4 = 0;
            foreach (MembershipUser user in users2)
            {
                if (num4++ >= num3)
                {
                    source.AddLast(this.CreateIUserFromMembershipUser(user));
                }
            }
            if (num3 > 0)
            {
                users2 = this._innerMemProvider.GetAllUsers(pageIndex + 1, pageSize, out totalRecords);
                int num5 = num3;
                foreach (MembershipUser user2 in users2)
                {
                    if (num5-- <= 0)
                    {
                        break;
                    }
                    source.AddLast(this.CreateIUserFromMembershipUser(user2));
                }
            }
            users = source.ToArray<IUser>();
            return ((totalRecords - (startIndex + source.Count)) > 0);
        }

        int IUserStore.GetTotalUsers()
        {
            this.Log(MethodBase.GetCurrentMethod().Name, MethodBase.GetCurrentMethod().Name);
            int totalRecords = 0;
            this._innerMemProvider.GetAllUsers(0, 0x3e8, out totalRecords);
            return totalRecords;
        }

        IUser IUserStore.GetUser(string userName)
        {
            this.Log("username:" + userName, MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            Util.CheckForNull("userName", userName);
            MembershipUser user = this._innerMemProvider.GetUser(userName, false);
            if (user != null)
            {
                return this.CreateIUserFromMembershipUser(user);
            }
            return null;
        }

        void IUserStore.Initialize(IPropertySet pProps)
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

        bool IUserStore.IsReadOnly() => 
            false;

        void IUserStore.TestConnection(IPropertySet pProps)
        {
            IUserStore store = new AGSMembershipProviderWrapper();
            store.Initialize(pProps);
        }

        void IUserStore.UpdateUser(IUser pUser)
        {
            this.Log("username:" + pUser.GetUsername(), MethodBase.GetCurrentMethod().Name);

            this.assertInitialized();
            MembershipUser user = this._innerMemProvider.GetUser(pUser.GetUsername(), false);
            if (user == null)
            {
                throw new Exception("Could not find user with user name '" + pUser.GetUsername() + "' in the user store.");
            }
            if (!user.PasswordQuestion.Equals(pUser.GetSecretQuestion()))
            {
                string password = pUser.GetPassword();
                if ((password == null) || password.Equals(""))
                {
                    throw new Exception("To change secret question and answer, original password must be provided.");
                }
                user.ChangePasswordQuestionAndAnswer(pUser.GetPassword(), pUser.GetSecretQuestion(), pUser.GetSecretAnswer());
                user = this._innerMemProvider.GetUser(pUser.GetUsername(), false);
            }
            user.Email = pUser.GetEmail();
            this._innerMemProvider.UpdateUser(user);
        }

        bool IUserStore.ValidateUser(string userName, string password)
        {
            this.Log("username:" + userName, MethodBase.GetCurrentMethod().Name);
            this.assertInitialized();
            return this._innerMemProvider.ValidateUser(userName, password);
        }

        private string GetErrorMessage(MembershipCreateStatus status)
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

        public void Initialize(string name, NameValueCollection config)
        {
            this.Log("name:" + name, MethodBase.GetCurrentMethod().Name);

            if (name.Equals(this.SQL_MEM_PROVIDER_CLASS) || name.Equals(this.SQL_MEM_PROVIDER_SHORTNAME))
            {
                Console.WriteLine("Creating an instance of SqlMembershipProvider");
                this._innerMemProvider = new AGSSqlMembershipProvider();
                Console.WriteLine("SqlMembershipProvider instantiated successfully.");
            }
            else if (name.Equals(this.AD_MEM_PROVIDER_CLASS) || name.Equals(this.AD_MEM_PROVIDER_SHORTNAME))
            {
                Console.WriteLine("Creating an instance of AGSMixMembershipProvider");
                this._innerMemProvider = new AGSMixMembershipProvider();
                Console.WriteLine("AGSMixMembershipProvider instantiated successfully.");
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
                Console.WriteLine("Creating instance of MembershipProvider class '" + type + "'.");
                object obj2 = Activator.CreateInstance(type);
                if ((obj2 == null) || !(obj2 is MembershipProvider))
                {
                    string str2 = "Instance of class '" + name + "' could not be created or class does not extend MembershipProvider.";
                    Console.WriteLine(str2);
                    throw new Exception(str2);
                }
                this._innerMemProvider = (MembershipProvider) obj2;
            }
            Console.WriteLine("Initializing MembershipProvider.");
            this._innerMemProvider.Initialize(name, config);
            Console.WriteLine("MembershipProvider initialized successfully.");
            this._innerMemProvider.ApplicationName = "esriags";
            Util.AddToProviders(this._innerMemProvider, name);
        }

        private void Log(string message, string method)
        {
            if (1 == 0)
            {
                StringBuilder log = new StringBuilder();
                log.AppendFormat("Membership Provider Wrapper - {0} - {1} - {2}{3}", DateTime.Now.ToString(), message, method, Environment.NewLine);
                File.AppendAllText(@"F:\arcgisDev\ProviderAGS\AGSMixMembershipProvider\bin\Debug\log.txt", log.ToString());
            }
        }
    }
}

