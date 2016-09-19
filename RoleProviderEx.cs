namespace AGSMixMembershipProvider
{
    using System.Web.Security;

    public abstract class RoleProviderEx : RoleProvider
    {
        protected RoleProviderEx()
        {
        }

        public abstract string[] GetAllRoles(string rolenameToMatch);
        public abstract string GetFullyQualifiedRolename(string rolename);
    }
}

