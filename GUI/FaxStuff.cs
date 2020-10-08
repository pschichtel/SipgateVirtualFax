using System;
using System.Windows;
using CredentialManagement;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public class FaxStuff
    {
        public const string CredentialName = "sipgate-fax"; 
        public static readonly FaxStuff Instance = new FaxStuff();
        
        public SipgateFaxClient FaxClient { get; private set; }
        public FaxScheduler FaxScheduler { get; private set; }

        public FaxStuff()
        {
            var credential = LookupCredential();
            FaxClient = new SipgateFaxClient(credential.Username, credential.Password);
            FaxScheduler = new FaxScheduler(FaxClient);
        }
        
        private static Credential LookupCredential()
        {
            var credential = new Credential { Target = CredentialName };
            if (!credential.Load())
            {
                MessageBox.Show(string.Format(Properties.Resources.Err_CredentialsNotFound, CredentialName));
                throw new Exception("Missing credential!");
            }

            return credential;
        }
    }
}