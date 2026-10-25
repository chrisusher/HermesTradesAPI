# GitHub Actions deployment

The `Deploy TEST Function App` workflow deploys the `Backend` Function App after a push to `main`, or when started manually. It reads deployment settings from the GitHub `TEST` environment.

## Create `AZURE_CREDENTIALS`

The workflow signs in to Azure with a service principal. Create one with the minimum built-in role needed for App Service deployments, scoped to the Function App resource:

1. Sign in to Azure CLI and select the subscription that contains the Function App:

   ```bash
   az login
   az account set --subscription "<subscription-id>"
   ```

2. Create the service principal. Replace the resource group and Function App placeholders with the values for the TEST app:

   ```bash
   az ad sp create-for-rbac \
     --role "Website Contributor" \
     --scopes "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.Web/sites/<function-app-name>" \
     --json-auth
   ```

   The command prints a JSON object containing the service principal credentials and Azure tenant, subscription, and client IDs. Keep the output available for the next step; the client secret cannot be retrieved later. The person running this command needs permission to create a service principal and assign a role at the Function App scope. The command omits a display name so it does not accidentally match and modify a service principal created by an earlier run.

3. In GitHub, open the repository's **Settings > Environments > TEST**. Create the `TEST` environment if needed. Under **Environment secrets**, choose **Add secret**, name it `AZURE_CREDENTIALS`, and paste the complete JSON object from the command as its value.

The workflow also expects these secrets in the same `TEST` environment:

- `AZURE_FUNCTIONAPP_NAME` - the Function App name.
- `AZURE_RESOURCE_GROUP` - the resource group containing it.

Keep the credential JSON in GitHub's secret store; do not commit it to the repository. Rotate the service principal credential and update the secret when it expires or is replaced.

For details, see Microsoft's [GitHub Actions deployment guidance for App Service](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions) and [Azure CLI service principal command reference](https://learn.microsoft.com/en-us/cli/azure/ad/sp).
