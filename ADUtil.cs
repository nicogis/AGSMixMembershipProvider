namespace AGSMixMembershipProvider
{
    using System;
    using System.Runtime.InteropServices;

    internal class ADUtil
    {
        internal static string getDCcomponent(string distinguishedName)
        {
            int index = distinguishedName.IndexOf("DC=");
            string str = distinguishedName.Substring(index);
            if ((str != null) && !str.Equals(""))
            {
                return str;
            }
            return null;
        }

        internal static string getDomainName()
        {
            IntPtr zero = IntPtr.Zero;
            NetApi.DsGetDcName(null, null, null, null, 0, out zero);
            NetApi.DOMAIN_CONTROLLER_INFO domain_controller_info = (NetApi.DOMAIN_CONTROLLER_INFO) Marshal.PtrToStructure(zero, typeof(NetApi.DOMAIN_CONTROLLER_INFO));
            NetApi.NetApiBufferFree(zero);
            zero = IntPtr.Zero;
            return domain_controller_info.DomainName;
        }

        internal static void splitDomainAndName(string domainQualifiedName, out string domain, out string name)
        {
            string[] strArray = domainQualifiedName.Split(new char[] { '\\' });
            if (strArray.Length == 1)
            {
                domain = "";
                name = strArray[0];
            }
            else
            {
                if (strArray.Length != 2)
                {
                    throw new Exception("Invalid string for domainQualifiedName.");
                }
                domain = strArray[0];
                name = strArray[1];
            }
        }

        private class NetApi
        {
            [DllImport("netapi32.dll", CharSet=CharSet.Auto)]
            public static extern int DsGetDcName(string ComputerName, string DomainName, [In] GuidAsClass DomainGuid, string SiteName, int Flags, out IntPtr pDCI);
            [DllImport("netapi32.dll", ExactSpelling=true)]
            public static extern int NetApiBufferFree(IntPtr bufptr);

            [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
            public struct DOMAIN_CONTROLLER_INFO
            {
                public string DomainControllerName;
                public string DomainControllerAddress;
                public int DomainControllerAddressType;
                public Guid DomainGuid;
                public string DomainName;
                public string DnsForestName;
                public int Flags;
                public string DcSiteName;
                public string ClientSiteName;
            }

            [StructLayout(LayoutKind.Sequential)]
            public class GuidAsClass
            {
                public Guid DomainGuid;
            }
        }
    }
}

