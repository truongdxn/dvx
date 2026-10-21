namespace Contoso.Plugins
{
    public static class Account
    {
        public const string EntityLogicalName = "account";
        public const string PrimaryIdAttribute = "accountid";
        public const string PrimaryNameAttribute = "name";

        public static class Fields
        {
            public const string Name = "name";
            public const string CreditLimit = "creditlimit";
        }
    }
}
