namespace AGSMixMembershipProvider
{
    using ESRI.ArcGIS.Server;
    using System;
    using System.Runtime.InteropServices;

    [ComVisible(true), Guid("922E937F-8692-42c9-B704-5C384A2B1329")]
    internal class AGSRole : IRole
    {
        private string _description;
        private string _rolename;

        string IRole.GetDescription() => 
            this._description;

        string IRole.GetRolename() => 
            this._rolename;

        void IRole.SetDescription(string description)
        {
            this._description = description;
        }

        void IRole.SetRolename(string rolename)
        {
            this._rolename = rolename;
        }
    }
}

