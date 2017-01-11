# azure-ad-netcore-sample

Simple sample of an ASP.net Core/.NET Core v1.0.1 with Azure AD authentication + adding custom claims before cookie issuance.

## Things you need to do
- [Create an Azure AD application] (https://portal.azure.com/#blade/Microsoft_AAD_IAM/ApplicationsListBlade)
- Update [appsettings.json] (https://github.com/jpda/azure-ad-netcore-sample/blob/master/src/azure-ad-netcore-sample/appsettings.json) with your app's Client ID, Tenant ID and Tenant name
- (optional) Add your own custom claims in the `OnTokenValidated` event in `Startup.cs::Configure`
