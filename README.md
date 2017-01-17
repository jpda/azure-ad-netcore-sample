# azure-ad-netcore-sample

Simple sample of an ASP.net Core/.NET Core v1.0.1 with Azure AD authentication + adding custom claims before cookie issuance.

## Things you need to do
- [Create an Azure AD application] (https://portal.azure.com/#blade/Microsoft_AAD_IAM/ApplicationsListBlade)
- Update [appsettings.json] (https://github.com/jpda/azure-ad-netcore-sample/blob/master/src/azure-ad-netcore-sample/appsettings.json) with your app's Client ID, Tenant ID and Tenant name
- (optional) Add your own custom claims in the `OnTokenValidated` event in `Startup.cs::Configure`

### Update 1/17/17 - updated group name resolution
Group names are now resolved from the AAD Graph (*not* the [Microsoft Graph] (https://graph.microsoft.io)). This update is changed to use the user's access to access the AAD Graph.

For admin consent, construct a URL similar to below - prompt=admin_consent is required to prevent users from application issues rising from not being able to consent.

```
https://login.microsoftonline.com/<YOUR TENANT ID>/oauth2/authorize?client_id=<YOUR APPLICATION/CLIENT ID>&response_type=code&redirect_uri=<AAD-REGISTERED URL-ENCODED RETURN URI>&response_mode=query&resource=https%3A%2F%2Fgraph.windows.net%2F&state=12345&prompt=admin_consent
```
This will resolve group names via the Microsoft Graph, making legacy code easier to work with a la `[Authorize(Role="Group Name")].` For this to work, you will need to:
- Add a client secret to your application
- Add the secret to the configuration (I've called it ClientSecret)
- Add Windows Azure Active Directory to the Required Permissions list, including 'Access the directory as the signed-in user' and 'Sign-in user and read profile.'

### Update 1/12/17 - added group-to-role transformation
This will add your AAD Groups as Role claims on the ticket, making it easier to do group-based authorization decisions. This copies the group GUID in a new claim of type `Role.` 
Make sure you add `groupMembershipClaims` to the Azure AD App's manifest to enable the claims to be sent.
