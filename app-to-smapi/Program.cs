using System;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace app_to_smapi
{
    class Program
    {
        static void Main(string[] args)
        {
            var a = new DoThing();
            a.Go();
            Console.ReadLine();
        }
    }

    public class DoThing
    {
        private string cid = "<your client id>";
        private string key = "<client secret>";
        private string rid = "https://management.azure.com/"; //for resource manager
        private string subid = "<subscription id>";
        private string rgName = "<resource group name>";
        private string azureAdTenantName = "your aad tenant";

        public void Go()
        {
            var ac = new AuthenticationContext($"https://login.microsoftonline.com/{azureAdTenantName}/oauth2/token");
            var token = ac.AcquireTokenAsync(rid, new ClientCredential(cid, key)).Result;

            var root = $"https://management.azure.com/subscriptions/{subid}/resourceGroups/{rgName}?api-version=2016-09-01";

            var a = new System.Net.Http.HttpClient();
            a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            var stuff = a.GetStringAsync(root).Result;

            Console.WriteLine(stuff);
        }
    }
}