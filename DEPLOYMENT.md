# Deployment

## API Deployment

The API is deployed by `.github/workflows/api-deploy.yml` on pushes to `main` and by manual `workflow_dispatch` runs.

Required GitHub secrets:

- `AZURE_WEBAPP_NAME`: Azure App Service app name for `PdfToolStack.API`.
- `AZURE_WEBAPP_PUBLISH_PROFILE`: publish profile XML downloaded from the Azure App Service.
- `API_HEALTH_URL`: full deployed health endpoint URL, including `/healthz`.

Deploy order:

1. Deploy the API workflow first and wait for the `/healthz` verification step to pass.
2. Deploy the frontend after the API is healthy so the UI points at a verified backend.

Verify deployment:

1. Confirm the GitHub Actions run completes successfully.
2. Open the URL stored in `API_HEALTH_URL`.
3. Confirm the response body is exactly `Healthy`.

Rollback note:

Redeploy the last known good GitHub Actions run or use the Azure App Service deployment history to restore the previous API package. After rollback, verify `API_HEALTH_URL` returns `Healthy`.
