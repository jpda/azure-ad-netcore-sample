
# azure-ad-netcore-sample

Simple sample of an ASP.net Core/.NET Core v1.0.1 with Azure AD authentication + adding custom claims before cookie issuance.



## Things you need to do
- [Create an Azure AD application] (https://portal.azure.com/#blade/Microsoft_AAD_IAM/ApplicationsListBlade)
- Update [appsettings.json] (https://github.com/jpda/azure-ad-netcore-sample/blob/master/src/azure-ad-netcore-sample/appsettings.json) with your app's Client ID, Tenant ID and Tenant name
- (optional) Add your own custom claims in the `OnTokenValidated` event in `Startup.cs::Configure`

### Update 1/12/17 - added group-to-role transformation
This will add your AAD Groups as Role claims on the ticket, making it easier to do group-based authorization decisions. This copies the group GUID in a new claim of type `Role.` 
Make sure you add `groupMembershipClaims` to the Azure AD App's manifest to enable the claims to be sent.

#### Resolving group names via the Graph
It also resolves group names via the Microsoft Graph, making legacy code easier to work with a la `[Authorize(Role="Group Name")].` For this to work, you will need to:
- Add a client secret to your application
- Add the secret to the configuration (I've called it ClientSecret)
- Make sure your Azure AD App's permissions include 'Group.Read.All' - this will *require* AAD Admin consent
