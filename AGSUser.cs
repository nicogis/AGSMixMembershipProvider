namespace AGSMixMembershipProvider
{
    using ESRI.ArcGIS.Server;
    using System;
    using System.Runtime.InteropServices;

    [ComVisible(true), Guid("936CF3EB-05DA-4b1b-9F64-6997B03436A0")]
    internal class AGSUser : IUser
    {
        private string _description;
        private string _email;
        private string _fullname;
        private string _password;
        private string _secretAnswer;
        private string _secretQuestion;
        private string _username;

        string IUser.GetDescription() => 
            this._description;

        string IUser.GetEmail() => 
            this._email;

        string IUser.GetFullname() => 
            this._fullname;

        string IUser.GetPassword() => 
            this._password;

        string IUser.GetSecretAnswer() => 
            this._secretAnswer;

        string IUser.GetSecretQuestion() => 
            this._secretQuestion;

        string IUser.GetUsername() => 
            this._username;

        void IUser.SetDescription(string Description)
        {
            this._description = Description;
        }

        void IUser.SetEmail(string email)
        {
            this._email = email;
        }

        void IUser.SetFullname(string fullname)
        {
            this._fullname = fullname;
        }

        void IUser.SetPassword(string Password)
        {
            this._password = Password;
        }

        void IUser.SetSecretAnswer(string secretAnswer)
        {
            this._secretAnswer = secretAnswer;
        }

        void IUser.SetSecretQuestion(string secretQuestion)
        {
            this._secretQuestion = secretQuestion;
        }

        void IUser.SetUsername(string UserName)
        {
            this._username = UserName;
        }
    }
}

