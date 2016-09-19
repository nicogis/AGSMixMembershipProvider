# ArcGIS Server Mix Provider
Mix Provider for ArcGIS Server

# Requirements

ArcGIS Server 10.4 or superior

# Description

Store ArcGIS Server users and roles in a SQL Server security store and use also users Active Directory


# Installation

- Install the mix provider .dll into the GAC.

>gacutil /i AGSMixMembershipProvider.dll

- Create a db in SQL Server/Express and run AGSMixMembershipProvider.sql in folder Support. Change in first row the name of your db

- Open the ArcGIS Server Administrator Directory and log in with a user who has administrative permissions to your site.
The Administrator Directory is typically available at http://gisserver.domain.com:6080/arcgis/admin.
Click security > config > updateIdentityStore.
Copy and paste the following text into the User Store Configuration dialog box on the Operation - updateIdentityStore page.


```<language>
{
    "type": "ASP_NET",
    "class": "AGSMixMembershipProvider.AGSMixMembershipProvider,AGSMixMembershipProvider,Version=1.0.0.0,Culture=Neutral,PublicKeyToken=4005576dfac9a17f",
    "properties": {
    "connectionStringName": "Data Source=.\\SQLEXPRESS;Initial Catalog=YourDB;User Id=UserDB;Password=PwdDB;",
    "passwordAD": "myPwdUserDomain",
    "usernameAD": "mydomain\\myUsernameUserDomain"
    }
}
```

Update the user, password, name database and datasource values in property connectionStringName. Update user, domain and password for user that has privileges for browser AD.
Copy and paste the following text into the Role Store Configuration dialog box on the Operation - updateIdentityStore page.


```<language>
{
        "type": "ASP_NET",
        "class": "AGSMixMembershipProvider.AGSMixRoleProvider,AGSMixMembershipProvider,Version=1.0.0.0,Culture=Neutral,PublicKeyToken=4005576dfac9a17f",
        "properties": {
        "connectionStringName": "Data Source=.\\SQLEXPRESS;Initial Catalog=YourDB;User Id=UserDB;Password=PwdDB;",
        "passwordAD": "myPwdUserDomain",
        "usernameAD": "mydomain\\myUsernameUserDomain",
        "useRolesDBforAD": "true"
        }
}
```

 Update the user, password, name database and datasource values in property connectionStringName. Update user, domain and password for user that has privileges for browser AD.
 Property useRolesDBforAD is true if you need also store Roles in sql server for users AD besides Roles in AD

-   Click Update to save your configuration.

-   Install two web adaptor in IIS. In the first enable only WA and in the second only Basic Authentication

-   For basic authentication you need create windows local users and add users in db sql server (username: 'namemachine\nameuser')

-   Add Role Provider in web.config of web adaptor in basic Authentication. If you need create also roles in sql server for user AD add it also in web.config of web adaptor WA ( useRolesDBforAD: true)



```<language>
<roleManager enabled="true" defaultProvider="AGSMixMembershipProvider">
    <providers>
        <clear />
        <!-- start this block-->
        <add name="AGSMixMembershipProvider" type="AGSMixMembershipProvider.AGSMixRoleProvider,AGSMixMembershipProvider,Version=1.0.0.0,Culture=Neutral,PublicKeyToken=4005576dfac9a17f"
                connectionStringName="Data Source=.\SQLEXPRESS;Initial Catalog=AGSMixMembershipProvider;User Id=UserDB;Password=PwdDB"
                passwordAD="myPwdUserDomain" usernameAD="mydomain\myUsernameUserDomain" useRolesDBforAD="true" />
        <!-- end this block-->
        <add name="AspNetWindowsTokenRoleProvider" applicationName="arcgis" type="System.Web.Security.WindowsTokenRoleProvider, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" />
    </providers>
</roleManager>
```









